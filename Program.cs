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
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime;
using System.Text;
using System.Threading;
using Cinegy.Telemetry;
using CommandLine;
using Newtonsoft.Json;
using TsAnalyser.Logging;
using TsAnalyser.Metrics;
using Cinegy.TsDecoder.TransportStream;
using Cinegy.TtxDecoder.Teletext;
using NLog;
using NLog.Fluent;
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

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private static DateTime _startTime = DateTime.UtcNow;
        private static bool _pendingExit;
        private static readonly UdpClient UdpClient = new UdpClient { ExclusiveAddressUse = false };
        private static readonly object LogfileWriteLock = new object();
        private static StreamWriter _logFileStream;
        private static readonly object HistoricalFileLock = new object();
        private static bool _historicalBufferFlushing;
        
        private static readonly RingBuffer RingBuffer = new RingBuffer();
        private static readonly Queue<DataPacket> HistoricalBuffer = new Queue<DataPacket>(HistoricaBufferSize);
        private static ulong _lastPcr;
        private static NetworkMetric _networkMetric;
        private static RtpMetric _rtpMetric = new RtpMetric();
        private static List<PidMetric> _pidMetrics = new List<PidMetric>();
        private static TsDecoder _tsDecoder;
        private static TeleTextDecoder _ttxDecoder;

        private static readonly StringBuilder ConsoleDisplay = new StringBuilder(1024);
        private static int _lastPrintedTsCount;
        // ReSharper disable once NotAccessedField.Local
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
                return Run(new ReadOptions { FileInput = args[0] });
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
                Parser.Default.ParseArguments<StreamOptions, ReadOptions>(new string[] { });
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

            LogSetup.ConfigureLogger("tsanalyser", opts.OrganizationId, opts.DescriptorTags,"https://telemetry.cinegy.com", opts.TelemetryEnabled);

            var location = Assembly.GetExecutingAssembly().Location;

            if (location != null)
                Logger.Info($"Cinegy Transport Stream Monitoring and Analysis Tool (Built: {File.GetCreationTime(location).ToLongDateString()})");

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

            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

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
                
                _periodicDataTimer = new Timer(UpdateSeriesDataTimerCallback, null, 0, 5000);

                SetupMetricsAndDecoders();

                var filePath = (_options as ReadOptions)?.FileInput;

                if (!IsNullOrEmpty(filePath))
                {
                    StartStreamingFile(filePath);
                }


                var streamOptions = _options as StreamOptions;
                if (streamOptions != null)
                {

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
                    "\nNetwork Details\n----------------\nTotal Packets Rcvd: {0} \tBuffer Usage: {1:0.00}%/(Peak: {2:0.00}%)\t\t\nTotal Data (MB): {3}\t\tPackets per sec:{4}",
                    _networkMetric.TotalPackets, _networkMetric.NetworkBufferUsage, _networkMetric.PeriodMaxNetworkBufferUsage,
                    _networkMetric.TotalData / 1048576,
                    _networkMetric.PacketsPerSecond);

                PrintToConsole("Period Max Packet Jitter (ms): {0}\t\t",
                    _networkMetric.PeriodLongestTimeBetweenPackets);

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
                var span = new TimeSpan((long)(_lastPcr/2.7));
                PrintToConsole(_lastPcr > 0 ? $"\nPCR Value: {span}\n----------------" : "\n\n");
                
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
            var factory = new TsPacketFactory();

            while (stream?.Read(data, 0, 188) > 0)
            {
                try
                {
                    var tsPackets = factory.GetTsPacketsFromData(data);

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
                    RingBuffer.Add(ref data);
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
            var dataBuffer = new byte[12 + (188 * 7)];
            var factory = new TsPacketFactory();

            while (_pendingExit != true)
            {
                int dataSize;
                long timestamp;
                var capacity = RingBuffer.Remove(ref dataBuffer, out dataSize, out timestamp);

                if (capacity > 0)
                {
                    dataBuffer = new byte[capacity];
                    continue;
                }

                if (dataBuffer == null) continue;

                //TODO: Reimplement support for historical buffer dumping

                //if (_packetQueue.Count > HistoricaBufferSize)
                //{
                //    LogMessage(new LogRecord()
                //    {
                //        EventCategory = "Error",
                //        EventKey = "BufferOverflow",
                //        EventTags = _options.DescriptorTags,
                //        EventMessage = "Packet Queue grown too large - flushing all queues."
                //    });

                //    //this event shall trigger the current historical buffer to write to a TS (if the historical buffer is full)
                //    FlushHistoricalBufferToFile();

                //    if (((StreamOptions)_options).SaveHistoricalData)
                //    {
                //        LogMessage("Disabling historical data buffer after queue overflow - possibly resource constraints.");

                //        ((StreamOptions)_options).SaveHistoricalData = false;
                //    }

                //    _packetQueue = new ConcurrentQueue<DataPacket>();
                //}

                //if (((StreamOptions) _options).SaveHistoricalData)
                //{
                //    lock (HistoricalBuffer)
                //    {
                //        HistoricalBuffer.Enqueue(data);
                //        if (HistoricalBuffer.Count >= HistoricaBufferSize)
                //        {
                //            HistoricalBuffer.Dequeue();
                //        }
                //    }
                //}

                try
                {
                    lock (_networkMetric)
                    {
                        //TODO: Inject ringbuffer delta below (after removing queue)
                        _networkMetric.AddPacket(dataBuffer, timestamp, 0);

                        if (!((StreamOptions)_options).NoRtpHeaders)
                        {
                            _rtpMetric.AddPacket(dataBuffer);
                        }

                        var tsPackets = factory.GetTsPacketsFromData(dataBuffer);

                        if (tsPackets == null)
                        {
                            Logger.Log(new TelemetryLogEventInfo() {Message = "Packet recieved with no detected TS packets",Level = LogLevel.Warn, Key="Packet"});
                            continue;
                        }

                        AnalysePackets(tsPackets);
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($@"Unhandled exception within network receiver: {ex.Message}");
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
                    if (tsPacket.AdaptationFieldExists)
                    {
                        if (tsPacket.AdaptationField.PcrFlag)
                        {
                            _lastPcr = tsPacket.AdaptationField.Pcr;
                        }
                    }

                    PidMetric currentPidMetric = null;
                    foreach (var pidMetric in _pidMetrics)
                    {
                        if (pidMetric.Pid != tsPacket.Pid) continue;
                        currentPidMetric = pidMetric;
                        break;
                    }

                    if (currentPidMetric == null)
                    {
                        currentPidMetric = new PidMetric { Pid = tsPacket.Pid };
                        currentPidMetric.DiscontinuityDetected += currentMetric_DiscontinuityDetected;
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
            if (_options.VerboseLogging)
            {
                //FIX

                /*
                LogMessage(new LogRecord()
                {
                    EventCategory = "Info",
                    EventKey = "Discontinuity",
                    EventTags = _options.DescriptorTags,
                    EventMessage = $"Discontinuity on TS PID {e.TsPid}"
                });*/
            }

            //this event shall trigger the current historical buffer to write to a TS (if the historical buffer is full)
            FlushHistoricalBufferToFile();
        }

        private static void RtpMetric_SequenceDiscontinuityDetected(object sender, EventArgs e)
        {
            if (_options.VerboseLogging)
            {
                Logger.Log(new TelemetryLogEventInfo()
                {
                    Message = "Discontinuity in RTP sequence",
                    Level = LogLevel.Warn,
                    Key = "Discontinuity"
                });
            }

            //this event shall trigger the current historical buffer to write to a TS (if the historical buffer is full)
            FlushHistoricalBufferToFile();
        }

        private static void NetworkMetric_BufferOverflow(object sender, EventArgs e)
        {
            Logger.Log(new TelemetryLogEventInfo()
            {
                Message = "Network buffer > 99% - probably loss of data from overflow",
                Level = LogLevel.Error,
                Key = "Overflow"
            });
        }

        private static void PrintToConsole(string message, params object[] arguments)
        {
            if (_options.SuppressOutput) return;

            ConsoleDisplay.AppendLine(Format(message, arguments));
        }

        private static void LogMessage(string message)
        {
            var lei = new TelemetryLogEventInfo
            {
                Level = LogLevel.Info,
                Key = "GenericEvent",
                Message = message
            };

            Logger.Log(lei);
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
                            Logger.Log(new TelemetryLogEventInfo()
                            {
                                Message = "Packet rate is too high for historical buffer size - clipped error stream",
                                Level = LogLevel.Error,
                                Key = "Rate"
                            });
                        
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
                    Logger.Log(new TelemetryLogEventInfo()
                    {
                        Message = "Error writing error buffer to file",
                        Level = LogLevel.Error,
                        Key = "IO"
                    });

                    _historicalBufferFlushing = false;
                }

                Logger.Log(new TelemetryLogEventInfo()
                {
                    Message = "Finished flushing recent data into file",
                    Level = LogLevel.Info,
                    Key = "IO"
                });
                
                _historicalBufferFlushing = false;
                Console.Clear();
            }
        }

        private static void WriteToFile(object line)
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
                    _logFileStream.WriteLine(line);
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
            try
            {
                var tsMetricLogRecord = new TsMetricLogRecord()
                {
                    Net = _networkMetric
                };

                if (!((StreamOptions)_options).NoRtpHeaders)
                {
                    tsMetricLogRecord.Rtp = _rtpMetric;
                }

                var tsmetric = new TsMetric();

                foreach (var pidMetric in _pidMetrics)
                {
                    tsmetric.PidCount++;
                    tsmetric.PidPackets += pidMetric.PeriodPacketCount;
                    tsmetric.PidCcErrors += pidMetric.PeriodCcErrorCount;
                    tsmetric.TeiErrors += pidMetric.PeriodTeiCount;

                    if (tsmetric.LongestPcrDelta < pidMetric.PeriodLargestPcrDelta)
                    {
                        tsmetric.LongestPcrDelta = pidMetric.PeriodLargestPcrDelta;
                    }

                    if (tsmetric.LargestPcrDrift < pidMetric.PeriodLargestPcrDrift)
                    {
                        tsmetric.LargestPcrDrift = pidMetric.PeriodLargestPcrDrift;
                    }

                    if (tsmetric.LowestPcrDrift < pidMetric.PeriodLowestPcrDrift)
                    {
                        tsmetric.LowestPcrDrift = pidMetric.PeriodLowestPcrDrift;
                    }
                }

                tsMetricLogRecord.Ts = tsmetric;

                LogEventInfo lei = new TelemetryLogEventInfo
                {
                    Key = "TSD",
                    TelemetryObject = tsMetricLogRecord,
                    Level = LogLevel.Info
                };

                Logger.Log(lei);
                
            }
            catch (Exception)
            {
                Debug.WriteLine("Concurrency error writing to log file...");
                _logFileStream?.Close();
                _logFileStream?.Dispose();
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
                    _networkMetric.UdpClient = UdpClient;
                }

                _pidMetrics = new List<PidMetric>();

                if (!_options.SkipDecodeTransportStream)
                {
                    _tsDecoder = new TsDecoder();
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
            Logger.Log(new TelemetryLogEventInfo()
            {
                Message = "Table Change: " + e.Message,
                Level = LogLevel.Info,
                Key = "TableChange"
            });
            
            Console.Clear();
        }

    }
}

