using System.Collections.Generic;

namespace TsAnalyser
{
    public class SerialisableMetrics
    {
        public SerialisableMetrics()
        {
            Network = new SerialisableNetworkMetric();
            Ts = new SerialisableTsMetric();
            Rtp = new SerialisableRtpMetric();
            Service = new ServiceDescriptionMetric();
        }

        public SerialisableNetworkMetric Network { get; set; }
        public SerialisableTsMetric Ts { get; set; }

        public SerialisableRtpMetric Rtp { get; set; }
        public ServiceDescriptionMetric Service { get; set; }

        public class SerialisableNetworkMetric
        {
            public long TotalPacketsRecieved { get; set; }
            public long CurrentBitrate { get; set; }
            public long HighestBitrate { get; set; }
            public long LongestTimeBetweenPackets { get; set; }
            public long LowestBitrate { get; set; }
            public float NetworkBufferUsage { get; set; }
            public int PacketsPerSecond { get; set; }
            public long ShortestTimeBetweenPackets { get; set; }
            public long TimeBetweenLastPacket { get; set; }
            public long AverageBitrate { get; set; }
        }

        public class SerialisableTsMetric
        {
            public SerialisableTsMetric()
            {
                Pids = new List<PidDetails>();
            }

            public List<PidDetails> Pids { get; set; }

            public class PidDetails
            {
                public int Pid { get; set; }
                public long CcErrorCount { get; set; }
                public long PacketCount { get; set; }
                public string StreamType { get; set; }
            }
        }

        public class SerialisableRtpMetric
        {
            public long MinLostPackets { get; set; }
            public long SequenceNumber { get; set; }
            public long Timestamp { get; set; }
            public long Ssrc { get; set; }
        }

        public class ServiceDescriptionMetric
        {
            public string ServiceName { get; set; }
            public string ServiceProvider { get; set; }
        }
    }
}
