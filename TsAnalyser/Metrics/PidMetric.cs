using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Threading;
using TsDecoder.TransportStream;

namespace TsAnalyser.Metrics
{
    [DataContract]
    public class PidMetric : Metric
    {
        public delegate void DiscontinuityDetectedEventHandler(object sender, TransportStreamEventArgs args);
        public delegate void TransportErrorIndicatorDetectedEventHandler(object sender, TransportStreamEventArgs args);

        private int _periodPacketCount = 0;
        private int _periodCcErrorCount = 0;

        internal override void ResetPeriodTimerCallback(object o)
        {
            lock (this)
            {
                PeriodPacketCount = _periodPacketCount;
                _periodPacketCount = 0;

                PeriodCcErrorCount = _periodCcErrorCount;
                _periodCcErrorCount = 0;
                
                base.ResetPeriodTimerCallback(o);
            }
        }

        [DataMember]
        public int Pid { get; set; }

        [DataMember]
        public long PacketCount { get; private set; }
        
        [DataMember]
        public int PeriodPacketCount { get; private set; }

        [DataMember]
        public long CcErrorCount { get; private set; }

        [DataMember]
        public int PeriodCcErrorCount { get; private set; }

        private int LastCc { get; set; }
        
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
                }

                PacketCount++;
                _periodPacketCount++;
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

                if (LastCc == newPacket.ContinuityCounter)
                {
                    if (newPacket.ContainsPayload)
                    {
                        CcErrorCount++;
                        _periodCcErrorCount++;
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
                        _periodCcErrorCount++;
                        OnDiscontinuityDetected(newPacket.Pid);
                        return;
                    }
                }

                if (LastCc != 15 || newPacket.ContinuityCounter == 0) return;

                CcErrorCount++;
                _periodCcErrorCount++;
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

        private void OnDiscontinuityDetected(int tsPid)
        {
            var handler = DiscontinuityDetected;
            if (handler == null) return;
            var args = new TransportStreamEventArgs { TsPid = tsPid };
            handler(this, args);
        }

        private void OnTransportErrorIndicatorDetected(int tsPid)
        {
            var handler = TransportErrorIndicatorDetected;
            if (handler == null) return;
            var args = new TransportStreamEventArgs { TsPid = tsPid };
            handler(this, args);
        }
    }
}