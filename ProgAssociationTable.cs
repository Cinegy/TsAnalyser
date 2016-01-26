using System.Diagnostics;

namespace TsAnalyser
{
    public struct ProgAssociationTable
    {
        public byte PointerField;
        public byte TableId;
        public short SectionLength;
        public short TransportStreamId;
        public byte VersionNumber;
        public bool CurrentNextIndicator;
        public byte SectionNumber;
        public byte LastSectionNumber;
        public short[] ProgramNumbers;
    }

    public class ProgAssociationTableFactory
    {
        private const int TsPacketSize = 188;
        private const int TsPacketHeaderSize = 4;

        public static ProgAssociationTable ProgAssociationTableFromTsPackets(TsPacket[] packets)
        {
            var pat = new ProgAssociationTable();

            for (var i = 0; i < packets.Length; i++)
            {
                if (!packets[i].PayloadUnitStartIndicator) continue;

                if (packets[i].Payload.Length != TsPacketSize - TsPacketHeaderSize) continue;

                pat.PointerField = packets[i].Payload[0];

                if (pat.PointerField > packets[i].Payload.Length)
                {
                    Debug.Assert(true, "Program Association Table has packet pointer outside the packet.");
                }

                var pos = 1 + pat.PointerField;

                pat.TableId = packets[i].Payload[pos];
                pat.SectionLength = (short) ((packets[i].Payload[pos + 1] & 0x3 << 8) + packets[i].Payload[pos + 2]);
                pat.TransportStreamId = (short) ((packets[i].Payload[pos + 3] << 8) + packets[i].Payload[pos + 4]);
                pat.VersionNumber = (byte) (packets[i].Payload[pos + 5] & 0x3E);
                pat.CurrentNextIndicator = (packets[i].Payload[pos + 5] & 0x1) != 0;
                pat.SectionNumber = packets[i].Payload[pos + 6];
                pat.LastSectionNumber = packets[i].Payload[pos + 7];

                pat.ProgramNumbers = new short[(pat.SectionLength - 9)/4];
                pat.ProgramNumbers[0] = (short) ((packets[i].Payload[pos + 8] << 8) + packets[i].Payload[pos + 9]);
            }

            return pat;
        }
    }
}