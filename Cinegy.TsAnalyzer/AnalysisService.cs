/* Copyright 2022-2023 Cinegy GmbH.

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

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Sockets;
using System.Runtime;
using Cinegy.Srt.Wrapper;
using Cinegy.TsAnalysis;
using Cinegy.TsDecoder.Descriptors;
using Cinegy.TsDecoder.TransportStream;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SrtSharp;
using TsAnalyzer.SerializableModels.Settings;
using LogLevel = SrtSharp.LogLevel;

namespace Cinegy.TsAnalyzer;

public class AnalysisService : IHost, IHostedService
{
    private readonly ILogger _logger;
    private readonly AppConfig _appConfig;
    private readonly IHostApplicationLifetime _appLifetime;
    private CancellationToken _cancellationToken;
    private static ISecureReliableTransport _engine;
    private readonly Meter _metricsMeter = new($"Cinegy.TsAnalyzer.{nameof(AnalysisService)}");
    
    private const string LineBreak = "---------------------------------------------------------------------";
    private static Analyzer _analyzer;
    private static bool _receiving;
    private static readonly UdpClient UdpClient = new UdpClient { ExclusiveAddressUse = false };
    private static bool _pendingExit;

    private const int WarmUpTime = 500;
    private static bool _warmedUp;

    private static readonly TsPacketFactory Factory = new();

    private static readonly DateTime StartTime = DateTime.UtcNow;
    private static readonly List<string> ConsoleLines = new(1024);

    private const int DefaultChunk = 1316;

    public IServiceProvider Services => throw new NotImplementedException();

    #region Constructor and IHostedService

    public AnalysisService(ILoggerFactory loggerFactory, IConfiguration configuration, IHostApplicationLifetime appLifetime)
    {
        _logger = loggerFactory.CreateLogger<AnalysisService>();
        _appConfig = configuration.Get<AppConfig>();
        
        _appLifetime = appLifetime;

        var bannedMessages = new HashSet<string>
        {
            ": srt_accept: no pending connection available at the moment"
        };

        var loggerOptions = new LoggerOptions
        {
            LogFlags = LogFlag.DisableSeverity |
                       LogFlag.DisableThreadName |
                       LogFlag.DisableEOL |
                       LogFlag.DisableTime,
            LogMessageAction = (level, message, area, file, line) =>
            {
                if (bannedMessages.Contains(message)) return;

                var msLevel = level switch
                {
                    var x when x == LogLevel.Debug => Microsoft.Extensions.Logging.LogLevel.Debug,
                    var x when x == LogLevel.Notice => Microsoft.Extensions.Logging.LogLevel.Information,
                    var x when x == LogLevel.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
                    var x when x == LogLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
                    var x when x == LogLevel.Critical => Microsoft.Extensions.Logging.LogLevel.Critical,
                    _ => Microsoft.Extensions.Logging.LogLevel.Trace
                };
                _logger.Log(msLevel, area + message);
            }
        };


        _engine = SecureReliableTransport.Setup(loggerOptions);

    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Cinegy TS Analyzer service activity");

        _cancellationToken = cancellationToken;

        StartWorker();

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Shutting down Cinegy TS Analyzer service activity");
        
        _pendingExit = true;
                
        _logger.LogInformation("Cinegy TS Analyzer service stopped");

        return Task.CompletedTask;
    }

    #endregion
        
    public void StartWorker()
    {
        _analyzer = new Analyzer(_logger);

        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
        _receiving = true;

        _analyzer.Setup();

        _analyzer.TsDecoder.TableChangeDetected += TsDecoder_TableChangeDetected;

        Uri sourceUri;

        try
        {
            sourceUri = new Uri(_appConfig.SourceUrl);
        }
        catch (Exception ex)
        {
            _logger.LogCritical($"Failed to decode source URL parameter {_appConfig.SourceUrl}: {ex.Message}");
            _appLifetime.StopApplication();
            return;
        }

        if (sourceUri.Scheme.Equals("srt",StringComparison.InvariantCultureIgnoreCase)) {
            new Thread(new ThreadStart(delegate { VideoSrtWorker(sourceUri); })).Start();
        }
        else if (sourceUri.Scheme.Equals("udp", StringComparison.InvariantCultureIgnoreCase))
        {
            new Thread(new ThreadStart(delegate { NetworkWorker(sourceUri); })).Start();
        }
        else if (sourceUri.Scheme.Equals("rtp", StringComparison.InvariantCultureIgnoreCase))
        {
            new Thread(new ThreadStart(delegate { NetworkWorker(sourceUri); })).Start();
        }
        else if (sourceUri.Scheme.Equals("file", StringComparison.InvariantCultureIgnoreCase))
        {
            new Thread(new ThreadStart(delegate { FileWorker(new FileStream(sourceUri.AbsolutePath, FileMode.Open)); })).Start();
        }
        else
        {
            _logger.LogCritical($"Unsupported URI scheme passed in: {sourceUri.Scheme}");
            _appLifetime.StopApplication();
            return;
        }
            
        if(_appConfig.LiveConsole) Console.Clear();

        var lastConsoleHeartbeatMinute = -1;
        var lastDataProcessed = 0L;
        var runtimeFormatString = "{0} hours {1} mins";
        while (!_pendingExit)
        {
            PrintConsoleFeedback();

            Thread.Sleep(60);

            if (DateTime.Now.Minute == lastConsoleHeartbeatMinute) continue;

            lastConsoleHeartbeatMinute = DateTime.Now.Minute;
            var run = DateTime.Now.Subtract(StartTime);
            var runtimeStr = string.Format(runtimeFormatString,Math.Floor(run.TotalHours),run.Minutes);
            
            _logger.LogInformation($"Running: {runtimeStr}, Data Processed: {(Factory.TotalDataProcessed - lastDataProcessed) / 1048576}MB");
            
                
            lastDataProcessed = Factory.TotalDataProcessed;
        }

        _logger.LogInformation("Logging stopped.");
    }


    private void TsDecoder_TableChangeDetected(object sender, TableChangedEventArgs args)
    {
        _logger.LogInformation($"TS Table Change Detected: {args.Message}");
    }

    private void NetworkWorker(Uri sourceUri)
    {
        var multicastAddress = sourceUri.DnsSafeHost;
        var multicastPort = sourceUri.Port;
        var listenAdapter = sourceUri.UserInfo;
        
        var listenAddress = string.IsNullOrEmpty(listenAdapter) ? IPAddress.Any : IPAddress.Parse(listenAdapter);

        var localEp = new IPEndPoint(listenAddress, multicastPort);

        UdpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        UdpClient.Client.ReceiveBufferSize = 1500 * 3000;
        UdpClient.ExclusiveAddressUse = false;
        UdpClient.Client.Bind(localEp);
        //_analyser.NetworkMetric.UdpClient = UdpClient;

        var parsedMcastAddr = IPAddress.Parse(multicastAddress);
        UdpClient.JoinMulticastGroup(parsedMcastAddr, listenAddress);

        _logger.LogInformation($"Requesting Transport Stream on {sourceUri.Scheme}://{listenAddress}@{multicastAddress}:{multicastPort}");

        IPEndPoint remoteEndPoint = null;
        while (_receiving && !_pendingExit && !_cancellationToken.IsCancellationRequested)
        {
            var data= UdpClient.Receive(ref remoteEndPoint);
            ProcessData(data, data.Length);
        }
    }

    
    private void VideoSrtWorker(Uri srtUri)
    {
        var srtAddress = srtUri.DnsSafeHost;

        if (Uri.CheckHostName(srtUri.DnsSafeHost) != UriHostNameType.IPv4)
        {
            srtAddress = Dns.GetHostEntry(srtUri.DnsSafeHost).AddressList.First().ToString();
        }

        var inputVideoPacketsStarted = false;
        var endPoint = new IPEndPoint(IPAddress.Parse(srtAddress), srtUri.Port);
        var srtReceiver = _engine.CreateReceiver(endPoint, DefaultChunk);
            
        while (!_cancellationToken.IsCancellationRequested)
        {
            if (!inputVideoPacketsStarted)
            {
                _logger.LogInformation("Started receiving input video SRT packets...");
                inputVideoPacketsStarted = true;
            }

            try
            {
                var chunk = srtReceiver.GetChunk();
                if (chunk.DataLen == 0) continue;
                ProcessData(chunk.Data, chunk.DataLen);
            }
            catch (Exception ex)
            {
                _logger.LogError($@"Unhandled exception within video SRT receiver: {ex.Message}");
                break;
            }
        }

        _logger.LogError("Closing video SRT Receiver");
    }

    private void FileWorker(Stream stream)
    {
        var data = new byte[1316];
        var readBytes = stream.Read(data, 0, 1316);

        while (readBytes > 0)
        {
            ProcessData(data, readBytes);
            readBytes = stream.Read(data, 0, 1316);
        }

        _pendingExit = true;
        Thread.Sleep(250);
        
        PrintConsoleFeedback();
        Console.WriteLine("Completed reading of file - hit enter to exit!");
        Console.ReadLine();
    }
    
    private void ProcessData(byte[] data, int stat)
    {
        try
        {
            if (_warmedUp)
            {
                if (stat < data.Length)
                {
                    var trimBuf = new byte[stat];
                    Buffer.BlockCopy(data, 0, trimBuf, 0, stat);
                    _analyzer.RingBuffer.Add(trimBuf);
                }
                else
                {
                    _analyzer.RingBuffer.Add(data);
                }

                var tsPackets = Factory.GetRentedTsPacketsFromData(data, out var tsPktCount, stat);
                if (tsPackets == null)
                {
                    Debug.Assert(true);
                }

                if (tsPackets == null) return;

                Factory.ReturnTsPackets(tsPackets, tsPktCount);
            }
            else
            {
                if (DateTime.UtcNow.Subtract(StartTime) > new TimeSpan(0, 0, 0, 0, WarmUpTime))
                    _warmedUp = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($@"Unhandled exception decoding data from SRT payload: {ex.Message}");
        }
    }
    #region ConsoleOutput

    private void PrintConsoleFeedback()
    {
        if (_appConfig.LiveConsole == false) return;

        var runningTime = DateTime.UtcNow.Subtract(StartTime);

        var networkMetric = _analyzer.NetworkMetric;
        var rtpMetric = _analyzer.RtpMetric;

        PrintToConsole("Network Details - {0}\t\tRunning: {1:hh\\:mm\\:ss}", _appConfig.SourceUrl, runningTime);

        PrintToConsole(LineBreak);

        if (_appConfig.SourceUrl.StartsWith("srt"))
        {
            PrintToConsole(
                "Total Packets Rcvd: {0} \t\t\t\t",
                networkMetric.TotalPackets, networkMetric.NetworkBufferUsage, networkMetric.PeriodMaxNetworkBufferUsage);
        }
        else
        {
            PrintToConsole(
                "Total Packets Rcvd: {0} \tBuffer Usage: {1:0.00}%/(Peak: {2:0.00}%)",
                networkMetric.TotalPackets, networkMetric.NetworkBufferUsage, networkMetric.PeriodMaxNetworkBufferUsage);
        }

        PrintToConsole(
            "Total Data (MB): {0}\t\tPackets per sec:{1}", Factory.TotalDataProcessed / 1048576, networkMetric.PacketsPerSecond);

        PrintToConsole("Period Max Packet Jitter (ms): {0:0.0}\tCorrupt TS Packets: {1}",
            networkMetric.PeriodLongestTimeBetweenPackets * 1000, Factory.TotalCorruptedTsPackets);

        PrintToConsole(
            "Bitrates (Mbps): {0:0.00}/{1:0.00}/{2:0.00}/{3:0.00} (Current/Avg/Peak/Low)",
            networkMetric.CurrentBitrate / 1048576.0, networkMetric.AverageBitrate / 1048576.0,
            networkMetric.HighestBitrate / 1048576.0, networkMetric.LowestBitrate / 1048576.0);

        if (_appConfig.SourceUrl.StartsWith("rtp://",StringComparison.InvariantCultureIgnoreCase))
        {
            PrintClearLineToConsole();
            PrintToConsole($"RTP Details - SSRC: {rtpMetric.Ssrc}");
            PrintToConsole(LineBreak);
            PrintToConsole(
                "Seq Num: {0}\tTimestamp: {1}\tMin Lost Pkts: {2}",
                rtpMetric.LastSequenceNumber, rtpMetric.LastTimestamp, rtpMetric.EstimatedLostPackets);
        }

        var pidMetrics = _analyzer.PidMetrics;

        lock (pidMetrics)
        {
            var pcrPid = pidMetrics.FirstOrDefault(m => _analyzer.SelectedPcrPid > 0 && m.Pid == _analyzer.SelectedPcrPid);

            if (pcrPid != null)
            {
                var span = new TimeSpan((long)(_analyzer.LastPcr / 2.7));
                var oPcrSpan = new TimeSpan((long)(_analyzer.LastOpcr / 2.7));
                var largestDrift = pcrPid.PeriodLowestPcrDrift;
                if (pcrPid.PeriodLargestPcrDrift > largestDrift) largestDrift = pcrPid.PeriodLargestPcrDrift;
                PrintToConsole(
                    $"PCR Value: {span:hh\\:mm\\:ss\\.fff}, OPCR Value: {oPcrSpan:hh\\:mm\\:ss\\.fff}, Period Drift (ms): {largestDrift:0.00}");
            }

            //PrintToConsole($"RAW PCR / PTS: {_analyzer.LastPcr } / {_analyzer.LastVidPts * 8} / {_analyzer.LastSubPts * 8}");
            PrintClearLineToConsole();

            //PrintToConsole(pidMetrics.Count < 10
            //    ? $"PID Details - Unique PIDs: {pidMetrics.Count}"
            //    : $"PID Details - Unique PIDs: {pidMetrics.Count}, (10 shown by packet count)");
            PrintToConsole(LineBreak);

            foreach (var pidMetric in pidMetrics.OrderByDescending(m => m.PacketCount).Take(10))
            {
                PrintToConsole("TS PID: {0}\tPacket Count: {1} \t\tCC Error Count: {2}", pidMetric.Pid,
                    pidMetric.PacketCount, pidMetric.CcErrorCount);
            }
        }

        var tsDecoder = _analyzer.TsDecoder;

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
                
                var pmt = tsDecoder.GetSelectedPmt();//_options.ProgramNumber);
                if (pmt != null)
                {
                    _analyzer.SelectedPcrPid = pmt.PcrPid;
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
    
    private static void PrintClearLineToConsole()
    {
        if (OperatingSystem.IsWindows()){
            ConsoleLines.Add("\t"); //use a tab for a clear line, to ensure that an operation runs
        }
    }

// TODO: LK to clean up
    private static void PrintToConsole(string message, params object[] arguments)
    {
        if (OperatingSystem.IsWindows()){
       // if (_options.SuppressOutput) return;
            ConsoleLines.Add(string.Format(message, arguments));
        }
    }

// TODO: LK to clean up
    private static void ClearCurrentConsoleLine()
    {
        if (OperatingSystem.IsWindows()){
        // Write space to end of line, and then CR with no LF
            Console.Write("\r".PadLeft(Console.WindowWidth - Console.CursorLeft - 1));
        }
    }

    #endregion

    #region IDispose

    public void Dispose()
    {
        _pendingExit = true;
        _analyzer?.Dispose();
    }

    #endregion
}
