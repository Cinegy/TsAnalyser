using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;
using System.IO;
using System.Net;
using System.Reflection;
using System.ServiceModel.Web;


namespace TsAnalyser
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class TsAnalyserApi : ITsAnalyserApi
    {
        private readonly Assembly _assembly = Assembly.GetExecutingAssembly();

        private SerialisableMetrics _serialisableMetric = new SerialisableMetrics();

        public NetworkMetric NetworkMetric { get; set; }
        
        public RtpMetric RtpMetric { get; set; }

        public List<TsMetrics> TsMetrics = new List<TsMetrics>();

        public Tables.ServiceDescriptionTable ServiceMetrics = null;//new Tables.ServiceDescriptionTable();
        public Tables.ProgramMapTable ProgramMetrics = null;// new Tables.ProgramMapTable();

        public void GetGlobalOptions()
        {
            if (WebOperationContext.Current == null) return;

            WebOperationContext.Current.OutgoingResponse.Headers.Add("Access-Control-Allow-Origin", "*");
            WebOperationContext.Current.OutgoingResponse.Headers.Add("Access-Control-Allow-Headers", "content-type");
            WebOperationContext.Current.OutgoingResponse.Headers.Add("Access-Control-Allow-Methods", "GET,PUT,POST,DELETE,OPTIONS");
        }

        public Stream ServeIndexEmbeddedStaticFile()
        {
            return ServeEmbeddedStaticFile();
        }

        public Stream ServeEmbeddedStaticFile()
        {
            if (WebOperationContext.Current == null)
            {
                return null;
            }

            var wildcardSegments = WebOperationContext.Current.IncomingRequest.UriTemplateMatch.WildcardPathSegments;
            var filename = wildcardSegments.LastOrDefault();

            if (string.IsNullOrWhiteSpace(filename))
            {
                filename = "index.html";
                wildcardSegments = new KeyedByTypeCollection<string>(new List<string>() { "index.html" });
            }
               
            var fileExt = new FileInfo(filename).Extension.ToLower();

            switch (fileExt)
            {
                case (".js"):
                    WebOperationContext.Current.OutgoingResponse.ContentType = "text/javascript";
                    break;
                case (".png"):
                    WebOperationContext.Current.OutgoingResponse.ContentType = "image/png";
                    break;
                case (".htm"):
                case (".html"):
                case (".htmls"):
                    WebOperationContext.Current.OutgoingResponse.ContentType = "text/html";
                    break;
                case (".css"):
                    WebOperationContext.Current.OutgoingResponse.ContentType = "text/css";
                    break;
                case (".jpeg"):
                case (".jpg"):
                    WebOperationContext.Current.OutgoingResponse.ContentType = "image/jpeg";
                    break;
                default:
                    WebOperationContext.Current.OutgoingResponse.ContentType = "application/octet-stream";
                    break;
            }

            try
            {
                var manifestAddress = wildcardSegments.Aggregate(_assembly.GetName().Name + ".embeddedWebResources", (current, wildcardPathSegment) => current + ("." + wildcardPathSegment));
                var stream = _assembly.GetManifestResourceStream(manifestAddress);

                if (stream == null)
                {
                    WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.NotFound;
                }

                return _assembly.GetManifestResourceStream(manifestAddress);
            }
            catch (Exception x)
            {
                Debug.WriteLine($"GetFile: {x.Message}", TraceEventType.Error);
                throw;
            }
        }
   
        public SerialisableMetrics GetCurrentMetrics()
        {
            WebOperationContext.Current?.OutgoingResponse.Headers.Add("Access-Control-Allow-Origin", "*");
            WebOperationContext.Current?.OutgoingResponse.Headers.Add("Cache-Control", "no-cache");

            RefreshMetrics();

            return _serialisableMetric;
        }

        public void ResetMetrics()
        {
            if (WebOperationContext.Current == null) return;

            WebOperationContext.Current.OutgoingResponse.Headers.Add("Access-Control-Allow-Origin", "*");
            WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.OK;

            OnStreamCommand(StreamCommandType.ResetMetrics);
        }
        
        public void StartStream()
        {
            WebOperationContext.Current?.OutgoingResponse.Headers.Add("Access-Control-Allow-Origin", "*");

            throw new NotImplementedException();
        }

        public void StopStream()
        {
            WebOperationContext.Current?.OutgoingResponse.Headers.Add("Access-Control-Allow-Origin", "*");
            
            throw new NotImplementedException();
        }

        private void RefreshMetrics()
        {
            _serialisableMetric = new SerialisableMetrics
            {
                Network =
                {
                    TotalPacketsRecieved = NetworkMetric.TotalPackets,
                    AverageBitrate = NetworkMetric.AverageBitrate,
                    CurrentBitrate = NetworkMetric.CurrentBitrate,
                    HighestBitrate = NetworkMetric.HighestBitrate,
                    LongestTimeBetweenPackets = NetworkMetric.LongestTimeBetweenPackets,
                    LowestBitrate = NetworkMetric.LowestBitrate,
                    NetworkBufferUsage = NetworkMetric.NetworkBufferUsage,
                    PacketsPerSecond = NetworkMetric.PacketsPerSecond,
                    ShortestTimeBetweenPackets = NetworkMetric.ShortestTimeBetweenPackets,
                    TimeBetweenLastPacket = NetworkMetric.TimeBetweenLastPacket
                },
                Rtp =
                {
                    MinLostPackets = RtpMetric.MinLostPackets,
                    SequenceNumber = RtpMetric.LastSequenceNumber,
                    Ssrc = RtpMetric.Ssrc,
                    Timestamp = RtpMetric.LastTimestamp
                },
                
            };

            foreach (var ts in TsMetrics.OrderBy(p => p.Pid))
            {
                var streamType = "";
                if (ProgramMetrics?.EsStreams != null)
                {
                    var esInfo = (ProgramMetrics?.EsStreams).FirstOrDefault(p => p.ElementaryPid == ts.Pid);
                    if (esInfo != null)
                    {
                        if (Tables.ProgramMapTable.ElementarystreamTypes.ContainsKey(esInfo.StreamType))
                        {
                            streamType = Tables.ProgramMapTable.ElementarystreamTypes[esInfo.StreamType];
                        }
                    }
                }
                _serialisableMetric.Ts.Pids.Add(new SerialisableMetrics.SerialisableTsMetric.PidDetails()
                {
                    CcErrorCount = ts.CcErrorCount,
                    Pid = ts.Pid,
                    PacketCount = ts.PacketCount,
                    StreamType = streamType
                });
            }

            if (ServiceMetrics?.Items == null || ServiceMetrics?.Items?.Count <= 0) return;

            foreach (var descriptor in ServiceMetrics?.Items[0].Descriptors.Where(d => d.DescriptorTag == 0x48))
            {
                var sd = descriptor as ServiceDescriptor;
                if (null == sd) continue;
                _serialisableMetric.Service.ServiceName = sd.ServiceName.Value;
                _serialisableMetric.Service.ServiceProvider = sd.ServiceProviderName.Value;
            }
        }

        public delegate void StreamCommandEventHandler(object sender, StreamCommandEventArgs e);

        public event StreamCommandEventHandler StreamCommand;

        protected virtual void OnStreamCommand(StreamCommandType command)
        {
            var handler = StreamCommand;
            if (handler == null) return;
            var args = new StreamCommandEventArgs {Command = command};
            handler(this, args);
        }
    }

    public class StreamCommandEventArgs : EventArgs
    {
        public StreamCommandType Command { get; set; }
    }

    public enum StreamCommandType
    {
        ResetMetrics,
        StopStream,
        StartStream
    }

}
    


