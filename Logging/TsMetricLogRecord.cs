using System.Runtime.Serialization;
using TsAnalyser.Metrics;

namespace TsAnalyser.Logging
{
    [DataContract]
    internal class TsMetricLogRecord
    {
        [DataMember]
        public NetworkMetric Net { get; set; }

        [DataMember]
        public RtpMetric Rtp { get; set; }

        [DataMember]
        public TsMetric Ts { get; set; }
    }
}
