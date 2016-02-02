using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace TsAnalyser
{
    public class ProgramMapTable
    {
        public class Section
        {
            public byte StreamType;
            public short ElementaryPID;
            public short ESInfoLength;
            public String Descriptor;
        }

      
        public byte PointerField;
        public byte TableId;
        public short SectionLength;
        public short ProgramNumber;
        public byte VersionNumber;
        public bool CurrentNextIndicator;
        public byte SectionNumber;
        public byte LastSectionNumber;
        public short ProgramInfoLength;
        public String descriptor;
        public List<Section> Sections;
    }
    public class ProgramMapTableFactory
    {
        private const int TsPacketSize = 188;
        private const int TsPacketHeaderSize = 4;

        public static ProgramMapTable ProgramMapTableFromTsPackets(TsPacket[] packets)
        {
            var pmt = new ProgramMapTable();

            for (var i = 0; i < packets.Length; i++)
            {
                if (!packets[i].PayloadUnitStartIndicator) continue;

                // if (packets[i].Payload.Length != TsPacketSize - TsPacketHeaderSize) continue;

                pmt.PointerField = packets[i].Payload[0];

                if (pmt.PointerField > packets[i].Payload.Length)
                {
                    Debug.Assert(true, "Program Association Table has packet pointer outside the packet.");
                }

                var pos = 1 + pmt.PointerField;

                pmt.TableId = packets[i].Payload[pos];
                pmt.SectionLength = (short)((packets[i].Payload[pos + 1] & 0x3 << 8) + packets[i].Payload[pos + 2]);
                pmt.ProgramNumber = (short)((packets[i].Payload[pos + 3] << 8) + packets[i].Payload[pos + 4]);
                pmt.VersionNumber = (byte)(packets[i].Payload[pos + 5] & 0x3E);
                pmt.CurrentNextIndicator = (packets[i].Payload[pos + 5] & 0x1) != 0;
                pmt.SectionNumber = packets[i].Payload[pos + 6];
                pmt.LastSectionNumber = packets[i].Payload[pos + 7];
                pmt.ProgramInfoLength = (short)((packets[i].Payload[pos + 10] & 0x3 << 8) + packets[i].Payload[pos + 11]);
                pmt.descriptor = BitConverter.ToString(packets[i].Payload, 12, pmt.ProgramInfoLength);
                // pmt.Sections = new String[(pmt.SectionLength - 9) / 4];                   
                int sectionstartStart = pos + 12 + pmt.ProgramInfoLength;
                int currentSectionStart = sectionstartStart;
                pmt.Sections = new List<ProgramMapTable.Section>();
                do
                {
                    ProgramMapTable.Section section = new ProgramMapTable.Section();
                    section.StreamType = packets[i].Payload[currentSectionStart];
                    section.ElementaryPID = (short)((packets[i].Payload[currentSectionStart + 1] & 0x1F << 8) + packets[i].Payload[currentSectionStart + 2]);
                    section.ESInfoLength = (short)((packets[i].Payload[currentSectionStart + 3] & 0x3 << 8) + packets[i].Payload[currentSectionStart + 4]);
                    section.Descriptor = BitConverter.ToString(packets[i].Payload, currentSectionStart + 5, section.ESInfoLength);
                    currentSectionStart = currentSectionStart + 5 + section.ESInfoLength;
                    pmt.Sections.Add(section);
                } while ((currentSectionStart + 5) <= pmt.SectionLength);

               

                /*  for (int ii = 0; ii < (pat.SectionLength - 9) / 4; ii++)
                  {
                      pat.ProgramNumbers[ii] = (short)((packets[i].Payload[programStart + (ii * 4)] << 8) + packets[i].Payload[programStart + 1 + (ii * 4)]);
                      pat.PIDS[ii] = (short)((packets[i].Payload[programStart + 2 + (ii * 4)] & 0x1F << 8) + packets[i].Payload[programStart + 3 + (ii * 4)]);
                  }*/
            }

            return pmt;
        }

    }
        
}
