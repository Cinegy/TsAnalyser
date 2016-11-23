/*   Copyright 2016 Cinegy GmbH

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Web;
using System.Text;
using System.Threading;
using CommandLine;
using Newtonsoft.Json;
using TsAnalyser.Metrics;
using TsAnalyser.Service;
using TsDecoder.TransportStream;
using TtxDecoder.Teletext;
using static System.String;

namespace TsAnalyser
{
    // ReSharper disable once ClassNeverInstantiated.Global
    internal class Program
    {
        private const int WarmUpTime = 500;
        private const int HistoricaBufferSize = 100000;

        private static bool _receiving;
        private static Options _options;
        
        private static bool _warmedUp;

        private static DateTime _startTime = DateTime.UtcNow;
        private static bool _pendingExit;
        private static ServiceHost _serviceHost;
        private static TsAnalyserApi _tsAnalyserApi;
        private static readonly UdpClient UdpClient = new UdpClient { ExclusiveAddressUse = false };
        private static readonly object LogfileWriteLock = new object();
        private static StreamWriter _logFileStream;
        private static readonly object JsonLogfileWriteLock = new object();
        private static StreamWriter _jsonLogFileStream;
        private static readonly object HistoricalFileLock = new object();
        private static bool _historicalBufferFlushing;

        private static readonly Queue<DataPacket> PacketQueue = new Queue<DataPacket>(3000);
        private static readonly Queue<DataPacket> HistoricalBuffer = new Queue<DataPacket>(HistoricaBufferSize);
        private static NetworkMetric _networkMetric;
        private static RtpMetric _rtpMetric = new RtpMetric();
        private static List<PidMetric> _pidMetrics = new List<PidMetric>();
        private static TsDecoder.TransportStream.TsDecoder _tsDecoder;
        private static TeleTextDecoder _ttxDecoder;

        private static readonly StringBuilder ConsoleDisplay = new StringBuilder(1024);
        private static int _lastPrintedTsCount;
        private static Timer _periodicDataTimer;

        static int Main(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return RunStreamInteractive();
            }

            if ((args.Length == 1) && (File.Exists(args[0])))
            {
                //a single argument was used, and it was a file - so skip all other parsing
                return Run(new ReadOptions {FileInput = args[0]});
            }

            var result = Parser.Default.ParseArguments<StreamOptions, ReadOptions>(args);

            return result.MapResult(
                (StreamOptions opts) => Run(opts),
                (ReadOptions opts) => Run(opts),
                errs => CheckArgumentErrors());
        }

        private static int CheckArgumentErrors()
        {
            //will print using library the appropriate help - now pause the console for the viewer
            Console.WriteLine("Hit enter to quit");
            Console.ReadLine();
            return -1;
        }

        private static int RunStreamInteractive()
        {
            Console.WriteLine("No arguments supplied - would you like to enter interactive mode? [Y/N]");
            var response = Console.ReadKey();

            if (response.Key != ConsoleKey.Y)
            {
                Console.WriteLine("\n\n");
                Parser.Default.ParseArguments<StreamOptions,ReadOptions>(new string[] { });
                return CheckArgumentErrors();
            }
            
            var newOpts = new StreamOptions();
            //ask the user interactively for an address and group
            Console.WriteLine(
                "\nYou chose to run in interactive mode, so now you can now set up a basic stream monitor. Making no entry uses defaults.");

            Console.Write("\nPlease enter the multicast address to listen to [239.1.1.1]: ");
            var address = Console.ReadLine();

            if (IsNullOrWhiteSpace(address)) address = "239.1.1.1";

            newOpts.MulticastAddress = address;

            Console.Write("Please enter the multicast group port [1234]: ");
            var port = Console.ReadLine();

            if (IsNullOrWhiteSpace(port))
            {
                port = "1234";
            }

            newOpts.MulticastGroup = int.Parse(port);

            Console.Write("Please enter the adapter address to listen for multicast packets [0.0.0.0]: ");

            var adapter = Console.ReadLine();

            if (IsNullOrWhiteSpace(adapter))
            {
                adapter = "0.0.0.0";
            }

            newOpts.AdapterAddress = adapter;

            return Run(newOpts);
        }

        private static int Run(Options opts)
        {
            Console.CancelKeyPress += Console_CancelKeyPress;

            Console.WriteLine(
                // ReSharper disable once AssignNullToNotNullAttribute
                $"Cinegy Transport Stream Monitoring and Analysis Tool (Built: {File.GetCreationTime(Assembly.GetExecutingAssembly().Location)})\n");

            try
            {
                Console.CursorVisible = false;
                Console.SetWindowSize(120, 60);
            }
            catch
            {
                Console.WriteLine("Failed to increase console size - probably screen resolution is low");
            }
            _options = opts;
            
            WorkLoop();

            return 0;
        }
        
        ~Program()
        {
            Console.CursorVisible = true;
        }

        private static void WorkLoop()
        {
            Console.Clear();

            if (!_receiving)
            {
                _receiving = true;

                LogMessage($"Logging started {Assembly.GetExecutingAssembly().GetName().Version}.");

                if (_options.EnableWebServices)
                {
                    var httpThreadStart = new ThreadStart(delegate
                    {
                        StartHttpService(_options.ServiceUrl);
                    });

                    var httpThread = new Thread(httpThreadStart) { Priority = ThreadPriority.Normal };

                    httpThread.Start();
                }

                SetupMetricsAndDecoders();
                
                var filePath = (_options as ReadOptions)?.FileInput;

                if (!IsNullOrEmpty(filePath))
                {
                    StartStreamingFile(filePath);
                }

                var streamOptions = _options as StreamOptions;
                if (streamOptions != null)
                {
                    if (!IsNullOrEmpty(streamOptions.TimeSeriesLogFile))
                    {
                        _periodicDataTimer = new Timer(UpdateSeriesDataTimerCallback, null, 0, 5000);
                    }

                    StartListeningToNetwork(streamOptions.MulticastAddress, streamOptions.MulticastGroup, streamOptions.AdapterAddress);
                }
            }

            Console.Clear();

            while (!_pendingExit)
            {
                if (!_options.SuppressOutput)
                {
                    PrintConsoleFeedback();
                }

                Thread.Sleep(20);
            }

            LogMessage("Logging stopped.");
        }

        private static void PrintConsoleFeedback()
        {
            var runningTime = DateTime.UtcNow.Subtract(_startTime);

            Console.SetCursorPosition(0, 0);

            if ((_options as StreamOptions) != null)
            {
                PrintToConsole("URL: rtp://@{0}:{1}\tRunning time: {2:hh\\:mm\\:ss}\t\t", ((StreamOptions)_options).MulticastAddress,
                    ((StreamOptions)_options).MulticastGroup, runningTime);

                PrintToConsole(
                    "\nNetwork Details\n----------------\nTotal Packets Rcvd: {0} \tBuffer Usage: {1:0.00}%/{2}\t\t\nTotal Data (MB): {3}\t\tPackets per sec:{4}",
                    _networkMetric.TotalPackets, _networkMetric.NetworkBufferUsage, PacketQueue.Count,
                    _networkMetric.TotalData / 1048576,
                    _networkMetric.PacketsPerSecond);
                PrintToConsole("Time Between Packets (ms): {0} \tShortest/Longest: {1}/{2}",
                    _networkMetric.TimeBetweenLastPacket, _networkMetric.ShortestTimeBetweenPackets,
                    _networkMetric.LongestTimeBetweenPackets);

                if (_historicalBufferFlushing)
                {
                    PrintToConsole("### Flushing historical stream buffer to file due to error! ###");
                }
                else
                {
                    PrintToConsole(
                        "Bitrates (Mbps): {0:0.00}/{1:0.00}/{2:0.00}/{3:0.00} (Current/Avg/Peak/Low)\t\t\t",
                        (_networkMetric.CurrentBitrate / 1048576.0), _networkMetric.AverageBitrate / 1048576.0,
                        (_networkMetric.HighestBitrate / 1048576.0), (_networkMetric.LowestBitrate / 1048576.0));
                }

                if (!((StreamOptions)_options).NoRtpHeaders)
                {
                    PrintToConsole(
                        "\nRTP Details\n----------------\nSeq Num: {0}\tMin Lost Pkts: {1}\nTimestamp: {2}\tSSRC: {3}\t",
                        _rtpMetric.LastSequenceNumber, _rtpMetric.EstimatedLostPackets, _rtpMetric.LastTimestamp,
                        _rtpMetric.Ssrc);
                }
            }

            lock (_pidMetrics)
            {
                PrintToConsole(_pidMetrics.Count < 10
                    ? $"\nPID Details - Unique PIDs: {_pidMetrics.Count}\n----------------"
                    : $"\nPID Details - Unique PIDs: {_pidMetrics.Count}, (10 shown by packet count)\n----------------");

                foreach (var pidMetric in _pidMetrics.OrderByDescending(m => m.PacketCount).Take(10))
                {
                    PrintToConsole("TS PID: {0}\tPacket Count: {1} \t\tCC Error Count: {2}\t", pidMetric.Pid,
                        pidMetric.PacketCount, pidMetric.CcErrorCount);
                }
            }

            if (_tsDecoder != null)
            {
                lock (_tsDecoder)
                {
                    var pmts = _tsDecoder?.ProgramMapTables.OrderBy(p => p.ProgramNumber).ToList();

                    PrintToConsole(pmts.Count < 5
                        ? $"\t\t\t\nService Information - Service Count: {pmts.Count}\n----------------\t\t\t\t"
                        : $"\t\t\t\nService Information - Service Count: {pmts.Count}, (5 shown)\n----------------\t\t\t\t");

                    foreach (var pmtable in pmts.Take(5))
                    {
                        var desc = _tsDecoder.GetServiceDescriptorForProgramNumber(pmtable?.ProgramNumber);
                        if (desc != null)
                        {
                            PrintToConsole(
                                $"Service {pmtable?.ProgramNumber}: {desc.ServiceName.Value} ({desc.ServiceProviderName.Value}) - {desc.ServiceTypeDescription}\t\t\t"
                                );
                        }
                    }

                    var pmt = _tsDecoder.GetSelectedPmt(_options.ProgramNumber);
                    if (pmt != null)
                    {
                        _options.ProgramNumber = pmt.ProgramNumber;
                    }

                    var serviceDesc = _tsDecoder.GetServiceDescriptorForProgramNumber(pmt?.ProgramNumber);

                    PrintToConsole(serviceDesc != null
                        ? $"\t\t\t\nElements - Selected Program {serviceDesc.ServiceName} (ID:{pmt?.ProgramNumber}) (first 5 shown)\n----------------\t\t\t\t"
                        : $"\t\t\t\nElements - Selected Program Service ID {pmt?.ProgramNumber} (first 5 shown)\n----------------\t\t\t\t");

                    if (pmt?.EsStreams != null)
                    {
                        foreach (var stream in pmt.EsStreams.Take(5))
                        {
                            if (stream != null)
                                PrintToConsole(
                                    "PID: {0} ({1})", stream.ElementaryPid,
                                    DescriptorDictionaries.ShortElementaryStreamTypeDescriptions[
                                        stream.StreamType]);
                        }
                    }
                }

                if (_options.DecodeTeletext)
                {
                    PrintTeletext();
                }
            }

            if (_lastPrintedTsCount != _pidMetrics.Count)
            {
                _lastPrintedTsCount = _pidMetrics.Count;
                Console.Clear();
            }

            Console.WriteLine(ConsoleDisplay.ToString());
            ConsoleDisplay.Clear();
        }

        private static void PrintTeletext()
        {
            lock (_ttxDecoder.TeletextDecodedSubtitlePage)
            {
                PrintToConsole($"\nTeleText Subtitles - decoding from Service ID {_ttxDecoder.ProgramNumber}\n----------------");

                foreach (var page in _ttxDecoder.TeletextDecodedSubtitlePage.Keys)
                {
                    PrintToConsole($"Live Decoding Page {page:X}\n");

                    //some strangeness here to get around the fact we just append to console, to clear out
                    //a fixed 4 lines of space for TTX render
                    const string clearLine = "\t\t\t\t\t\t\t\t\t";
                    var ttxRender = new[] { clearLine, clearLine, clearLine, clearLine };

                    var i = 0;

                    foreach (var line in _ttxDecoder.TeletextDecodedSubtitlePage[page])
                    {
                        if (IsNullOrEmpty(line) || IsNullOrEmpty(line.Trim()) || i >= ttxRender.Length)
                            continue;

                        ttxRender[i] = $"{new string(line.Where(c => !char.IsControl(c)).ToArray())}\t\t\t";
                        i++;
                    }

                    foreach (var val in ttxRender)
                    {
                        PrintToConsole(val);
                    }
                }
            }
        }

        private static void StartListeningToNetwork(string multicastAddress, int multicastGroup,
            string listenAdapter = "")
        {

            var listenAddress = IsNullOrEmpty(listenAdapter) ? IPAddress.Any : IPAddress.Parse(listenAdapter);

            var localEp = new IPEndPoint(listenAddress, multicastGroup);

            UdpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            UdpClient.Client.ReceiveBufferSize = 1500 * 3000;
            UdpClient.ExclusiveAddressUse = false;
            UdpClient.Client.Bind(localEp);
            _networkMetric.UdpClient = UdpClient;

            var parsedMcastAddr = IPAddress.Parse(multicastAddress);
            UdpClient.JoinMulticastGroup(parsedMcastAddr, listenAddress);

            var ts = new ThreadStart(delegate
            {
                ReceivingNetworkWorkerThread(UdpClient, localEp);
            });

            var receiverThread = new Thread(ts) { Priority = ThreadPriority.Highest };

            receiverThread.Start();

            var queueThread = new Thread(ProcessQueueWorkerThread) { Priority = ThreadPriority.AboveNormal };

            queueThread.Start();
        }

        private static void StartStreamingFile(string fileName)
        {
            var fs = new FileStream(fileName, FileMode.Open);

            var ts = new ThreadStart(delegate
            {
                FileStreamWorkerThread(fs);
            });

            var receiverThread = new Thread(ts) { Priority = ThreadPriority.Highest };

            receiverThread.Start();
        }

        private static void FileStreamWorkerThread(Stream stream)
        {
            var data = new byte[188];

            while (stream?.Read(data, 0, 188) > 0)
            {
                try
                {
                    var tsPackets = TsPacketFactory.GetTsPacketsFromData(data);

                    if (tsPackets == null) break;

                    AnalysePackets(tsPackets);

                }
                catch (Exception ex)
                {
                    LogMessage($@"Unhandled exception within file streamer: {ex.Message}");
                }
            }

            _pendingExit = true;
            Thread.Sleep(250);

            Console.WriteLine("Completed reading of file - hit enter to exit!");
            Console.ReadLine();
        }

        private static void ReceivingNetworkWorkerThread(UdpClient client, IPEndPoint localEp)
        {
            while (_receiving)
            {
                var data = client.Receive(ref localEp);
                if (data == null) continue;

                if (_warmedUp)
                {
                    var dataPkt = new DataPacket
                    {
                        DataPayload = data,
                        Timestamp = NetworkMetric.AccurateCurrentTime()
                    };

                    lock (PacketQueue)
                    {
                        PacketQueue.Enqueue(dataPkt);
                        if (PacketQueue.Count > HistoricaBufferSize)
                        {
                            LogMessage("Packet Queue grown too large - flushing all queues.");

                            //this event shall trigger the current historical buffer to write to a TS (if the historical buffer is full)
                            FlushHistoricalBufferToFile();
                            
							if(((StreamOptions)_options).SaveHistoricalData){
								LogMessage("Disabling historical data buffer after queue overflow - possibly resource constraints.");

								((StreamOptions) _options).SaveHistoricalData = false;
							}
							
                            PacketQueue.Clear();
                        }
                    }

                    if (!((StreamOptions)_options).SaveHistoricalData) continue;

                    lock (HistoricalBuffer)
                    {
                        HistoricalBuffer.Enqueue(dataPkt);
                        if (HistoricalBuffer.Count >= HistoricaBufferSize)
                        {
                            HistoricalBuffer.Dequeue();
                        }
                    }
                }
                else
                {
                    if (DateTime.UtcNow.Subtract(_startTime) > new TimeSpan(0, 0, 0, 0, WarmUpTime))
                        _warmedUp = true;
                }
            }
        }

        private static void ProcessQueueWorkerThread()
        {
            while (_pendingExit != true)
            {
                if (PacketQueue.Count > 0)
                {
                    DataPacket data;
                    lock (PacketQueue)
                    {
                        data = PacketQueue.Dequeue();
                    }
                    
                    if (data?.DataPayload == null) continue;
                  
                    try
                    {
                        lock (_networkMetric)
                        {
                            _networkMetric.AddPacket(data.DataPayload, data.Timestamp, PacketQueue.Count);

                            if (!((StreamOptions)_options).NoRtpHeaders)
                            {
                                _rtpMetric.AddPacket(data.DataPayload);
                            }

                            var tsPackets = TsPacketFactory.GetTsPacketsFromData(data.DataPayload);

                            if (tsPackets == null) break;

                            AnalysePackets(tsPackets);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMessage($@"Unhandled exception within network receiver: {ex.Message}");
                    }
                }
                else
                {
                    Thread.Sleep(1);
                }
            }
            LogMessage("Stopping analysis thread due to exit request.");
        }

        private static void AnalysePackets(IEnumerable<TsPacket> tsPackets)
        {
            lock (_pidMetrics)
            {
                foreach (var tsPacket in tsPackets)
                {
                    var currentPidMetric = _pidMetrics.FirstOrDefault(pidMetric => pidMetric.Pid == tsPacket.Pid);

                    if (currentPidMetric == null)
                    {
                        currentPidMetric = new PidMetric { Pid = tsPacket.Pid };
                        currentPidMetric.DiscontinuityDetected += currentMetric_DiscontinuityDetected;
                        currentPidMetric.TransportErrorIndicatorDetected += currentMetric_TransportErrorIndicatorDetected;
                        _pidMetrics.Add(currentPidMetric);
                    }

                    currentPidMetric.AddPacket(tsPacket);

                    if (_tsDecoder == null) continue;
                    lock (_tsDecoder)
                    {
                        _tsDecoder.AddPacket(tsPacket);

                        if (_ttxDecoder == null) continue;
                        lock (_ttxDecoder)
                        {
                            _ttxDecoder.AddPacket(_tsDecoder, tsPacket);
                        }
                    }
                }
            }
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Console.CursorVisible = true;
            if (_pendingExit) return; //already trying to exit - allow normal behaviour on subsequent presses
            _pendingExit = true;
            e.Cancel = true;
        }

        private static void currentMetric_DiscontinuityDetected(object sender, TransportStreamEventArgs e)
        {
            LogMessage($"Discontinuity on TS PID {e.TsPid}");

            //this event shall trigger the current historical buffer to write to a TS (if the historical buffer is full)
            FlushHistoricalBufferToFile();
        }

        private static void currentMetric_TransportErrorIndicatorDetected(object sender, TransportStreamEventArgs e)
        {
            LogMessage($"Transport Error Indicator on TS PID {e.TsPid}");
        }

        private static void RtpMetric_SequenceDiscontinuityDetected(object sender, EventArgs e)
        {
            LogMessage("Discontinuity in RTP sequence.");

            //this event shall trigger the current historical buffer to write to a TS (if the historical buffer is full)
            FlushHistoricalBufferToFile();
        }

        private static void NetworkMetric_BufferOverflow(object sender, EventArgs e)
        {
            LogMessage("Network buffer > 99% - probably loss of data from overflow.");
        }

        private static void _networkMetric_ExcessiveIat(object sender, IatEventArgs args)
        {
            LogMessage($"Excessive Inter-Packet Arrival Time (IAT) - max {args.MaxIat}, detected {args.MeasuredIat}.");
        }

        private static void PrintToConsole(string message, params object[] arguments)
        {
            if (_options.SuppressOutput) return;

            ConsoleDisplay.AppendLine(Format(message, arguments));
        }

        private static void LogMessage(string message)
        {
            ThreadPool.QueueUserWorkItem(WriteToFile, message);
        }

        private static void FlushHistoricalBufferToFile()
        {
            if (_historicalBufferFlushing) return;

            if (HistoricalBuffer.Count < 1) return;

            if (!((StreamOptions)_options).SaveHistoricalData) return;

            ThreadPool.QueueUserWorkItem(WriteHistoricalData, null);

        }

        private static void WriteHistoricalData(object context)
        {
            _historicalBufferFlushing = true;

            DataPacket[] recentTs;

            lock (HistoricalBuffer)
            {
                //return if buffer is not nearly full - either we are just starting, or the buffer was recently flushed...
                if (HistoricalBuffer.Count < (HistoricaBufferSize - 10))
                {
                    _historicalBufferFlushing = false;
                    return;
                }

                LogMessage("Flushing recent data into file.");

                recentTs = HistoricalBuffer.ToArray();

                HistoricalBuffer.Clear();
            }

            lock (HistoricalFileLock)
            {
                try
                {
                    if (IsNullOrWhiteSpace(_options.LogFile)) return;

                    var fileName = Path.GetDirectoryName(_options.LogFile) + $"\\streamerror-{DateTime.UtcNow.ToFileTime()}.ts";

                    var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write);

                    var errorStream = new BinaryWriter(fs);

                    foreach (var dataPacket in recentTs)
                    {
                        if (((StreamOptions)_options).NoRtpHeaders)
                        {
                            errorStream.Write(dataPacket.DataPayload);
                        }
                        else
                        {
                            errorStream.Write(dataPacket.DataPayload, 12, dataPacket.DataPayload.Length - 12);
                        }
                    }

                    //sleep 5 seconds, to allow the historical buffer to fill with some more packets after the event
                    //before flushing
                    Thread.Sleep(5000);

                    lock (HistoricalBuffer)
                    {
                        if (HistoricalBuffer.Count > (HistoricaBufferSize - 10))
                        {
                            LogMessage("Packet rate is too high for historical buffer size - clipped error stream");
                            _historicalBufferFlushing = false;
                            return;
                        }

                        recentTs = HistoricalBuffer.ToArray();

                        HistoricalBuffer.Clear();

                        foreach (var dataPacket in recentTs)
                        {
                            if (((StreamOptions)_options).NoRtpHeaders)
                            {
                                errorStream.Write(dataPacket.DataPayload);
                            }
                            else
                            {
                                errorStream.Write(dataPacket.DataPayload, 12, dataPacket.DataPayload.Length - 12);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    LogMessage("Error writing error buffer to file...");
                    _historicalBufferFlushing = false;
                }

                LogMessage("Finished flushing recent data into file.");
                _historicalBufferFlushing = false;
                Console.Clear();
            }
        }

        private static void WriteToFile(object msg)
        {
            lock (LogfileWriteLock)
            {
                try
                {
                    if (_logFileStream == null || _logFileStream.BaseStream.CanWrite != true)
                    {
                        if (IsNullOrWhiteSpace(_options.LogFile)) return;

                        var fs = new FileStream(_options.LogFile, FileMode.Append, FileAccess.Write);

                        _logFileStream = new StreamWriter(fs) { AutoFlush = true };
                    }

                    var jsonMsg = new JsonMsg() { EventMessage = msg.ToString(), EventTags = _options.DescriptorTags };

                    var formattedMsg = JsonConvert.SerializeObject(jsonMsg);
                  
                    _logFileStream.WriteLine(formattedMsg);
                }
                catch (Exception)
                {
                    Debug.WriteLine("Concurrency error writing to log file...");
                    _logFileStream?.Close();
                    _logFileStream?.Dispose();
                }
            }
        }

        private static void UpdateSeriesDataTimerCallback(object o)
        {
            lock (JsonLogfileWriteLock)
            {
                try
                {
                    if (_jsonLogFileStream == null || _jsonLogFileStream.BaseStream.CanWrite != true)
                    {
                        if (IsNullOrWhiteSpace(((StreamOptions)_options).TimeSeriesLogFile)) return;

                        var fs = new FileStream(((StreamOptions)_options).TimeSeriesLogFile, FileMode.Append, FileAccess.Write);

                        _jsonLogFileStream = new StreamWriter(fs) { AutoFlush = true };
                    }

                    var sb = new StringBuilder();
                    var qt = '"'.ToString();
                    sb.Append($"{{{qt}Ts{qt}:{{");

                    sb.Append($"{qt}Tags{qt}:{qt}{_options.DescriptorTags}{qt},");

                    var json = JsonConvert.SerializeObject(_networkMetric);
                    sb.Append($"{qt}Net{qt}:{json},");

                    if (!((StreamOptions)_options).NoRtpHeaders)
                    {
                        json = JsonConvert.SerializeObject(_rtpMetric);
                        sb.Append($"{qt}Rtp{qt}:{json},");
                    }

                    var pidCount = 0;
                    long totalCcErrors = 0;
                    long totalPidPackets = 0;
                    foreach (var pidMetric in _pidMetrics)
                    {
                        pidCount++;
                        totalPidPackets += pidMetric.PeriodPacketCount;
                        totalCcErrors += pidMetric.PeriodCcErrorCount;
                    }

                    sb.Append($"{qt}Pid{qt}:{{");
                    sb.Append($"{qt}Count{qt}:{pidCount},");
                    sb.Append($"{qt}Packets{qt}:{totalPidPackets},");
                    sb.Append($"{qt}CCErrors{qt}:{totalCcErrors}");

                    sb.Append("}}}");

                    _jsonLogFileStream.WriteLine($"{sb}");



                }
                catch (Exception)
                {
                    Debug.WriteLine("Concurrency error writing to log file...");
                    _logFileStream?.Close();
                    _logFileStream?.Dispose();
                }
            }
        }

        private static void StartHttpService(string serviceAddress)
        {
            var baseAddress = new Uri(serviceAddress);

            _serviceHost?.Close();

            _tsAnalyserApi = new TsAnalyserApi
            {
                NetworkMetric = _networkMetric,
                TsMetrics = _pidMetrics,
                RtpMetric = _rtpMetric
            };

            _tsAnalyserApi.StreamCommand += _tsAnalyserApi_StreamCommand;

            _serviceHost = new ServiceHost(_tsAnalyserApi, baseAddress);
            var webBinding = new WebHttpBinding();

            var serviceEndpoint = new ServiceEndpoint(ContractDescription.GetContract(typeof(ITsAnalyserApi)))
            {
                Binding = webBinding,
                Address = new EndpointAddress(baseAddress)
            };

            _serviceHost.AddServiceEndpoint(serviceEndpoint);

            var webBehavior = new WebHttpBehavior
            {
                AutomaticFormatSelectionEnabled = true,
                DefaultOutgoingRequestFormat = WebMessageFormat.Json,
                HelpEnabled = true
            };

            serviceEndpoint.Behaviors.Add(webBehavior);

            //TODO: Trying to disable MEX, to allow serving of index when visiting root...

            //Metadata Exchange
            //var serviceBehavior = new ServiceMetadataBehavior {HttpGetEnabled = true};
            //_serviceHost.Description.Behaviors.AddData(serviceBehavior);

            try
            {
                _serviceHost.Open();
            }
            catch (Exception ex)
            {
                var msg =
                    "Failed to start local web API for player - either something is already using the requested URL, the tool is not running as local administrator, or netsh url reservations have not been made " +
                    "to allow non-admin users to host services.\n\n" +
                    "To make a URL reservation, permitting non-admin execution, run:\n" +
                    "netsh http add urlacl http://+:8124/ user=BUILTIN\\Users\n\n" +
                    "This is the details of the exception thrown:" +
                    ex.Message +
                    "\n\nHit enter to continue without services.\n\n";

                Console.WriteLine(msg);

                Console.ReadLine();

                LogMessage(msg);
            }
        }

        private static void SetupMetricsAndDecoders()
        {
            lock (_pidMetrics)
            {
                _startTime = DateTime.UtcNow;

                var streamOpts = _options as StreamOptions;

                if (streamOpts != null)
                {
                    _networkMetric = new NetworkMetric()
                    {
                        MaxIat = streamOpts.InterArrivalTimeMax,
                        MulticastAddress = streamOpts.MulticastAddress,
                        MulticastGroup = streamOpts.MulticastGroup
                    };

                    _rtpMetric = new RtpMetric();

                    _rtpMetric.SequenceDiscontinuityDetected += RtpMetric_SequenceDiscontinuityDetected;
                    _networkMetric.BufferOverflow += NetworkMetric_BufferOverflow;
                    _networkMetric.ExcessiveIat += _networkMetric_ExcessiveIat;
                    _networkMetric.UdpClient = UdpClient;
                }

                _pidMetrics = new List<PidMetric>();

                if (!_options.SkipDecodeTransportStream)
                {
                    _tsDecoder = new TsDecoder.TransportStream.TsDecoder();
                    _tsDecoder.TableChangeDetected += _tsDecoder_TableChangeDetected;
                }

                if (_options.DecodeTeletext)
                {
                    _ttxDecoder = _options.ProgramNumber > 1 ? new TeleTextDecoder(_options.ProgramNumber) : new TeleTextDecoder();
                }

                
            }
        }

        private static void _tsDecoder_TableChangeDetected(object sender, TableChangedEventArgs e)
        {
            LogMessage("Table Change: " + e.Message);
            Console.Clear();
        }

        private static void _tsAnalyserApi_StreamCommand(object sender, StreamCommandEventArgs e)
        {
            switch (e.Command)
            {
                case (StreamCommandType.ResetMetrics):
                    SetupMetricsAndDecoders();

                    _tsAnalyserApi.NetworkMetric = _networkMetric;
                    _tsAnalyserApi.TsMetrics = _pidMetrics;
                    _tsAnalyserApi.RtpMetric = _rtpMetric;
                    Console.Clear();

                    break;
                case (StreamCommandType.StopStream):
                    //todo: implement
                    break;
                case (StreamCommandType.StartStream):
                    //todo: implement
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
        [SuppressMessage("ReSharper", "UnusedMember.Local")]
        private class JsonMsg
        {
            public string EventTime = DateTime.UtcNow.ToString("o");

            public string EventTags { get; set; }
            public string EventMessage { get; set; }
        }
    }
}

