using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace TsAnalyser.Metrics
{
    internal class TsMetric : Metric
    {
        [DataMember]
        public int PidCount { get; set; }

        [DataMember]
        public int PidPackets { get; set; }

        [DataMember]
        public int PidCcErrors { get; set; }
    }
}
