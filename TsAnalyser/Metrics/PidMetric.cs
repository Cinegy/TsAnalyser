﻿using System;
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
        private int _periodTeiCount = 0;
        private bool _hasPcr = false;
        private ulong _lastPcr = 0;
        private ulong _periodLargestPcrDelta;

        internal override void ResetPeriodTimerCallback(object o)
        {
            lock (this)
            {
                PeriodPacketCount = _periodPacketCount;
                _periodPacketCount = 0;

                PeriodCcErrorCount = _periodCcErrorCount;
                _periodCcErrorCount = 0;

                PeriodTeiCount = _periodTeiCount;
                _periodTeiCount = 0;

                PeriodLargestPcrDelta = (int)TsPacketFactory.PcrToTimespan(_periodLargestPcrDelta).TotalMilliseconds;

                _periodLargestPcrDelta = 0;
                
                base.ResetPeriodTimerCallback(o);
            }
        }

        [DataMember]
        public int Pid { get; set; }

        [DataMember]
        public long PacketCount { get; private set; }
        
        [DataMember]
        public int PeriodPacketCount { get; private set; }

        public long TeiCount { get; private set; }

        [DataMember]
        public int PeriodTeiCount { get; private set; }

        [DataMember]
        public long CcErrorCount { get; private set; }

        [DataMember]
        public int PeriodCcErrorCount { get; private set; }

        [DataMember]
        public bool HasPcr => _hasPcr;

        [DataMember]
        public int PeriodLargestPcrDelta { get; private set; }

        private int LastCc { get; set; }
        
        public void AddPacket(TsPacket newPacket)
        {
            try
            {
                if (newPacket.Pid != Pid)
                    throw new InvalidOperationException("Cannot add TS Packet from different pid to a metric!");

                if (newPacket.TransportErrorIndicator)
                {
                    TeiCount++;
                    _periodTeiCount++;
                }
                else
                {
                    CheckCcContinuity(newPacket);
                    CheckPcr(newPacket);
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

        private void CheckPcr(TsPacket tsPacket)
        {
            if (!tsPacket.AdaptationFieldExists) return;
            if (!tsPacket.AdaptationField.PcrFlag) return;

            //tsPacket.AdaptationField = new AdaptationField()
            //{
            //    FieldSize = 1 + tsPacket.Payload[4],
            //    PcrFlag = (tsPacket.Payload[5] & 0x10) != 0
            //};

            //if (tsPacket.AdaptationField.FieldSize >= tsPacket.Payload.Length)
            //{
            //    Debug.WriteLine("TS packet data adaptationFieldSize >= payloadSize");
            //    return;
            //}

            //if (tsPacket.AdaptationField.PcrFlag)
            //{
            //    _hasPcr = true;

            //    //Packet has PCR
            //    tsPacket.AdaptationField.Pcr = (((uint)(tsPacket.Payload[6]) << 24) + ((uint)(tsPacket.Payload[7] << 16)) + ((uint)(tsPacket.Payload[8] << 8)) + (tsPacket.Payload[9]));
            //    tsPacket.AdaptationField.Pcr <<= 1;
            //    if ((tsPacket.Payload[10] & 0x80) == 1)
            //    {
            //        tsPacket.AdaptationField.Pcr |= 1;
            //    }
            //    tsPacket.AdaptationField.Pcr *= 300;
            //    var iLow = (uint)((tsPacket.Payload[10] & 1) << 8) + tsPacket.Payload[11];
            //    tsPacket.AdaptationField.Pcr += iLow;
            //}

            if (_lastPcr != 0)
            {
                var latestDelta = tsPacket.AdaptationField.Pcr - _lastPcr;
                if (latestDelta > _periodLargestPcrDelta) _periodLargestPcrDelta = latestDelta;
            }

            _lastPcr = tsPacket.AdaptationField.Pcr;
            

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
        
        private void OnDiscontinuityDetected(int tsPid)
        {
            var handler = DiscontinuityDetected;
            if (handler == null) return;
            var args = new TransportStreamEventArgs { TsPid = tsPid };
            handler(this, args);
        }
        
    }
}