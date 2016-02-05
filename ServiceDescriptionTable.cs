using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace TsAnalyser
{
    public class ServiceDescriptionTable
    {
        public class Section
        {
            public ushort ServiceId;
            public bool EITScheduleFlag;
            public bool EITPresentFollowingFlag;
            public byte RunningStatus;
            public bool FreeCAMode;
            public ushort DescriptorsLoopLength;
           // public String Descriptor;

            public byte DVBDescriptorTag;
            public byte DescriptorLength;
            public byte ServiceType;
            public byte ServiceProviderNameLength;
            public byte Charset1;
            public String ServiceProviderName;
            public byte ServiceNameLength;
            public byte Charset2;
            public String ServiceName;
        }




        public byte PointerField;
        public byte TableId;
        public short SectionLength;
        public short TransportStreamId;
        public byte VersionNumber;
        public bool CurrentNextIndicator;
        public byte SectionNumber;
        public byte LastSectionNumber;
        public short OriginalNetworkId;
        
        public List<Section> Sections;
    }

    public class ServiceDescriptionTableFactory
    {
        private const int TsPacketSize = 188;
        private const int TsPacketHeaderSize = 4;

        public static ServiceDescriptionTable ServiceDescriptionTableFromTsPackets(TsPacket[] packets)
        {
            var sdt = new ServiceDescriptionTable();

            for (var i = 0; i < packets.Length; i++)
            {
                if (!packets[i].PayloadUnitStartIndicator) continue;

                // if (packets[i].Payload.Length != TsPacketSize - TsPacketHeaderSize) continue;

                sdt.PointerField = packets[i].Payload[0];

                if (sdt.PointerField > packets[i].Payload.Length)
                {
                    Debug.Assert(true, "Program Association Table has packet pointer outside the packet.");
                }

                var pos = 1 + sdt.PointerField;

                sdt.TableId = packets[i].Payload[pos];
                sdt.SectionLength = (short)((packets[i].Payload[pos + 1] & 0x3 << 8) + packets[i].Payload[pos + 2]);
                sdt.TransportStreamId = (short)((packets[i].Payload[pos + 3] << 8) + packets[i].Payload[pos + 4]);
                sdt.VersionNumber = (byte)(packets[i].Payload[pos + 5] & 0x3E);
                sdt.CurrentNextIndicator = (packets[i].Payload[pos + 5] & 0x1) != 0;
                sdt.SectionNumber = packets[i].Payload[pos + 6];
                sdt.LastSectionNumber = packets[i].Payload[pos + 7];

                sdt.OriginalNetworkId = (short)((packets[i].Payload[pos + 8] << 8) + packets[i].Payload[pos + 9]);
                int sectionstartStart = pos + 11;
                int currentSectionStart = sectionstartStart;
                sdt.Sections = new List<ServiceDescriptionTable.Section>();
                do
                {
                    ServiceDescriptionTable.Section section = new ServiceDescriptionTable.Section();
                    section.ServiceId = (ushort)(packets[i].Payload[currentSectionStart] << 8 + packets[i].Payload[currentSectionStart + 1]);
                    section.EITScheduleFlag = (packets[i].Payload[currentSectionStart + 2] & 0x40) == 0x40;
                    section.EITPresentFollowingFlag = (packets[i].Payload[currentSectionStart + 2] & 0x80) == 0x80;
                    section.RunningStatus = (byte)(packets[i].Payload[currentSectionStart + 3] & 0x7);
                    section.FreeCAMode = (packets[i].Payload[currentSectionStart + 3] & 0x8) == 0x8;
                    section.DescriptorsLoopLength = (ushort)(((packets[i].Payload[currentSectionStart + 3] & 0xf) << 8) + packets[i].Payload[currentSectionStart + 4]);

                    section.DVBDescriptorTag = packets[i].Payload[currentSectionStart + 5];
                    section.DescriptorLength = packets[i].Payload[currentSectionStart + 6];
                    section.ServiceType = packets[i].Payload[currentSectionStart + 7];
                    section.ServiceProviderNameLength = packets[i].Payload[currentSectionStart + 8];                                        
                    if (section.ServiceProviderNameLength > 1)
                    {
                        section.Charset1 = packets[i].Payload[currentSectionStart + 9];
                        section.ServiceProviderName = System.Text.Encoding.UTF8.GetString(packets[i].Payload, currentSectionStart + 10, section.ServiceProviderNameLength - 1);
                        if (section.Charset1 > 65)
                        {
                            section.ServiceProviderName = (char)(section.Charset1) + section.ServiceProviderName;
                            section.ServiceProviderName = section.ServiceProviderName.Substring(0, section.ServiceProviderName.Length);
                        }
                    }
                    section.ServiceNameLength = packets[i].Payload[currentSectionStart + 11 + section.ServiceProviderNameLength - 2];
                    if (section.ServiceNameLength > 2)
                    {
                        section.Charset2 = packets[i].Payload[currentSectionStart + 12 + section.ServiceProviderNameLength - 2];
                        section.ServiceName = System.Text.Encoding.UTF8.GetString(packets[i].Payload, currentSectionStart + 13 + section.ServiceProviderNameLength - 2, section.ServiceNameLength);
                        if (section.Charset2 > 65)
                        {
                            section.ServiceName = (char)(section.Charset2) + section.ServiceName;
                            section.ServiceName = section.ServiceName.Substring(0, section.ServiceName.Length - 1);
                        }
                    }


                    currentSectionStart = currentSectionStart + 5 + section.DescriptorsLoopLength;
                    sdt.Sections.Add(section);
                } while ((currentSectionStart + 5) <= sdt.SectionLength);
            
            }

            return sdt;
        }
    }
}
