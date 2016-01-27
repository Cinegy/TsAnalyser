using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;
using System.Globalization;
using System.IO;
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

        public void GetGlobalOptions()
        {
            if (WebOperationContext.Current == null) return;

            WebOperationContext.Current.OutgoingResponse.Headers.Add("Access-Control-Allow-Origin", "*");
            WebOperationContext.Current.OutgoingResponse.Headers.Add("Access-Control-Allow-Headers", "content-type");
            WebOperationContext.Current.OutgoingResponse.Headers.Add("Access-Control-Allow-Methods", "GET,PUT,POST,DELETE,OPTIONS");
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
                var items = _assembly.GetManifestResourceNames();
                var manifestAddress = wildcardSegments.Aggregate("TsAnalyser.embeddedWebResources", (current, wildcardPathSegment) => current + ("." + wildcardPathSegment));

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
         
            RefreshMetrics();

            return _serialisableMetric;
        }

        public void ResetMetrics(string itemPath)
        {
            WebOperationContext.Current?.OutgoingResponse.Headers.Add("Access-Control-Allow-Origin", "*");

            throw new NotImplementedException();
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
                    SSRC = RtpMetric.Ssrc,
                    Timestamp = RtpMetric.LastTimestamp
                }
            };

            foreach (var ts in TsMetrics)
            {
                _serialisableMetric.Ts.Pids.Add(new SerialisableMetrics.SerialisableTsMetric.PidDetails()
                {
                    CcErrorCount = ts.CcErrorCount,
                    Pid = ts.Pid,
                    IsProgAssociationTable = ts.IsProgAssociationTable,
                    PacketCount = ts.PacketCount
                });
            }

        }

    }
    

}

