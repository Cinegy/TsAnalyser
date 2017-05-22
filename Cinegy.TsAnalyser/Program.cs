/*   Copyright 2017 Cinegy GmbH

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
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime;
using System.Text;
using System.Threading;
using Cinegy.Telemetry;
using Cinegy.TsAnalysis;
using Cinegy.TsDecoder.TransportStream;
using Cinegy.TtxDecoder.Teletext;
using CommandLine;
using NLog;

namespace Cinegy.TsAnalyser
{
    // ReSharper disable once ClassNeverInstantiated.Global
    internal class Program
    {
        private const int WarmUpTime = 500;
        private static bool _receiving;
        private static Options _options;
        private static bool _warmedUp;
        private static  Logger Logger;
        private static Analyser _analyser;
        private static DateTime _startTime = DateTime.UtcNow;
        private static bool _pendingExit;
        private static readonly UdpClient UdpClient = new UdpClient { ExclusiveAddressUse = false };
        private static readonly StringBuilder ConsoleDisplay = new StringBuilder(1024);
        private static int _lastPrintedTsCount;
        
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

            if (string.IsNullOrWhiteSpace(address)) address = "239.1.1.1";

            newOpts.MulticastAddress = address;

            Console.Write("Please enter the multicast group port [1234]: ");
            var port = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(port))
            {
                port = "1234";
            }

            newOpts.MulticastGroup = int.Parse(port);

            Console.Write("Please enter the adapter address to listen for multicast packets [0.0.0.0]: ");

            var adapter = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(adapter))
            {
                adapter = "0.0.0.0";
            }

            newOpts.AdapterAddress = adapter;

            return Run(newOpts);
        }

        private static int Run(Options opts)
        {
            Console.CancelKeyPress += Console_CancelKeyPress;

            Logger = LogManager.GetCurrentClassLogger();

            LogSetup.ConfigureLogger("tsanalyser", opts.OrganizationId, opts.DescriptorTags, "https://telemetry.cinegy.com", opts.TelemetryEnabled, false);

            _analyser = new Analyser(Logger);

            var location = Assembly.GetEntryAssembly().Location;
            
            Logger.Info($"Dumb test)");

            if (location != null)
                Logger.Info($"Cinegy Transport Stream Monitoring and Analysis Tool (Built: {File.GetCreationTime(location)})");

            try
            {
                Console.CursorVisible = false;
                Console.SetWindowSize(120, 60);
                Console.OutputEncoding = Encoding.Unicode;
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
            
            _receiving = true;

            LogMessage($"Logging started {Assembly.GetEntryAssembly().GetName().Version}.");

            _analyser.TsDecoder.TableChangeDetected += TsDecoder_TableChangeDetected;
            _analyser.InspectTeletext = _options.DecodeTeletext;
            _analyser.InspectTsPackets = !_options.SkipDecodeTransportStream;
            _analyser.SelectedProgramNumber = _options.ProgramNumber;
            _analyser.VerboseLogging = _options.VerboseLogging;

            var streamOptions = _options as StreamOptions;

            var filePath = (_options as ReadOptions)?.FileInput;

            if (!string.IsNullOrEmpty(filePath))
            {
                _analyser.Setup();
                StartStreamingFile(filePath);
            }

            if (streamOptions != null)
            {
                _analyser.HasRtpHeaders = !streamOptions.NoRtpHeaders;
                _analyser.Setup(streamOptions.MulticastAddress,streamOptions.MulticastGroup);

                if (_analyser.InspectTeletext)
                {
                    _analyser.TeletextDecoder.Service.TeletextPageReady += Service_TeletextPageReady;
                    _analyser.TeletextDecoder.Service.TeletextPageCleared += Service_TeletextPageCleared;                        
                }

                StartListeningToNetwork(streamOptions.MulticastAddress, streamOptions.MulticastGroup, streamOptions.AdapterAddress);
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

        private static void TsDecoder_TableChangeDetected(object sender, TableChangedEventArgs args)
        {
            if ((args.TableType == TableType.Pat) || (args.TableType == TableType.Pmt) || (args.TableType == TableType.Sdt))
            {
                //abuse occasional table refresh to clear all content on screen
                Console.Clear();
            }
        }

        private static void PrintConsoleFeedback()
        {
            var runningTime = DateTime.UtcNow.Subtract(_startTime);

            Console.SetCursorPosition(0, 0);

            if ((_options as StreamOptions) != null)
            {
                PrintToConsole("URL: rtp://@{0}:{1}\tRunning time: {2:hh\\:mm\\:ss}\t\t", ((StreamOptions)_options).MulticastAddress,
                    ((StreamOptions)_options).MulticastGroup, runningTime);

                var _networkMetric = _analyser.NetworkMetric;
                var _rtpMetric = _analyser.RtpMetric;

                PrintToConsole(
                    "\nNetwork Details\n----------------\nTotal Packets Rcvd: {0} \tBuffer Usage: {1:0.00}%/(Peak: {2:0.00}%)\t\t\nTotal Data (MB): {3}\t\tPackets per sec:{4}",
                    _networkMetric.TotalPackets, _networkMetric.NetworkBufferUsage, _networkMetric.PeriodMaxNetworkBufferUsage,
                    _networkMetric.TotalData / 1048576,
                    _networkMetric.PacketsPerSecond);

                PrintToConsole("Period Max Packet Jitter (ms): {0}\t\t",
                    _networkMetric.PeriodLongestTimeBetweenPackets);

                PrintToConsole(
                    "Bitrates (Mbps): {0:0.00}/{1:0.00}/{2:0.00}/{3:0.00} (Current/Avg/Peak/Low)\t\t\t",
                    (_networkMetric.CurrentBitrate / 1048576.0), _networkMetric.AverageBitrate / 1048576.0,
                    (_networkMetric.HighestBitrate / 1048576.0), (_networkMetric.LowestBitrate / 1048576.0));

                if (!((StreamOptions)_options).NoRtpHeaders)
                {
                    PrintToConsole(
                        "\nRTP Details\n----------------\nSeq Num: {0}\tMin Lost Pkts: {1}\nTimestamp: {2}\tSSRC: {3}\t",
                        _rtpMetric.LastSequenceNumber, _rtpMetric.EstimatedLostPackets, _rtpMetric.LastTimestamp,
                        _rtpMetric.Ssrc);
                }
            }

            var _pidMetrics = _analyser.PidMetrics;

            lock (_pidMetrics)
            {

                var span = new TimeSpan((long)(_analyser.LastPcr / 2.7));
                PrintToConsole(_analyser.LastPcr > 0 ? $"\nPCR Value: {span}\n----------------" : "\n\n");

                PrintToConsole(_pidMetrics.Count < 10
                    ? $"\nPID Details - Unique PIDs: {_pidMetrics.Count}\n----------------"
                    : $"\nPID Details - Unique PIDs: {_pidMetrics.Count}, (10 shown by packet count)\n----------------");

                foreach (var pidMetric in _pidMetrics.OrderByDescending(m => m.PacketCount).Take(10))
                {
                    PrintToConsole("TS PID: {0}\tPacket Count: {1} \t\tCC Error Count: {2}\t", pidMetric.Pid,
                        pidMetric.PacketCount, pidMetric.CcErrorCount);
                }
            }

            var _tsDecoder = _analyser.TsDecoder;

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
                        _analyser.SelectedPcrPid = pmt.PcrPid;
                    }

                    var serviceDesc = _tsDecoder.GetServiceDescriptorForProgramNumber(pmt?.ProgramNumber);

                    PrintToConsole(serviceDesc != null
                        ? $"\t\t\t\nElements - Selected Program: {serviceDesc.ServiceName} (ID:{pmt?.ProgramNumber}) (first 5 shown)\n----------------\t\t\t\t"
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

            var result = ConsoleDisplay.ToString();
            Console.WriteLine(result);
            ConsoleDisplay.Clear();
        }

        private static void PrintTeletext()
        {
            //some strangeness here to get around the fact we just append to console, to clear out
            //a fixed 4 lines of space for TTX render
            const string clearLine = "\t\t\t\t\t\t\t\t\t";
            var ttxRender = new[] { clearLine, clearLine, clearLine, clearLine };

            if (DecodedSubtitlePage != null)
            {
                lock (DecodedSubtitlePage)
                {
                    var defaultLang = DecodedSubtitlePage.ParentMagazine.ParentService.AssociatedDescriptor.Languages
                        .FirstOrDefault();
                    
                    PrintToConsole(
                        $"\nTeletext Subtitles ({defaultLang.Iso639LanguageCode})- decoding from Service ID {DecodedSubtitlePage.ParentMagazine.ParentService.ProgramNumber}, PID: {DecodedSubtitlePage.ParentMagazine.ParentService.TeletextPid}");

                    PrintToConsole(
                        $"Total Pages: {_analyser.TeletextDecoder.Service.Metric.TtxPageReadyCount}, Total Clears: {_analyser.TeletextDecoder.Service.Metric.TtxPageClearCount}\n----------------");

                    PrintToConsole($"Live Decoding Page {DecodedSubtitlePage.ParentMagazine.MagazineNum}{DecodedSubtitlePage.PageNum:00}\n");
                    
                    var i = 0;

                    foreach (var row in DecodedSubtitlePage.Rows)
                    {
                        if (!row.IsChanged() || string.IsNullOrWhiteSpace(row.GetPlainRow())) continue;
                        ttxRender[i] = $"{row.GetPlainRow()}\t\t\t";
                        i++;
                    }
                }
            }

            foreach (var val in ttxRender)
            {
                PrintToConsole(val);
            }
        }
        
        private static void StartListeningToNetwork(string multicastAddress, int multicastGroup,
            string listenAdapter = "")
        {

            var listenAddress = string.IsNullOrEmpty(listenAdapter) ? IPAddress.Any : IPAddress.Parse(listenAdapter);

            var localEp = new IPEndPoint(listenAddress, multicastGroup);

            UdpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            UdpClient.Client.ReceiveBufferSize = 1500 * 3000;
            UdpClient.ExclusiveAddressUse = false;
            UdpClient.Client.Bind(localEp);
            _analyser.NetworkMetric.UdpClient = UdpClient;

            var parsedMcastAddr = IPAddress.Parse(multicastAddress);
            UdpClient.JoinMulticastGroup(parsedMcastAddr, listenAddress);

            var ts = new ThreadStart(delegate
            {
                ReceivingNetworkWorkerThread(UdpClient);
            });

            var receiverThread = new Thread(ts) { };

            receiverThread.Start();

       
        }

        private static void StartStreamingFile(string fileName)
        {
            var fs = new FileStream(fileName, FileMode.Open);

            var ts = new ThreadStart(delegate
            {
                FileStreamWorkerThread(fs);
            });
            
            var receiverThread = new Thread(ts);

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

                    _analyser.AnalysePackets(tsPackets);

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

        private static void ReceivingNetworkWorkerThread(UdpClient client)
        {
            while (_receiving && !_pendingExit)
            {
                var data = client.ReceiveAsync().Result.Buffer;

                if (data == null) continue;

                if (_warmedUp)
                {
                    _analyser.RingBuffer.Add(ref data);
                }
                else
                {
                    if (DateTime.UtcNow.Subtract(_startTime) > new TimeSpan(0, 0, 0, 0, WarmUpTime))
                        _warmedUp = true;
                }
            }
        }
        
        private static TeletextPage DecodedSubtitlePage;

        private static void Service_TeletextPageReady(object sender, EventArgs e)
        {
            var ttxE = (TeletextPageReadyEventArgs)e;

            if (ttxE == null) return;

            DecodedSubtitlePage = ttxE.Page;
        }

        private static void Service_TeletextPageCleared(object sender, EventArgs e)
        {
            var ttxE = (TeletextPageClearedEventArgs)e;

            if (DecodedSubtitlePage?.PageNum == ttxE.PageNumber)
                DecodedSubtitlePage = null;
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Console.CursorVisible = true;
            if (_pendingExit) return; //already trying to exit - allow normal behaviour on subsequent presses
            _pendingExit = true;
            _analyser.Cancel();
            e.Cancel = true;
        }
        
        private static void PrintToConsole(string message, params object[] arguments)
        {
            if (_options.SuppressOutput) return;

            ConsoleDisplay.AppendLine(string.Format(message, arguments));
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
        
    }
}

