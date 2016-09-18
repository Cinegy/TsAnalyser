using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TsAnalyser.Tables
{
    public class Table
    {
        public byte PointerField { get; set; }
        public byte TableId { get; set; }//0	8	uimsbf
        public bool SectionSyntaxIndicator { get; set; }//	8	1	bslbf
        public bool Zero { get; set; }//9	1	bslbf
        public byte Reserved { get; set; }//10	2	bslbf
        public UInt16 SectionLength { get; set; }//	12	12	uimsbf

        private UInt16 tableBytes = 0;
        protected byte[] data;


        public Table(TsAnalyser.TsPacket packet)
        {
            this.PointerField = 0;

            // if (packet.PayloadUnitStartIndicator && packet.AdaptationFieldFlag && packet.AdaptationField.TransportPrivateDataFlag)
            {
                this.PointerField = (byte)(packet.Payload[0]);
            }
            this.PointerField++;

            this.TableId = packet.Payload[this.PointerField + 0];
            this.SectionSyntaxIndicator = (bool)((packet.Payload[this.PointerField + 1] & 0x80) == 0x80);
            this.Reserved = (byte)((packet.Payload[this.PointerField + 1] >> 6) & 0x03);
            this.SectionLength = (UInt16)(((packet.Payload[this.PointerField + 1] & 0xf) << 8) + packet.Payload[this.PointerField + 2]);

            data = new byte[SectionLength + 3];
            if ((SectionLength + 3 + (PointerField - 1)) > packet.Payload.Length)
            {
                Buffer.BlockCopy(packet.Payload, PointerField - 1, data, 0, packet.Payload.Length - (PointerField - 1));
                tableBytes = (UInt16)(packet.Payload.Length - (PointerField - 1));
            }
            else
            {
                Buffer.BlockCopy(packet.Payload, (PointerField - 1), data, 0, SectionLength + 3);
                tableBytes = (UInt16)(SectionLength + 3);
            }
        }

        virtual public bool HasAllBytes()
        {
            return tableBytes >= SectionLength + 3 && SectionLength > 0;
        }

        virtual public bool Add(TsAnalyser.TsPacket packet)
        {
            if (!packet.PayloadUnitStartIndicator)
            {
                if ((SectionLength + 3 - tableBytes) > packet.Payload.Length)
                {
                    Buffer.BlockCopy(packet.Payload, 0, data, tableBytes, packet.Payload.Length);
                    tableBytes += (UInt16)(packet.Payload.Length);
                }
                else
                {
                    Buffer.BlockCopy(packet.Payload, 0, data, tableBytes, (SectionLength + 3 - tableBytes));
                    tableBytes += (UInt16)(SectionLength + 3 - tableBytes);
                }

                return true;
            }
            return false;
        }

        virtual public bool ProcessTable()
        {
            return true;
        }
        public byte[] GetData()
        {
            return data;
        }
    }
}
