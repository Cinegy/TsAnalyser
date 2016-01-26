using System;

namespace TsAnalyser
{
    public class RtpMetric
    {
        private long _totalPackets;
        public long MinLostPackets { get; private set; }
        public int LastSequenceNumber { get; private set; }
        public uint Ssrc { get; private set; }
        public uint LastTimestamp { get; private set; }

        public void AddPacket(byte[] data)
        {
            var seqNum = (data[2] << 8) + data[3];
            Ssrc = (uint) ((data[4] << 24) + (data[5] << 16) + (data[6] << 8) + data[7]);
            LastTimestamp = (uint) ((data[4] << 24) + (data[5] << 16) + (data[6] << 8) + data[7]);

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
                    MinLostPackets += ushort.MaxValue - LastSequenceNumber;
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
                MinLostPackets += seqDiff;
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

        protected virtual void OnSequenceDiscontinuityDetected()
        {
            var handler = SequenceDiscontinuityDetected;
            handler?.Invoke(this, EventArgs.Empty);
        }
    }
}