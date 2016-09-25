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
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Web;
using System.Text;
using System.Threading;
using CommandLine;
using TsAnalyser.Tables;
using TsAnalyser.Teletext;
using static System.String;

namespace TsAnalyser
{
    internal class Program
    {
        private static bool _receiving;
        private static bool _suppressConsoleOutput;
        private static bool _readServiceDescriptions;
        private static bool _noRtpHeaders;
        private static DateTime _startTime = DateTime.UtcNow;
        private static string _logFile;
        private static bool _pendingExit;
        private static ServiceHost _serviceHost;
        private static TsAnalyserApi _tsAnalyserApi;
        private static readonly UdpClient UdpClient = new UdpClient { ExclusiveAddressUse = false };
        private static readonly object LogfileWriteLock = new object();
        private static StreamWriter _logFileStream;

        private static NetworkMetric _networkMetric = new NetworkMetric();
        private static RtpMetric _rtpMetric = new RtpMetric();
        private static List<TsMetrics> _tsMetrics = new List<TsMetrics>();
        private static ProgAssociationTable _progAssociationTable;
        private static Tables.ProgramMapTable _programMapTable;
        private static Tables.ServiceDescriptionTable _serviceDescriptionTable;
        private static readonly object ServiceDescriptionTableLock = new object();
        private static readonly List<ServiceDescriptor> ServiceDescriptors = new List<ServiceDescriptor>(16);

        private static readonly Dictionary<short, Dictionary<ushort, TeleText>> TeletextSubtitlePages = new Dictionary<short, Dictionary<ushort, TeleText>>();
        private static readonly Dictionary<short, Pes> TeletextSubtitleBuffers = new Dictionary<short, Pes>();
        private static readonly object TeletextSubtitleDecodedPagesLock = new object();
        private static readonly Dictionary<short, Dictionary<ushort, string[]>> TeletextDecodedSubtitlePages = new Dictionary<short, Dictionary<ushort, string[]>>();
        
        private static readonly StringBuilder ConsoleDisplay = new StringBuilder(1024);

        static void Main(string[] args)
        {
            var options = new Options();

            Console.CancelKeyPress += Console_CancelKeyPress;

            Console.WriteLine(
                $"Cinegy Simple RTP monitoring tool v1.0.0 ({File.GetCreationTime(Assembly.GetExecutingAssembly().Location)})\n");

            try
            {
                Console.CursorVisible = false;
                Console.SetWindowSize(120, 60);
            }
            catch
            {
                Console.WriteLine("Failed to increase console size - probably screen resolution is low");
            }

            if (!Parser.Default.ParseArguments(args, options))
            {
                //ask the user interactively for an address and group
                Console.WriteLine(
                    "\nSince parameters were not passed at the start, you can now enter the two most important (or just hit enter to quit)");
                Console.Write("\nPlease enter the multicast address to listen to (e.g. 239.1.1.1): ");
                var address = Console.ReadLine();

                if (IsNullOrWhiteSpace(address)) return;

                options.MulticastAddress = address;

                Console.Write("Please enter the multicast group port (e.g. 1234): ");
                var port = Console.ReadLine();
                if (IsNullOrWhiteSpace(port))
                {
                    Console.WriteLine("Not a valid group port number - press enter to exit.");
                    Console.ReadLine();
                    return;
                }
                options.MulticastGroup = int.Parse(port);

            }

            WorkLoop(options);
        }

