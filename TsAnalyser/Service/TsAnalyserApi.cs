using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;
using Newtonsoft.Json;
using TsAnalyser.Metrics;
using TsDecoder.Tables;
using TsDecoder.TransportStream;

namespace TsAnalyser.Service
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class TsAnalyserApi : ITsAnalyserApi
    {
        private readonly Assembly _assembly = Assembly.GetExecutingAssembly();
        
        public NetworkMetric NetworkMetric { get; set; }
        
        public RtpMetric RtpMetric { get; set; }

        public List<PidMetric> TsMetrics = new List<PidMetric>();

        public ServiceDescriptionTable ServiceMetrics = null;//new Tables.ServiceDescriptionTable();

        public ProgramMapTable ProgramMetrics = null;// new Tables.DescriptorDictionaries();

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
   
        public Stream GetNetworkMetric()
        {
            WebOperationContext.Current?.OutgoingResponse.Headers.Add("Access-Control-Allow-Origin", "*");
            WebOperationContext.Current?.OutgoingResponse.Headers.Add("Cache-Control", "no-cache");
            WebOperationContext.Current?.OutgoingResponse.Headers.Add("ContentType", "application/json; charset=utf-8");

            var json =  JsonConvert.SerializeObject(NetworkMetric);
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
            return ms;
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

        //private SerialisableMetrics RefreshMetrics()
        //{
        //    var serialisableMetric = new SerialisableMetrics
        //    {
        //        Network =
        //        {
        //            TotalPacketsReceived = NetworkMetric.TotalPackets,
        //            AverageBitrate = NetworkMetric.AverageBitrate,
        //            CurrentBitrate = NetworkMetric.CurrentBitrate,
        //            HighestBitrate = NetworkMetric.HighestBitrate,
        //            LongestTimeBetweenPackets = NetworkMetric.LongestTimeBetweenPackets,
        //            LowestBitrate = NetworkMetric.LowestBitrate,
        //            NetworkBufferUsage = NetworkMetric.NetworkBufferUsage,
        //            PacketsPerSecond = NetworkMetric.PacketsPerSecond,
        //            ShortestTimeBetweenPackets = NetworkMetric.ShortestTimeBetweenPackets,
        //            TimeBetweenLastPacket = NetworkMetric.TimeBetweenLastPacket
        //        },
        //        Rtp =
        //        {
        //            EstimatedLostPackets = RtpMetric.EstimatedLostPackets,
        //            SequenceNumber = RtpMetric.LastSequenceNumber,
        //            Ssrc = RtpMetric.Ssrc,
        //            Timestamp = RtpMetric.LastTimestamp
        //        },
                
        //    };

        //    foreach (var ts in TsMetrics.OrderBy(p => p.Pid))
        //    {
        //        var streamType = "";
        //        if (ProgramMetrics?.EsStreams != null)
        //        {
        //            var esInfo = (ProgramMetrics?.EsStreams).FirstOrDefault(p => p.ElementaryPid == ts.Pid);
        //            if (esInfo != null)
        //            {
        //                if (DescriptorDictionaries.ShortElementaryStreamTypeDescriptions.ContainsKey(esInfo.StreamType))
        //                {
        //                    streamType = DescriptorDictionaries.ShortElementaryStreamTypeDescriptions[esInfo.StreamType];
        //                }
        //            }
        //        }
        //        serialisableMetric.Pid.Pids.Add(new SerialisableMetrics.SerialisablePidMetric.PidDetails()
        //        {
        //            CcErrorCount = ts.CcErrorCount,
        //            Pid = ts.Pid,
        //            PacketCount = ts.PacketCount,
        //            StreamType = streamType
        //        });
        //    }

        //    if (ServiceMetrics?.Items == null || ServiceMetrics?.Items?.Count <= 0) return serialisableMetric;

        //    foreach (var descriptor in ServiceMetrics?.Items[0].Descriptors.Where(d => d.DescriptorTag == 0x48))
        //    {
        //        var sd = descriptor as ServiceDescriptor;
        //        if (null == sd) continue;
        //        serialisableMetric.Service.ServiceName = sd.ServiceName.Value;
        //        serialisableMetric.Service.ServiceProvider = sd.ServiceProviderName.Value;
        //    }

        //    return serialisableMetric;
        //}

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
    


