using System;

namespace TsAnalyser.TsElements
{
    public class OptionalPes
    {
        public byte MarkerBits { get; set; } //	2 	10 binary or 0x2 hex
        public byte ScramblingControl { get; set; } //	2 	00 implies not scrambled
        public bool Priority { get; set; } //	1 	
        public bool DataAlignmentIndicator { get; set; } // 	1 	1 indicates that the PES packet header is immediately followed by the video start code or audio syncword
        public bool Copyright { get; set; } //	1 	1 implies copyrighted
        public bool OriginalOrCopy { get; set; } //	1 	1 implies original
        public byte PtsdtsIndicator { get; set; } //	2 	11 = both present, 01 is forbidden, 10 = only PTS, 00 = no PTS or DTS
        public bool EscrFlag { get; set; } //	1 	
        public bool EsRateFlag { get; set; } //	1 	
        public bool DsmTrickModeFlag { get; set; } // 	1 	
        public bool AdditionalCopyInfoFlag { get; set; } //	1 	
        public bool CrcFlag { get; set; } //	1 	
        public bool ExtensionFlag { get; set; } // 	1 	
        public byte PesHeaderLength { get; set; } //	8 	gives the length of the remainder of the PES header
        public byte[] OptionalFields { get; set; } // 	variable length 	presence is determined by flag bits above
    }

    public class Pes
    {
        public const byte PrivateStream1 = 0xBD;
        public const uint DefaultPacketStartCodePrefix = 0x000001;
        
        public uint PacketStartCodePrefix { get; set; } //3 bytes 	0x000001
        public byte StreamId { get; set; } //	1 byte 	Examples: Audio streams (0xC0-0xDF), Video streams (0xE0-0xEF) [4][5][6][7]
                                           //Note: The above 4 bytes is called the 32 bit start code.
        public ushort PesPacketLength { get; set; } //	2 bytes 	Specifies the number of bytes remaining in the packet after this field. Can be zero. If the PES packet length is set to zero, the PES packet can be of any length. A value of zero for the PES packet length can be used only when the PES packet payload is a video elementary stream.[8]
        public OptionalPes OptionalPesHeader { get; set; } //	variable length (length >= 9) 	not present in case of Padding stream & Private stream 2 (navigation data)
        public byte[] Data { get; set; } //		See elementary stream. In the case of private streams the first byte of the payload is the sub-stream number.

        private ushort _pesBytes;
        private readonly byte[] _data;

        public Pes(TsPacket packet)
        {
            PacketStartCodePrefix = (uint)((packet.PesHeader.Payload[0] << 16) + (packet.PesHeader.Payload[1] << 8) + packet.PesHeader.Payload[2]);
            StreamId = packet.PesHeader.Payload[3];
            PesPacketLength = (ushort)((packet.PesHeader.Payload[4] << 8) + packet.PesHeader.Payload[5]);
            _data = new byte[PesPacketLength + 6];

            Buffer.BlockCopy(packet.PesHeader.Payload, 0, _data, _pesBytes, packet.PesHeader.Payload.Length);
            _pesBytes += (ushort)(packet.PesHeader.Payload.Length);
            Buffer.BlockCopy(packet.Payload, 0, _data, _pesBytes, packet.Payload.Length);
            _pesBytes += (ushort)(packet.Payload.Length);
        }

        public bool HasAllBytes()
        {
            return _pesBytes >= PesPacketLength + 6 && PesPacketLength > 0;
        }

        public bool Add(TsPacket packet)
        {
            if (packet.PayloadUnitStartIndicator) return false;

            if ((PesPacketLength + 6 - _pesBytes) > packet.Payload.Length)
            {
                Buffer.BlockCopy(packet.Payload, 0, _data, _pesBytes, packet.Payload.Length);
                _pesBytes += (ushort)(packet.Payload.Length);
            }
            else
            {
                Buffer.BlockCopy(packet.Payload, 0, _data, _pesBytes, (PesPacketLength + 6 - _pesBytes));
                _pesBytes += (ushort)(PesPacketLength + 6 - _pesBytes);
            }

            return true;
        }

        public bool Decode()
        {
            if (!HasAllBytes()) return false;

            OptionalPesHeader = new OptionalPes
            {
                MarkerBits = (byte) ((_data[6] >> 6) & 0x03),
                ScramblingControl = (byte) ((_data[6] >> 4) & 0x03),
                Priority = (_data[6] & 0x08) == 0x08,
                DataAlignmentIndicator = (_data[6] & 0x04) == 0x04,
                Copyright = (_data[6] & 0x02) == 0x02,
                OriginalOrCopy = (_data[6] & 0x01) == 0x01,
                PtsdtsIndicator = (byte) ((_data[7] >> 6) & 0x03),
                EscrFlag = (_data[7] & 0x20) == 0x20,
                EsRateFlag = (_data[7] & 0x10) == 0x10,
                DsmTrickModeFlag = (_data[7] & 0x08) == 0x08,
                AdditionalCopyInfoFlag = (_data[7] & 0x04) == 0x04,
                CrcFlag = (_data[7] & 0x02) == 0x02,
                ExtensionFlag = (_data[7] & 0x01) == 0x01,
                PesHeaderLength = _data[8]
            };
            
            Data = _data;

            return true;
        }
    }
}