        private static void WorkLoop(Options options)
        {
            Console.Clear();

            if (!_receiving)
            {
                _receiving = true;
                _logFile = options.LogFile;
                _suppressConsoleOutput = options.SuppressOutput;
                _readServiceDescriptions = options.ReadServiceDescriptions;
                _noRtpHeaders = options.NoRtpHeaders;
                

                if (!IsNullOrWhiteSpace(_logFile))
                {
                    PrintToConsole("Logging events to file {0}", _logFile);
                }
                LogMessage("Logging started.");

                if (options.EnableWebServices)
                {
                    var httpThreadStart = new ThreadStart(delegate
                    {
                        StartHttpService(options.ServiceUrl);
                    });

                    var httpThread = new Thread(httpThreadStart) { Priority = ThreadPriority.Normal };

                    httpThread.Start();
                }

                SetupMetrics();
                StartListeningToNetwork(options.MulticastAddress, options.MulticastGroup, options.AdapterAddress);
            }

            Console.Clear();

            while (!_pendingExit)
            {
                var runningTime = DateTime.UtcNow.Subtract(_startTime);

                //causes occasional total refresh to erase glitches that build up
                if (runningTime.Milliseconds < 20)
                {
                   // Console.Clear();
                }

                if (!_suppressConsoleOutput)

                {
                    Console.SetCursorPosition(0, 0);

                    PrintToConsole("URL: rtp://@{0}:{1}\tRunning time: {2:hh\\:mm\\:ss}\t\t\n", options.MulticastAddress,
                        options.MulticastGroup, runningTime);
                    PrintToConsole(
                        "Network Details\n----------------\nTotal Packets Rcvd: {0} \tBuffer Usage: {1:0.00}%\t\t\nTotal Data (MB): {2}\t\tPackets per sec:{3}",
                        _networkMetric.TotalPackets, _networkMetric.NetworkBufferUsage, _networkMetric.TotalData / 1048576,
                        _networkMetric.PacketsPerSecond);
                    PrintToConsole("Time Between Packets (ms): {0} \tShortest/Longest: {1}/{2}",
                        _networkMetric.TimeBetweenLastPacket, _networkMetric.ShortestTimeBetweenPackets,
                        _networkMetric.LongestTimeBetweenPackets);
                    PrintToConsole("Bitrates (Mbps): {0:0.00}/{1:0.00}/{2:0.00}/{3:0.00} (Current/Avg/Peak/Low)\t\t\t",
                        (_networkMetric.CurrentBitrate / 131072.0), _networkMetric.AverageBitrate / 131072.0,
                        (_networkMetric.HighestBitrate / 131072.0), (_networkMetric.LowestBitrate / 131072.0));

                    if (!_noRtpHeaders)
                    { 
                        PrintToConsole(
                            "\nRTP Details\n----------------\nSeq Num: {0}\tMin Lost Pkts: {1}\nTimestamp: {2}\tSSRC: {3}\t",
                            _rtpMetric.LastSequenceNumber, _rtpMetric.MinLostPackets, _rtpMetric.LastTimestamp, _rtpMetric.Ssrc);
                    }
                    
                    PrintToConsole("\nTS Details\n----------------");
                    lock (_tsMetrics)
                    {
                        var patMetric = _tsMetrics.FirstOrDefault(m => m.Pid == 0x0);
                        if (patMetric?.ProgAssociationTable.ProgramNumbers != null)
                        {
                            PrintToConsole("Unique PID count: {0}\t\tProgram Count: {1}\t\t\t\n(Showing up to 10 PID streams in table by packet count)\t\t\t", _tsMetrics.Count,
                                patMetric.ProgAssociationTable.ProgramNumbers.Length);
                        }

                        foreach (var tsMetric in _tsMetrics.OrderByDescending(m => m.PacketCount).Take(10))
                        {
                            PrintToConsole("TS PID: 0x{0:X0}\tPacket Count: {1} \t\tCC Error Count: {2}\t", tsMetric.Pid,
                                tsMetric.PacketCount, tsMetric.CcErrorCount);
                        }
                    }

                    if (null != _serviceDescriptionTable && _readServiceDescriptions)
                    {
                        lock (ServiceDescriptionTableLock)
                        {
                            PrintToConsole(
                                "\t\t\t\nService Information\n----------------\t\t\t\t");

                            foreach (var descriptor in ServiceDescriptors)
                            {
                                PrintToConsole(
                                    "Service: {0} ({1}) - {2}\t\t\t",
                                    descriptor.ServiceName.Value, descriptor.ServiceProviderName.Value, descriptor.ServiceTypeDescription);

                            }
                        }
                        lock (TeletextSubtitleDecodedPagesLock)
                        {
                            PrintToConsole("\nTeleText Subtitles\n----------------");
                            foreach (var pid in TeletextDecodedSubtitlePages.Keys)
                            {
                                foreach (var page in TeletextDecodedSubtitlePages[pid].Keys)
                                {
                                    PrintToConsole("Live Decoding Page {0:X} from Pid {1}\n", page, pid);

                                    //some strangeness here to get around the fact we just append to console, to clear out
                                    //a fixed 4 lines of space for TTX render
                                    const string clearLine = "\t\t\t\t\t\t\t\t\t";
                                    var ttxRender = new[] { clearLine, clearLine, clearLine, clearLine };

                                    var i = 0;

                                    foreach (var line in TeletextDecodedSubtitlePages[pid][page])
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
                    }

                }

                
                Console.WriteLine(ConsoleDisplay.ToString());
                ConsoleDisplay.Clear();

                Thread.Sleep(20);
            }

            LogMessage("Logging stopped.");
        }

        private static void StartListeningToNetwork(string multicastAddress, int multicastGroup,
            string listenAdapter = "")
        {

            var listenAddress = IsNullOrEmpty(listenAdapter) ? IPAddress.Any : IPAddress.Parse(listenAdapter);

            var localEp = new IPEndPoint(listenAddress, multicastGroup);

            UdpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            UdpClient.Client.ReceiveBufferSize = 1024*256;
            UdpClient.ExclusiveAddressUse = false;
            UdpClient.Client.Bind(localEp);
            _networkMetric.UdpClient = UdpClient;

            var parsedMcastAddr = IPAddress.Parse(multicastAddress);
            UdpClient.JoinMulticastGroup(parsedMcastAddr, listenAddress);

            var ts = new ThreadStart(delegate
            {
                ReceivingNetworkWorkerThread(UdpClient, localEp);
            });

            var receiverThread = new Thread(ts) {Priority = ThreadPriority.Highest};

            receiverThread.Start();
        }



        private static void ReceivingNetworkWorkerThread(UdpClient client, IPEndPoint localEp)
        {
            while (_receiving)
            {
                var data = client.Receive(ref localEp);
                if (data == null) continue;
                try
                {
                    _networkMetric.AddPacket(data);

                    if (!_noRtpHeaders)
                    {
                        _rtpMetric.AddPacket(data);
                    }

                    //TS packet metrics
                    var tsPackets = TsPacketFactory.GetTsPacketsFromData(data);

                    if (tsPackets == null)
                    {
                        break;
                    }

                    lock (_tsMetrics)
                    {
                        foreach (var tsPacket in tsPackets)
                        {
                            var currentMetric = _tsMetrics.FirstOrDefault(tsMetric => tsMetric.Pid == tsPacket.Pid);
                            if (currentMetric == null)
                            {
                                currentMetric = new TsMetrics { Pid = tsPacket.Pid };
                                currentMetric.DiscontinuityDetected += currentMetric_DiscontinuityDetected;
                                currentMetric.TransportErrorIndicatorDetected += currentMetric_TransportErrorIndicatorDetected;
                                _tsMetrics.Add(currentMetric);
                            }
                            currentMetric.AddPacket(tsPacket);

                            if (currentMetric.Pid == 0x0)
                            {
                                _progAssociationTable = currentMetric.ProgAssociationTable;
                            }

                            if (!_readServiceDescriptions) continue;

                            if (_progAssociationTable != null && tsPacket.Pid == _progAssociationTable.PmtPid)
                            {
                                if (tsPacket.PayloadUnitStartIndicator)
                                {
                                    _programMapTable = new Tables.ProgramMapTable(tsPacket);
                                }
                                else
                                {
                                    if (_programMapTable != null && !_programMapTable.HasAllBytes())
                                    {
                                        _programMapTable?.Add(tsPacket);
                                    }
                                }

                                if (_programMapTable != null && _programMapTable.HasAllBytes())
                                {
                                    if (_programMapTable.ProcessTable())
                                    {
                                        if (_tsAnalyserApi != null && _tsAnalyserApi.ProgramMetrics == null) _tsAnalyserApi.ProgramMetrics = _programMapTable;

                                        foreach (var esStream in _programMapTable.EsStreams)
                                        {
                                            foreach (var descriptor in esStream.Descriptors.Where(d => d.DescriptorTag == 0x56))
                                            {
                                                var teletext = descriptor as TeletextDescriptor;
                                                if (null == teletext) continue;

                                                foreach (var lang in teletext.Languages)
                                                {
                                                    if (lang.TeletextType != 0x02 && lang.TeletextType != 0x05)
                                                        continue;

                                                    if (!TeletextSubtitlePages.ContainsKey(esStream.ElementaryPid))
                                                    {
                                                        TeletextSubtitlePages.Add(esStream.ElementaryPid, new Dictionary<ushort, TeleText>());
                                                        TeletextSubtitleBuffers.Add(esStream.ElementaryPid, null);
                                                    }
                                                    var m = lang.TeletextMagazineNumber;
                                                    if (lang.TeletextMagazineNumber == 0)
                                                    {
                                                        m = 8;
                                                    }
                                                    var page = (ushort)((m << 8) + lang.TeletextPageNumber);
                                                    //var pageStr = $"{page:X}";

                                                    // if (page == 0x199)
                                                    {
                                                        if (
                                                            TeletextSubtitlePages[esStream.ElementaryPid]
                                                                .ContainsKey(page)) continue;

                                                        TeletextSubtitlePages[esStream.ElementaryPid].Add(page, new TeleText(page, esStream.ElementaryPid));
                                                        TeletextSubtitlePages[esStream.ElementaryPid][page].TeletextPageRecieved += TeletextPageRecievedMethod;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        _programMapTable = null;
                                    }
                                }

                                /* _programMapTable = Tables.ProgramMapTableFactory.ProgramMapTableFromTsPackets(new[] { tsPacket });
                                     if (_tsAnalyserApi != null) _tsAnalyserApi.ProgramMetrics = _programMapTable;*/
                            }

                            if (tsPacket.Pid == 0x0011)
                            {
                                lock (ServiceDescriptionTableLock)
                                {
                                    if (tsPacket.PayloadUnitStartIndicator)
                                    {
                                        _serviceDescriptionTable = new Tables.ServiceDescriptionTable(tsPacket);
                                    }
                                    else
                                    {
                                        if (_serviceDescriptionTable != null && !_serviceDescriptionTable.HasAllBytes())
                                        {
                                            _serviceDescriptionTable.Add(tsPacket);
                                        }
                                    }

                                    if (_serviceDescriptionTable != null && _serviceDescriptionTable.HasAllBytes())
                                    {
                                        if (_serviceDescriptionTable.ProcessTable())
                                        {
                                            if (_tsAnalyserApi != null && _tsAnalyserApi.ServiceMetrics == null) _tsAnalyserApi.ServiceMetrics = _serviceDescriptionTable;

                                            if (null != _serviceDescriptionTable && _readServiceDescriptions)
                                            {
                                                lock (ServiceDescriptionTableLock)
                                                {
                                                    if (_serviceDescriptionTable?.Items != null &&
                                                        _serviceDescriptionTable.TableId == 0x42)
                                                    {
                                                        foreach (var item in _serviceDescriptionTable.Items)
                                                        {
                                                            foreach (
                                                                var descriptor in
                                                                    item.Descriptors.Where(d => d.DescriptorTag == 0x48)
                                                                )
                                                            {
                                                                var sd = descriptor as ServiceDescriptor;

                                                                if (sd == null) continue;

                                                                var match = false;
                                                                foreach (var serviceDescriptor in ServiceDescriptors)
                                                                {
                                                                    if (serviceDescriptor.ServiceName.Value == sd.ServiceName.Value)
                                                                    {
                                                                        match = true;
                                                                    }
                                                                }

                                                                if (!match) ServiceDescriptors.Add(sd);
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        else
                                        {
                                            _serviceDescriptionTable = null;
                                        }
                                    }

                                    //_serviceDescriptionTable = Tables.ServiceDescriptionTableFactory.ServiceDescriptionTableFromTsPackets(new[] { tsPacket });

                                }
                            }
                            if (TeletextSubtitlePages?.ContainsKey(tsPacket.Pid) == false) continue;

                            if (tsPacket.PayloadUnitStartIndicator)
                            {
                                if (null != TeletextSubtitleBuffers[tsPacket.Pid])
                                {
                                    if (TeletextSubtitleBuffers[tsPacket.Pid].HasAllBytes())
                                    {
                                        TeletextSubtitleBuffers[tsPacket.Pid].Decode();
                                        foreach (var key in TeletextSubtitlePages[tsPacket.Pid].Keys)
                                        {
                                            TeletextSubtitlePages[tsPacket.Pid][key].DecodeTeletextData(TeletextSubtitleBuffers[tsPacket.Pid]);
                                        }
                                    }
                                }

                                TeletextSubtitleBuffers[tsPacket.Pid] = new Pes(tsPacket);
                            }
                            else if (TeletextSubtitleBuffers[tsPacket.Pid] != null/* && !TeletextSubtitleBuffers[packet.PID].HasAllBytes()*/)
                            {
                                TeletextSubtitleBuffers[tsPacket.Pid].Add(tsPacket);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($@"Unhandled exception within network receiver: {ex.Message}");
                }
            }
        }
        
        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            if (_pendingExit) return; //already trying to exit - allow normal behaviour on subsequent presses
            _pendingExit = true;
            e.Cancel = true;
        }

        private static void currentMetric_DiscontinuityDetected(object sender, TransportStreamEventArgs e)
        {
            LogMessage($"Discontinuity on TS PID {e.TsPid}");
        }

        private static void currentMetric_TransportErrorIndicatorDetected(object sender, TransportStreamEventArgs e)
        {
            LogMessage($"Transport Error Indicator on TS PID {e.TsPid}");
        }

        private static void RtpMetric_SequenceDiscontinuityDetected(object sender, EventArgs e)
        {
            LogMessage("Discontinuity in RTP sequence.");
        }

        private static void NetworkMetric_BufferOverflow(object sender, EventArgs e)
        {
            LogMessage("Network buffer > 99% - probably loss of data from overflow.");
        }
        
        private static void PrintToConsole(string message, params object[] arguments)
        {
            if (_suppressConsoleOutput) return;

            ConsoleDisplay.AppendLine(string.Format(message, arguments));

        //   Console.WriteLine(message, arguments);
        }
        
        private static void LogMessage(string message)
        {
            ThreadPool.QueueUserWorkItem(WriteToFile, message);
        }

        public static void WriteToFile(object msg)
        {
            lock (LogfileWriteLock)
            {
                try
                {
                    if (_logFileStream == null || _logFileStream.BaseStream.CanWrite != true)
                    {
                        if (IsNullOrWhiteSpace(_logFile)) return;

                        var fs = new FileStream(_logFile, FileMode.Append, FileAccess.Write);

                        _logFileStream = new StreamWriter(fs) {AutoFlush = true};
                    }

                    _logFileStream.WriteLine($"{DateTime.Now} - {msg}");
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
                TsMetrics = _tsMetrics,
                RtpMetric = _rtpMetric
            };

            _tsAnalyserApi.StreamCommand += _tsAnalyserApi_StreamCommand;
            
            _serviceHost = new ServiceHost(_tsAnalyserApi, baseAddress);
            var webBinding = new WebHttpBinding();

            var serviceEndpoint = new ServiceEndpoint(ContractDescription.GetContract(typeof (ITsAnalyserApi)))
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
            
            //Metadata Exchange
            //var serviceBehavior = new ServiceMetadataBehavior {HttpGetEnabled = true};
            //_serviceHost.Description.Behaviors.Add(serviceBehavior);

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

        private static void SetupMetrics()
        {
            lock (_tsMetrics)
            {
                _startTime = DateTime.UtcNow;
                _networkMetric = new NetworkMetric();
                _rtpMetric = new RtpMetric();
                _tsMetrics = new List<TsMetrics>();
                _rtpMetric.SequenceDiscontinuityDetected += RtpMetric_SequenceDiscontinuityDetected;
                _networkMetric.BufferOverflow += NetworkMetric_BufferOverflow;
                _networkMetric.UdpClient = UdpClient;
            }
        }

        private static void TeletextPageRecievedMethod(object sender, EventArgs args)
        {
            var teletextArgs = (TeleTextSubtitleEventArgs)args;
            lock(TeletextSubtitleDecodedPagesLock)
            {
                if (!TeletextDecodedSubtitlePages.ContainsKey(teletextArgs.Pid))
                {
                    TeletextDecodedSubtitlePages.Add(teletextArgs.Pid, new Dictionary<ushort, string[]>());
                }

                if (!TeletextDecodedSubtitlePages.ContainsKey(teletextArgs.Pid)) return;

                if (!TeletextDecodedSubtitlePages[teletextArgs.Pid].ContainsKey(teletextArgs.PageNumber))
                {
                    TeletextDecodedSubtitlePages[teletextArgs.Pid].Add(teletextArgs.PageNumber, new string[0]);
                }

                if (TeletextDecodedSubtitlePages[teletextArgs.Pid].ContainsKey(teletextArgs.PageNumber))
                {

                }

                TeletextDecodedSubtitlePages[teletextArgs.Pid][teletextArgs.PageNumber] = teletextArgs.Page;
            }
        }

        private static void _tsAnalyserApi_StreamCommand(object sender, StreamCommandEventArgs e)
        {
            switch (e.Command)
            {
                case (StreamCommandType.ResetMetrics):
                    SetupMetrics();

                    _tsAnalyserApi.NetworkMetric = _networkMetric;
                    _tsAnalyserApi.TsMetrics = _tsMetrics;
                    _tsAnalyserApi.RtpMetric = _rtpMetric;

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
    }
}

