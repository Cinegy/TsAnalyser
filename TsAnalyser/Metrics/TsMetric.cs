using System.Runtime.Serialization;

namespace TsAnalyser.Metrics
{
    internal class TsMetric 
    {
        [DataMember]
        public int PidCount { get; set; }

        [DataMember]
        public int PidPackets { get; set; }

        [DataMember]
        public int PidCcErrors { get; set; }

        [DataMember]
        public int TeiErrors { get; set; }

        [DataMember]
        public int LongestPcrDelta { get; set; }

        [DataMember]
        public int LargestPcrDrift { get; set; }

        [DataMember]
        public int LowestPcrDrift { get; set; }
    }
}
