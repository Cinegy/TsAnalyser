/*   Copyright 2016-2020 Cinegy GmbH

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

using Cinegy.Telemetry;
using Cinegy.TsAnalysis;
using Cinegy.TsDecoder.TransportStream;
using Cinegy.TtxDecoder.Teletext;
using CommandLine;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Cinegy.TsAnalyser
{
    // ReSharper disable once ClassNeverInstantiated.Global
    internal class Program
    {
        private const int WarmUpTime = 500;
        private const string LineBreak = "---------------------------------------------------------------------";
        private static bool _receiving;
        private static Options _options;
        private static bool _warmedUp;
        private static  Logger _logger;
        private static Analyser _analyser;
        private static readonly DateTime StartTime = DateTime.UtcNow;
        private static bool _pendingExit;
        private static readonly UdpClient UdpClient = new UdpClient();
        private static readonly List<string> ConsoleLines = new List<string>(1024);
        private static string TeletextLockString = string.Empty;
        
        static int Main(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return RunStreamInteractive();
            }

            if (args.Length == 1 && File.Exists(args[0]))
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

            newOpts.UdpPort = int.Parse(port);

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

            _logger = LogManager.GetCurrentClassLogger();

            var buildVersion = Assembly.GetEntryAssembly()?.GetName().Version.ToString();
            
            LogSetup.ConfigureLogger("tsanalyser", opts.OrganizationId, opts.DescriptorTags, "https://telemetry.cinegy.com", opts.TelemetryEnabled, false, "TSAnalyser", buildVersion );

            _analyser = new Analyser(_logger);

            var location = Assembly.GetEntryAssembly()?.Location;
            
            _logger.Info($"Cinegy Transport Stream Monitoring and Analysis Tool (Built: {File.GetCreationTime(location)})");

            try
            {
                Console.CursorVisible = false;
                Console.SetWindowSize(120, 50);
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

            LogMessage($"Logging started {Assembly.GetEntryAssembly()?.GetName().Version}.");

            _analyser.InspectTeletext = _options.DecodeTeletext;
            _analyser.InspectTsPackets = !_options.SkipDecodeTransportStream;
            _analyser.SelectedProgramNumber = _options.ProgramNumber;
            _analyser.VerboseLogging = _options.VerboseLogging;

            var filePath = (_options as ReadOptions)?.FileInput;

            if (!string.IsNullOrEmpty(filePath))
            {
                _analyser.Setup();
                _analyser.TsDecoder.TableChangeDetected += TsDecoder_TableChangeDetected;

                if (_analyser.InspectTeletext)
                {
                    _analyser.TeletextDecoder.Service.TeletextPageReady += Service_TeletextPageReady;
                    _analyser.TeletextDecoder.Service.TeletextPageCleared += Service_TeletextPageCleared;
                }

                StartStreamingFile(filePath);
            }

            if (_options is StreamOptions streamOptions)
            {
                _analyser.HasRtpHeaders = !streamOptions.NoRtpHeaders;
                _analyser.Setup(streamOptions.MulticastAddress, streamOptions.UdpPort);
                _analyser.TsDecoder.TableChangeDetected += TsDecoder_TableChangeDetected;

                if (_analyser.InspectTeletext)
                {
                    _analyser.TeletextDecoder.Service.TeletextPageReady += Service_TeletextPageReady;
                    _analyser.TeletextDecoder.Service.TeletextPageCleared += Service_TeletextPageCleared;
                }

                StartListeningToNetwork(streamOptions.MulticastAddress, streamOptions.UdpPort, streamOptions.AdapterAddress);
            }

            Console.Clear();

            while (!_pendingExit)
            {
                if (!_options.SuppressOutput)
                {
                    PrintConsoleFeedback();
                }

                Thread.Sleep(30);
            }

            LogMessage("Logging stopped.");
        }

        private static void TsDecoder_TableChangeDetected(object sender, TableChangedEventArgs args)
        {
            //Todo: finish implementing better EIT support
            //if (args.TableType != TableType.Eit) return;
            //if (sender is TsDecoder.TransportStream.TsDecoder decoder) Debug.WriteLine("EIT Version Num: " + decoder.EventInformationTable.VersionNumber);
        }

        private static void PrintConsoleFeedback()
        {
            var runningTime = DateTime.UtcNow.Subtract(StartTime);
            
            if (_options is StreamOptions)
            {
                //PrintToConsole("URL: {0}://{1}:{2}\tRunning time: {3:hh\\:mm\\:ss}",
                //    ((StreamOptions)_options).NoRtpHeaders ? "udp" : "rtp",
                //    string.IsNullOrWhiteSpace(((StreamOptions)_options).MulticastAddress) ?
                //        "127.0.0.1" : $"@{((StreamOptions)_options).MulticastAddress}",
                //    ((StreamOptions)_options).UdpPort, runningTime);

                var networkMetric = _analyser.NetworkMetric;
                var rtpMetric = _analyser.RtpMetric;
                
                //PrintClearLineToConsole();
                PrintToConsole("Network Details - {0}://{1}:{2}\t\tRunning: {3:hh\\:mm\\:ss}", ((StreamOptions)_options).NoRtpHeaders ? "udp" : "rtp",
                    string.IsNullOrWhiteSpace(((StreamOptions)_options).MulticastAddress) ?
                        "127.0.0.1" : $"@{((StreamOptions)_options).MulticastAddress}",
                    ((StreamOptions)_options).UdpPort, runningTime);

                PrintToConsole(LineBreak);

                PrintToConsole(
                    "Total Packets Rcvd: {0} \tBuffer Usage: {1:0.00}%/(Peak: {2:0.00}%)",
                    networkMetric.TotalPackets, networkMetric.NetworkBufferUsage, networkMetric.PeriodMaxNetworkBufferUsage);

                PrintToConsole(
                    "Total Data (MB): {0}\t\tPackets per sec:{1}",
                    networkMetric.TotalData / 1048576,
                    networkMetric.PacketsPerSecond);

                PrintToConsole("Period Max Packet Jitter (ms): {0}",
                    networkMetric.PeriodLongestTimeBetweenPackets);

                PrintToConsole(
                    "Bitrates (Mbps): {0:0.00}/{1:0.00}/{2:0.00}/{3:0.00} (Current/Avg/Peak/Low)",
                    networkMetric.CurrentBitrate / 1048576.0, networkMetric.AverageBitrate / 1048576.0,
                    networkMetric.HighestBitrate / 1048576.0, networkMetric.LowestBitrate / 1048576.0);

                if (!((StreamOptions)_options).NoRtpHeaders)
                {
                    PrintClearLineToConsole();
                    PrintToConsole($"RTP Details - SSRC: {rtpMetric.Ssrc}");
                    PrintToConsole(LineBreak);
                    PrintToConsole(
                        "Seq Num: {0}\tTimestamp: {1}\tMin Lost Pkts: {2}",
                        rtpMetric.LastSequenceNumber, rtpMetric.LastTimestamp, rtpMetric.EstimatedLostPackets);
                }
            }

            var pidMetrics = _analyser.PidMetrics;

            lock (pidMetrics)
            {
                var span = new TimeSpan((long)(_analyser.LastPcr / 2.7));
                
                PrintToConsole($"PCR Value: {span}");
                //PrintToConsole($"RAW PCR / PTS: {_analyser.LastPcr } / {_analyser.LastVidPts * 8} / {_analyser.LastSubPts * 8}");
                PrintClearLineToConsole();

                PrintToConsole(pidMetrics.Count < 10
                    ? $"PID Details - Unique PIDs: {pidMetrics.Count}"
                    : $"PID Details - Unique PIDs: {pidMetrics.Count}, (10 shown by packet count)");
                PrintToConsole(LineBreak);

                foreach (var pidMetric in pidMetrics.OrderByDescending(m => m.PacketCount).Take(10))
                {
                    PrintToConsole("TS PID: {0}\tPacket Count: {1} \t\tCC Error Count: {2}", pidMetric.Pid,
                        pidMetric.PacketCount, pidMetric.CcErrorCount);
                }
            }

            var tsDecoder = _analyser.TsDecoder;

            if (tsDecoder != null)
            {                
                lock (tsDecoder)
                {
                    var pmts = tsDecoder.ProgramMapTables.OrderBy(p => p.ProgramNumber).ToList();

                    PrintClearLineToConsole();

                    PrintToConsole(pmts.Count < 5
                        ? $"Service Information - Service Count: {pmts.Count}"
                        : $"Service Information - Service Count: {pmts.Count}, (5 shown)");

                    PrintToConsole(LineBreak);

                    foreach (var pmtable in pmts.Take(5))
                    {
                        var desc = tsDecoder.GetServiceDescriptorForProgramNumber(pmtable?.ProgramNumber);
                        if (desc != null)
                        {
                            PrintToConsole(
                                $"Service {pmtable?.ProgramNumber}: {desc.ServiceName.Value} ({desc.ServiceProviderName.Value}) - {desc.ServiceTypeDescription}"
                                );
                        }
                    }

                    var pmt = tsDecoder.GetSelectedPmt(_options.ProgramNumber);
                    if (pmt != null)
                    {
                        _options.ProgramNumber = pmt.ProgramNumber;
                        _analyser.SelectedPcrPid = pmt.PcrPid;
                    }

                    var serviceDesc = tsDecoder.GetServiceDescriptorForProgramNumber(pmt?.ProgramNumber);

                    PrintClearLineToConsole();

                    PrintToConsole(serviceDesc != null
                        ? $"Elements - Selected Program: {serviceDesc.ServiceName} (ID:{pmt?.ProgramNumber}) (first 5 shown)"
                        : $"Elements - Selected Program Service ID {pmt?.ProgramNumber} (first 5 shown)");
                    PrintToConsole(LineBreak);

                    if (pmt?.EsStreams != null)
                    {
                        foreach (var stream in pmt.EsStreams.Take(5))
                        {
                            if (stream == null) continue;
                            if (stream.StreamType != 6)
                            {
                                PrintToConsole(
                                    "PID: {0} ({1})", stream.ElementaryPid,
                                    DescriptorDictionaries.ShortElementaryStreamTypeDescriptions[
                                        stream.StreamType]);
                            }
                            else
                            {
                                if (stream.Descriptors.OfType<Ac3Descriptor>().Any())
                                {
                                    PrintToConsole("PID: {0} ({1})", stream.ElementaryPid, "AC-3 / Dolby Digital");
                                    continue;
                                }
                                if (stream.Descriptors.OfType<Eac3Descriptor>().Any())
                                {
                                    PrintToConsole("PID: {0} ({1})", stream.ElementaryPid, "EAC-3 / Dolby Digital Plus");
                                    continue;
                                }
                                if (stream.Descriptors.OfType<SubtitlingDescriptor>().Any())
                                {
                                    PrintToConsole("PID: {0} ({1})", stream.ElementaryPid, "DVB Subtitles");
                                    continue;
                                }
                                if (stream.Descriptors.OfType<TeletextDescriptor>().Any())
                                {
                                    PrintToConsole("PID: {0} ({1})", stream.ElementaryPid, "Teletext");
                                    continue;
                                }
                                if (stream.Descriptors.OfType<RegistrationDescriptor>().Any())
                                {
                                    if (stream.Descriptors.OfType<RegistrationDescriptor>().First().Organization == "2LND")
                                    {
                                        PrintToConsole("PID: {0} ({1})", stream.ElementaryPid, "Cinegy DANIEL2");
                                        continue;
                                    }
                                }
                                    
                                PrintToConsole(
                                    "PID: {0} ({1})", stream.ElementaryPid,
                                    DescriptorDictionaries.ShortElementaryStreamTypeDescriptions[
                                        stream.StreamType]);

                            }

                        }
                    }
                }

                if (_options.DecodeTeletext)
                {
                    PrintTeletext();
                }
            }
            
            Console.CursorVisible = false;
            Console.SetCursorPosition(0, 0);

            foreach (var consoleLine in ConsoleLines)
            {
                ClearCurrentConsoleLine();
                Console.WriteLine(consoleLine);
            }

            Console.CursorVisible = true;

            ConsoleLines.Clear();

        }

        private static void PrintTeletext()
        {
            //some strangeness here to get around the fact we just append to console, to clear out
            //a fixed 4 lines of space for TTX render
            const string clearLine = "\t\t\t\t\t\t\t\t\t";
            var ttxRender = new [] { clearLine, clearLine, clearLine, clearLine };

            if (_decodedSubtitlePage == null) return;

            lock (_decodedSubtitlePage)
            {
                if (string.IsNullOrEmpty(TeletextLockString))
                {
                    var defaultLang = _decodedSubtitlePage.ParentMagazine.ParentService.AssociatedDescriptor
                        .Languages
                        .FirstOrDefault();

                    if (defaultLang != null)
                        TeletextLockString =
                            $"Teletext {_decodedSubtitlePage.ParentMagazine.MagazineNum}{_decodedSubtitlePage.PageNum:x00} ({defaultLang.Iso639LanguageCode}) - decoding Service ID {_decodedSubtitlePage.ParentMagazine.ParentService.ProgramNumber}, PID: {_decodedSubtitlePage.ParentMagazine.ParentService.TeletextPid}";
                }

                PrintClearLineToConsole();
                   
                PrintToConsole($"{TeletextLockString}, PTS: {_decodedSubtitlePage.Pts}");
                    
                PrintToConsole(LineBreak);
                    
                PrintToConsole(
                    $"Packets (Period/Total): {_analyser.TeletextMetric.PeriodTtxPacketCount}/{_analyser.TeletextMetric.TtxPacketCount}, Total Pages: {_analyser.TeletextMetric.TtxPageReadyCount}, Total Clears: {_analyser.TeletextMetric.TtxPageClearCount}");

                PrintClearLineToConsole();

                var i = 0;

                foreach (var row in _decodedSubtitlePage.Rows)
                {
                    if (i>3  || string.IsNullOrWhiteSpace(row.GetPlainRow())) continue;
                    ttxRender[i] = $"{row.RowNum} - {row.GetPlainRow()}";
                    i++;
                }
            }

            foreach (var val in ttxRender)
            {
                PrintToConsole(val);
            }

        }
        
        private static void StartListeningToNetwork(string multicastAddress, int networkPort,
            string listenAdapter = "")
        {

            var listenAddress = string.IsNullOrEmpty(listenAdapter) ? IPAddress.Any : IPAddress.Parse(listenAdapter);

            var localEp = new IPEndPoint(listenAddress, networkPort);

            UdpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            UdpClient.Client.ReceiveBufferSize = 1500 * 3000;
            UdpClient.Client.Bind(localEp);
            _analyser.NetworkMetric.UdpClient = UdpClient;

            if (!string.IsNullOrWhiteSpace(multicastAddress))
            {
                var parsedMcastAddr = IPAddress.Parse(multicastAddress);
                UdpClient.JoinMulticastGroup(parsedMcastAddr, listenAddress);
            }

            var ts = new ThreadStart(delegate
            {
                ReceivingNetworkWorkerThread(UdpClient);
            });

            var receiverThread = new Thread(ts) {Priority = ThreadPriority.Highest};

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
            var ep = client.Client.LocalEndPoint as IPEndPoint;

            while (_receiving && !_pendingExit)
            {
                var data = client.Receive(ref ep);

                if (_warmedUp)
                {
                    _analyser.RingBuffer.Add(ref data);
                }
                else
                {
                    if (DateTime.UtcNow.Subtract(StartTime) > new TimeSpan(0, 0, 0, 0, WarmUpTime))
                        _warmedUp = true;
                }
            }
        }
        
        private static TeletextPage _decodedSubtitlePage;

        private static void Service_TeletextPageReady(object sender, EventArgs e)
        {
            var ttxE = (TeletextPageReadyEventArgs)e;

            if (ttxE == null) return;

            _decodedSubtitlePage = ttxE.Page;
        }

        private static void Service_TeletextPageCleared(object sender, EventArgs e)
        {
            var ttxE = (TeletextPageClearedEventArgs)e;

            if (_decodedSubtitlePage?.PageNum == ttxE.PageNumber)
                _decodedSubtitlePage = null;
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Console.CursorVisible = true;
            if (_pendingExit) return; //already trying to exit - allow normal behaviour on subsequent presses
            _pendingExit = true;
            _analyser.Cancel();
            e.Cancel = true;
        }

        private static void PrintClearLineToConsole()
        {
            if (_options.SuppressOutput) return;
            ConsoleLines.Add("\t"); //use a tab for a clear line, to ensure that an operation runs
        }

        private static void PrintToConsole(string message, params object[] arguments)
        {
            if (_options.SuppressOutput) return;
            ConsoleLines.Add(string.Format(message, arguments));
        }

        private static void ClearCurrentConsoleLine()
        {
            // Write space to end of line, and then CR with no LF
            Console.Write("\r".PadLeft(Console.WindowWidth - Console.CursorLeft - 1));
        }

        private static void LogMessage(string message)
        {
            var lei = new TelemetryLogEventInfo
            {
                Level = LogLevel.Info,
                Key = "GenericEvent",
                Message = message
            };

            _logger.Log(lei);
        }
        
    }
}

