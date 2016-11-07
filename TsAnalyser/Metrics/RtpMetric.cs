using System;
using System.Runtime.Serialization;

namespace TsAnalyser.Metrics
{
    [DataContract]
    public class RtpMetric : Metric
    {
        private long _totalPackets;
        private int _periodEstimatedLostPackets;

        internal override void ResetPeriodTimerCallback(object o)
        {
            lock (this)
            {
                PeriodEstimatedLostPackets = _periodEstimatedLostPackets ;
               _periodEstimatedLostPackets = 0;
                
                base.ResetPeriodTimerCallback(o);
            }
        }

        [DataMember]
        public long EstimatedLostPackets { get; private set; }

        [DataMember]
        public long PeriodEstimatedLostPackets { get; private set; }

        [DataMember]
        public int LastSequenceNumber { get; private set; }
        
        [DataMember]
        public uint Ssrc { get; private set; }

        [DataMember]
        public uint LastTimestamp { get; private set; }

        public void AddPacket(byte[] data)
        {
            var seqNum = (data[2] << 8) + data[3];
            LastTimestamp = (uint) ((data[4] << 24) + (data[5] << 16) + (data[6] << 8) + data[7]);
            Ssrc = (uint) ((data[8] << 24) + (data[9] << 16) + (data[10] << 8) + data[11]);

            if (_totalPackets == 0)
            {
                RegisterFirstPacket(seqNum);
                return;
            }

            _totalPackets++;

            if (seqNum == 0)
            {
                if (LastSequenceNumber != ushort.MaxValue)
                {
                    var lost = ushort.MaxValue - LastSequenceNumber;
                    if (lost > 30000)
                    {
                        lost = 1;
                    }
                    EstimatedLostPackets += lost;
                    _periodEstimatedLostPackets += lost;

                    OnSequenceDiscontinuityDetected();
                }
            }
            else if (LastSequenceNumber + 1 != seqNum)
            {
                var seqDiff = seqNum - LastSequenceNumber;

                if (seqDiff < 0)
                {
                    seqDiff = seqNum + ushort.MaxValue - LastSequenceNumber;
                }
                if (seqDiff > 30000)
                {
                    seqDiff = 1;
                }
                EstimatedLostPackets += seqDiff;
                _periodEstimatedLostPackets += seqDiff;

                OnSequenceDiscontinuityDetected();
            }


            LastSequenceNumber = seqNum;
        }

        private void RegisterFirstPacket(int seqNum)
        {
            LastSequenceNumber = seqNum;
            _totalPackets++;
        }

        // Sequence Counter Error has been detected
        public event EventHandler SequenceDiscontinuityDetected;

        private void OnSequenceDiscontinuityDetected()
        {
            var handler = SequenceDiscontinuityDetected;
            handler?.Invoke(this, EventArgs.Empty);
        }
    }
}
