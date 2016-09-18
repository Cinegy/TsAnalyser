using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TsAnalyser.Tables
{
    public class OptionalPES
    {
        public byte MarkerBits { get; set; } //	2 	10 binary or 0x2 hex
        public byte ScramblingControl { get; set; } //	2 	00 implies not scrambled
        public bool Priority { get; set; } //	1 	
        public bool DataAlignmentIndicator { get; set; } // 	1 	1 indicates that the PES packet header is immediately followed by the video start code or audio syncword
        public bool Copyright { get; set; } //	1 	1 implies copyrighted
        public bool OriginalOrCopy { get; set; } //	1 	1 implies original
        public byte PTSDTSIndicator { get; set; } //	2 	11 = both present, 01 is forbidden, 10 = only PTS, 00 = no PTS or DTS
        public bool ESCRFlag { get; set; } //	1 	
        public bool ESRateFlag { get; set; } //	1 	
        public bool DSMTrickModeFlag { get; set; } // 	1 	
        public bool AdditionalCopyInfoFlag { get; set; } //	1 	
        public bool CRCFlag { get; set; } //	1 	
        public bool ExtensionFlag { get; set; } // 	1 	
        public byte PESHeaderLength { get; set; } //	8 	gives the length of the remainder of the PES header
        public byte[] OptionalFields { get; set; } // 	variable length 	presence is determined by flag bits above
        public byte[] StuffingBytes { get; set; } // 	variable length 	0xff
    }

    public class PES
    {
        public const byte PRIVATE_STREAM_1 = 0xBD;
        public const UInt32 PACKET_START_CODE_PREFIX = 0x000001;


        public UInt32 PacketStartCodePrefix { get; set; } //3 bytes 	0x000001
        public byte StreamId { get; set; } //	1 byte 	Examples: Audio streams (0xC0-0xDF), Video streams (0xE0-0xEF) [4][5][6][7]
                                           //Note: The above 4 bytes is called the 32 bit start code.
        public UInt16 PESPacketLength { get; set; } //	2 bytes 	Specifies the number of bytes remaining in the packet after this field. Can be zero. If the PES packet length is set to zero, the PES packet can be of any length. A value of zero for the PES packet length can be used only when the PES packet payload is a video elementary stream.[8]
        public OptionalPES OptionalPESHeader { get; set; } //	variable length (length >= 9) 	not present in case of Padding stream & Private stream 2 (navigation data)
        public byte[] StuffingBytes { get; set; } // 	variable length 	
        public byte[] Data { get; set; } //		See elementary stream. In the case of private streams the first byte of the payload is the sub-stream number.

        private UInt16 pesBytes = 0;
        private byte[] data;

        public PES(TsPacket packet)
        {
            PacketStartCodePrefix = (UInt32)((packet.PesHeader.Payload[0] << 16) + (packet.PesHeader.Payload[1] << 8) + packet.PesHeader.Payload[2]);
            StreamId = packet.PesHeader.Payload[3];
            PESPacketLength = (UInt16)((packet.PesHeader.Payload[4] << 8) + packet.PesHeader.Payload[5]);
            data = new byte[PESPacketLength + 6];

            Buffer.BlockCopy(packet.PesHeader.Payload, 0, data, pesBytes, packet.PesHeader.Payload.Length);
            pesBytes += (UInt16)(packet.PesHeader.Payload.Length);
            Buffer.BlockCopy(packet.Payload, 0, data, pesBytes, packet.Payload.Length);
            pesBytes += (UInt16)(packet.Payload.Length);
        }

        public bool HasAllBytes()
        {
            return pesBytes >= PESPacketLength + 6 && PESPacketLength > 0;
        }

        public bool Add(TsPacket packet)
        {
            if (!packet.PayloadUnitStartIndicator)
            {
                if ((PESPacketLength + 6 - pesBytes) > packet.Payload.Length)
                {
                    Buffer.BlockCopy(packet.Payload, 0, data, pesBytes, packet.Payload.Length);
                    pesBytes += (UInt16)(packet.Payload.Length);
                }
                else
                {
                    Buffer.BlockCopy(packet.Payload, 0, data, pesBytes, (PESPacketLength + 6 - pesBytes));
                    pesBytes += (UInt16)(PESPacketLength + 6 - pesBytes);
                }

                return true;
            }
            return false;
        }

        public bool Decode()
        {
            if (HasAllBytes())
            {
                OptionalPESHeader = new OptionalPES();
                OptionalPESHeader.MarkerBits = (byte)((data[6] >> 6) & 0x03);
                OptionalPESHeader.ScramblingControl = (byte)((data[6] >> 4) & 0x03);
                OptionalPESHeader.Priority = (data[6] & 0x08) == 0x08;
                OptionalPESHeader.DataAlignmentIndicator = (data[6] & 0x04) == 0x04;
                OptionalPESHeader.Copyright = (data[6] & 0x02) == 0x02;
                OptionalPESHeader.OriginalOrCopy = (data[6] & 0x01) == 0x01;

                OptionalPESHeader.PTSDTSIndicator = (byte)((data[7] >> 6) & 0x03);
                OptionalPESHeader.ESCRFlag = (data[7] & 0x20) == 0x20;
                OptionalPESHeader.ESRateFlag = (data[7] & 0x10) == 0x10;
                OptionalPESHeader.DSMTrickModeFlag = (data[7] & 0x08) == 0x08;
                OptionalPESHeader.AdditionalCopyInfoFlag = (data[7] & 0x04) == 0x04;
                OptionalPESHeader.CRCFlag = (data[7] & 0x02) == 0x02;
                OptionalPESHeader.ExtensionFlag = (data[7] & 0x01) == 0x01;

                OptionalPESHeader.PESHeaderLength = data[8];

                Data = data;

                return true;
            }
            return false;
        }
    }
}
