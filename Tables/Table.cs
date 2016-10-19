using System;
using TsAnalyser.TsElements;

namespace TsAnalyser.Tables
{
    public class Table
    {
        public byte PointerField { get; set; }
        public byte TableId { get; set; }//0	8	uimsbf
        public bool SectionSyntaxIndicator { get; set; }//	8	1	bslbf
        public bool Zero { get; set; }//9	1	bslbf
        public byte Reserved { get; set; }//10	2	bslbf
        public ushort SectionLength { get; set; }//	12	12	uimsbf

        private ushort _tableBytes;
        protected byte[] Data;


        public Table(TsPacket packet)
        {
            PointerField = 0;

            // if (packet.PayloadUnitStartIndicator && packet.AdaptationFieldFlag && packet.AdaptationField.TransportPrivateDataFlag)
            {
                PointerField = packet.Payload[0];
            }
            PointerField++;

            TableId = packet.Payload[PointerField + 0];
            SectionSyntaxIndicator = (packet.Payload[PointerField + 1] & 0x80) == 0x80;
            Reserved = (byte)((packet.Payload[PointerField + 1] >> 6) & 0x03);
            SectionLength = (ushort)(((packet.Payload[PointerField + 1] & 0xf) << 8) + packet.Payload[PointerField + 2]);

            Data = new byte[SectionLength + 3];
            if ((SectionLength + 3 + (PointerField - 1)) > packet.Payload.Length)
            {
                Buffer.BlockCopy(packet.Payload, PointerField - 1, Data, 0, packet.Payload.Length - (PointerField - 1));
                _tableBytes = (ushort)(packet.Payload.Length - (PointerField - 1));
            }
            else
            {
                Buffer.BlockCopy(packet.Payload, (PointerField - 1), Data, 0, SectionLength + 3);
                _tableBytes = (ushort)(SectionLength + 3);
            }
        }

        public virtual bool HasAllBytes()
        {
            return _tableBytes >= SectionLength + 3 && SectionLength > 0;
        }

        public virtual bool Add(TsPacket packet)
        {
            if (packet.PayloadUnitStartIndicator) return false;

            if ((SectionLength + 3 - _tableBytes) > packet.Payload.Length)
            {
                Buffer.BlockCopy(packet.Payload, 0, Data, _tableBytes, packet.Payload.Length);
                _tableBytes += (ushort)(packet.Payload.Length);
            }
            else
            {
                Buffer.BlockCopy(packet.Payload, 0, Data, _tableBytes, (SectionLength + 3 - _tableBytes));
                _tableBytes += (ushort)(SectionLength + 3 - _tableBytes);
            }

            return true;
        }

        public virtual bool ProcessTable()
        {
            return true;
        }
        public byte[] GetData()
        {
            return Data;
        }
    }
}
