using System;
using System.Diagnostics;

namespace TsAnalyser
{
    public class TsMetrics
    {
        public delegate void DiscontinuityDetectedEventHandler(object sender, TransportStreamEventArgs args);
        public delegate void TransportErrorIndicatorDetectedEventHandler(object sender, TransportStreamEventArgs args);

        public int Pid { get; set; }
        public long PacketCount { get; set; }
        public long CcErrorCount { get; set; }
        public int LastCc { get; set; }
        public bool IsProgAssociationTable { get; set; }
        public ProgAssociationTable ProgAssociationTable { get; private set; }
        public Tables.ProgramMapTable ProgramMapTable { get; private set; }

        public void AddPacket(TsPacket newPacket)
        {
            try
            {
                if (newPacket.Pid != Pid)
                    throw new InvalidOperationException("Cannot add TS Packet from different pid to a metric!");

                if (newPacket.TransportErrorIndicator)
                {
                    OnTransportErrorIndicatorDetected(newPacket.Pid);
                }
                else
                {
                    CheckCcContinuity(newPacket);
                    LastCc = newPacket.ContinuityCounter;

                    if (newPacket.Pid == 0x00)
                    {
                        IsProgAssociationTable = true;
                        ProgAssociationTable = ProgAssociationTableFactory.ProgAssociationTableFromTsPackets(new[] { newPacket });
                    }
                }

                PacketCount++;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Exception generated within AddPacket method: " + ex.Message);
            }
        }

        private void CheckCcContinuity(TsPacket newPacket)
        {
            try
            {
                if (PacketCount == 0)
                {
                    //fresh metric, first packet - so no possible error yet...
                    return;
                }

                if (newPacket.Pid == 0x1fff)
                    return;


                if (!newPacket.ContainsPayload)
                {
                    Debug.WriteLine("No payload");
                }


                if (LastCc == newPacket.ContinuityCounter)
                {
                    if (newPacket.ContainsPayload)
                    {
                        CcErrorCount++;
                    }

                    //special case of no data... ignore for now
                    //TODO: check for no data flag in original packet
                    Debug.Assert(true, "Special CC repeated case - requires investigation!");
                    return;
                }

                if (LastCc != 15)
                {
                    if (LastCc + 1 != newPacket.ContinuityCounter)
                    {
                        CcErrorCount++;
                        OnDiscontinuityDetected(newPacket.Pid);
                        return;
                    }
                }

                if (LastCc != 15 || newPacket.ContinuityCounter == 0) return;

                CcErrorCount++;
                OnDiscontinuityDetected(newPacket.Pid);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Exception generated within CheckCcContinuity method: " + ex.Message);
            }
        }

        // Continuity Counter Error has been detected.
        public event DiscontinuityDetectedEventHandler DiscontinuityDetected;
        // Transport Error Indicator has been detected inside packet.
        public event TransportErrorIndicatorDetectedEventHandler TransportErrorIndicatorDetected;

        protected virtual void OnDiscontinuityDetected(int tsPid)
        {
            var handler = DiscontinuityDetected;
            if (handler == null) return;
            var args = new TransportStreamEventArgs { TsPid = tsPid };
            handler(this, args);
        }

        protected virtual void OnTransportErrorIndicatorDetected(int tsPid)
        {
            var handler = TransportErrorIndicatorDetected;
            if (handler == null) return;
            var args = new TransportStreamEventArgs { TsPid = tsPid };
            handler(this, args);
        }
    }

    public class TransportStreamEventArgs : EventArgs
    {
        public int TsPid { get; set; }
    }
}