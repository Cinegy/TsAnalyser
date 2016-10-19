using System.Diagnostics;
using TsAnalyser.TsElements;

namespace TsAnalyser.Tables
{
    public class ProgAssociationTable
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
        public short[] Pids;
        public short PmtPid = -1;
    }

    public class ProgAssociationTableFactory
    {
        public static ProgAssociationTable ProgAssociationTableFromTsPackets(TsPacket[] packets)
        {
            var pat = new ProgAssociationTable();

            for (var i = 0; i < packets.Length; i++)
            {
                if (!packets[i].PayloadUnitStartIndicator) continue;
                
                pat.PointerField = packets[i].Payload[0];

                if (pat.PointerField > packets[i].Payload.Length)
                {
                    Debug.Assert(true, "Program Association Table has packet pointer outside the packet.");
                }

                var pos = 1 + pat.PointerField;

                pat.TableId = packets[i].Payload[pos];
                pat.SectionLength = (short) (((packets[i].Payload[pos + 1] & 0x3) << 8) + packets[i].Payload[pos + 2]);
                pat.TransportStreamId = (short) ((packets[i].Payload[pos + 3] << 8) + packets[i].Payload[pos + 4]);
                pat.VersionNumber = (byte) (packets[i].Payload[pos + 5] & 0x3E);
                pat.CurrentNextIndicator = (packets[i].Payload[pos + 5] & 0x1) != 0;
                pat.SectionNumber = packets[i].Payload[pos + 6];
                pat.LastSectionNumber = packets[i].Payload[pos + 7];

                pat.ProgramNumbers = new short[(pat.SectionLength - 9)/4];
                pat.Pids = new short[(pat.SectionLength - 9) / 4];
                var programStart = pos + 8;
               
                for (var ii = 0; ii < (pat.SectionLength - 9) / 4; ii++)
                {
                    pat.ProgramNumbers[ii] = (short)((packets[i].Payload[programStart + (ii * 4)] << 8) + packets[i].Payload[programStart + 1 + (ii * 4)]);
                    pat.Pids[ii] = (short)(((packets[i].Payload[programStart + 2 + (ii * 4)] & 0x1F) << 8) + packets[i].Payload[programStart + 3 + (ii * 4)]);
                    if(pat.ProgramNumbers[ii] != 0)
                    {
                        pat.PmtPid = pat.Pids[ii];
                    }
                }
            }

            return pat;
        }
    }
}