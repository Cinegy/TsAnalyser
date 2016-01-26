using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using CommandLine;
using static System.String;

namespace TsAnalyser
{
    internal class Program
    {
        private static bool _receiving;
        private static readonly DateTime StartTime = DateTime.UtcNow;
        private static string _logFile;
        private static bool _pendingExit;

        private static readonly NetworkMetric NetworkMetric = new NetworkMetric();
        private static readonly RtpMetric RtpMetric = new RtpMetric();
        private static readonly List<TsMetrics> TsMetrics = new List<TsMetrics>();

        static void Main(string[] args)
        {
            var options = new Options();
            
            Console.CancelKeyPress += Console_CancelKeyPress;

            Console.WriteLine("Cinegy Simple RTP monitoring tool v1.0.0 ({0})\n", File.GetCreationTime(Assembly.GetExecutingAssembly().Location));

            if (!Parser.Default.ParseArguments(args, options))
            {
                //ask the user interactively for an address and group
                Console.WriteLine("\nSince parameters were not passed at the start, you can now enter the two most important (or just hit enter to quit)");
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
                if (!IsNullOrWhiteSpace(_logFile))
                {
                    Console.WriteLine("Logging events to file {0}", _logFile);
                }
                LogMessage("Logging started.");
                StartListeningToNetwork(options.MulticastAddress, options.MulticastGroup, options.AdapterAddress);
                RtpMetric.SequenceDiscontinuityDetected += RtpMetric_SequenceDiscontinuityDetected;
                NetworkMetric.BufferOverflow +=NetworkMetric_BufferOverflow;
            }

            Console.Clear();
            
            while (!_pendingExit)
            {
                var runningTime = DateTime.UtcNow.Subtract(StartTime);
               
                Console.SetCursorPosition(0, 0);
                Console.WriteLine("URL: rtp://@{0}:{1}\tRunning time: {2:hh\\:mm\\:ss}\t\t\n", options.MulticastAddress, options.MulticastGroup,runningTime);
                Console.WriteLine("Network Details\n----------------\nTotal Packets Rcvd: {0} \tBuffer Usage: {1:0.00}%\t\t\nTotal Data (MB): {2}\t\tPackets per sec:{3}",
                    NetworkMetric.TotalPackets, NetworkMetric.NetworkBufferUsage, NetworkMetric.TotalData / 1048576, NetworkMetric.PacketsPerSecond);
                Console.WriteLine("Time Between Packets (ms): {0} \tShortest/Longest: {1}/{2}",NetworkMetric.TimeBetweenLastPacket,NetworkMetric.ShortestTimeBetweenPackets,NetworkMetric.LongestTimeBetweenPackets);
                Console.WriteLine("Bitrates (Mbps): {0:0.00}/{1:0.00}/{2:0.00}/{3:0.00} (Current/Avg/Peak/Low)\t\t\t", (NetworkMetric.CurrentBitrate / 131072.0), NetworkMetric.AverageBitrate / 131072.0, (NetworkMetric.HighestBitrate / 131072.0), (NetworkMetric.LowestBitrate / 131072.0));
                Console.WriteLine("\nRTP Details\n----------------\nSeq Num: {0}\tMin Lost Pkts: {1}\nTimestamp: {2}\tSSRC: {3}\t", RtpMetric.LastSequenceNumber, RtpMetric.MinLostPackets,RtpMetric.LastTimestamp ,RtpMetric.Ssrc);
                Console.WriteLine("\nTS Details\n----------------");
                lock (TsMetrics)
                {
                    var patMetric = TsMetrics.FirstOrDefault(m => m.IsProgAssociationTable);
                    if (patMetric != null && patMetric.ProgAssociationTable.ProgramNumbers != null)
                    {
                        Console.WriteLine("Unique PID count: {0}\t\tProgram Count: {1}\t\t\t",TsMetrics.Count, patMetric.ProgAssociationTable.ProgramNumbers.Length);
                    }

                    foreach (var tsMetric in TsMetrics.OrderByDescending(m => m.Pid))
                    {
                        Console.WriteLine("TS PID: {0}\tPacket Count: {1} \t\tCC Error Count: {2}\t", tsMetric.Pid,
                            tsMetric.PacketCount, tsMetric.CcErrorCount);  
                    }
                }
                Thread.Sleep(20);
            }

            LogMessage("Logging stopped.");
        }


        private static void StartListeningToNetwork(string multicastAddress, int multicastGroup, string listenAdapter = "")
        {
            var client = new UdpClient {ExclusiveAddressUse = false};

            var listenAddress = IsNullOrEmpty(listenAdapter) ? IPAddress.Any : IPAddress.Parse(listenAdapter);

            var localEp = new IPEndPoint(listenAddress, multicastGroup);

            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            client.Client.ReceiveBufferSize = 1024*256;
            client.ExclusiveAddressUse = false;
            client.Client.Bind(localEp);
            NetworkMetric.UdpClient = client;

            var parsedMcastAddr = IPAddress.Parse(multicastAddress);
            client.JoinMulticastGroup(parsedMcastAddr);

            var ts = new ThreadStart(delegate
            {
                ReceivingNetworkWorkerThread(client, localEp);
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
                    NetworkMetric.AddPacket(data);
                    RtpMetric.AddPacket(data);

                    //TS packet metrics
                    var tsPackets = TsPacketFactory.GetTsPacketsFromData(data);

                    lock (TsMetrics)
                    {
                        foreach (var tsPacket in tsPackets)
                        {
                            var currentMetric = TsMetrics.FirstOrDefault(tsMetric => tsMetric.Pid == tsPacket.Pid);
                            if (currentMetric == null)
                            {
                                currentMetric = new TsMetrics {Pid = tsPacket.Pid};
                                currentMetric.DiscontinuityDetected += currentMetric_DiscontinuityDetected;
                                TsMetrics.Add(currentMetric);
                            }
                            currentMetric.AddPacket(tsPacket);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($@"Unhandled exception withing network receiver: {ex.Message}");
                }
            }
        }

        static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            if (_pendingExit) return; //already trying to exit - allow normal behaviour on subsequent presses
            _pendingExit = true;
            e.Cancel = true;
        }

        static void currentMetric_DiscontinuityDetected(object sender, DiscontinutityEventArgs e)
        {
            LogMessage($"Discontinuity on TS PID {e.TsPid}");
        }

        static void RtpMetric_SequenceDiscontinuityDetected(object sender, EventArgs e)
        {
            LogMessage("Discontinuity in RTP sequence.");
        }

        static void NetworkMetric_BufferOverflow(object sender, EventArgs e)
        {
            LogMessage("Network buffer > 99% - probably loss of data from overflow.");
        }

        private static void LogMessage(string message)
        {
            try
            {
                if (IsNullOrWhiteSpace(_logFile)) return;

                var fs = new FileStream(_logFile, FileMode.Append, FileAccess.Write);
                var sw = new StreamWriter(fs);

                sw.WriteLine("{0} - {1}", DateTime.Now, message);

                sw.Close();
                fs.Close();
                sw.Dispose();
                fs.Dispose();
            }
            catch (Exception)
            {
                Debug.WriteLine("Concurrency error writing to log file...");
            }

        }
    }

   
}
