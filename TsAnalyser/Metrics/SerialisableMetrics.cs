using System;
using System.Collections.Generic;

namespace TsAnalyser.Metrics
{
    public class SerialisableMetricsOld
    {
        public SerialisableMetricsOld()
        {
            Pid = new SerialisablePidMetric();
            Rtp = new SerialisableRtpMetric();
            Service = new ServiceDescriptionMetric();
        }
        
        public SerialisablePidMetric Pid { get; set; }
        public SerialisableRtpMetric Rtp { get; set; }
        public ServiceDescriptionMetric Service { get; set; }

        public class SerialisablePidMetric
        {
            public SerialisablePidMetric()
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
