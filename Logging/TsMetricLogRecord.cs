using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using TsAnalyser.Metrics;

namespace TsAnalyser.Logging
{

    [DataContract]
    internal class TsMetricLogRecord : LogRecord
    {
        [DataMember]
        public NetworkMetric Net { get; set; }

        [DataMember]
        public RtpMetric Rtp { get; set; }

        [DataMember]
        public TsMetric Ts { get; set; }
    }
}
