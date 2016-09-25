using System;
using System.Collections.Generic;

namespace TsAnalyser.Tables
{
    public class ServiceDescriptionTable : Table
    {
        public class Item
        {
            public ushort ServiceId;
            public bool EitScheduleFlag;
            public bool EitPresentFollowingFlag;
            public byte RunningStatus;
            public bool FreeCaMode;
            public ushort DescriptorsLoopLength;
            public IEnumerable<Descriptor> Descriptors { get; set; }

            public byte DvbDescriptorTag;
            public byte DescriptorLength;            
        }

       

        public ushort TransportStreamId;
        public byte VersionNumber;
        public bool CurrentNextIndicator;
        public byte SectionNumber;
        public byte LastSectionNumber;
        public ushort OriginalNetworkId;
        
        public List<Item> Items;

        public ServiceDescriptionTable(TsPacket packet) : base(packet)
        {

        }

        public override bool ProcessTable()
        {
            var sdt = this;//new ServiceDescriptionTable();
            sdt.PointerField = 0;

            // if (packet.PayloadUnitStartIndicator && packet.AdaptationFieldFlag && packet.AdaptationField.TransportPrivateDataFlag)
            {
                //  eit.PointerField = (byte)(data[0]);
            }
            sdt.PointerField++;
            if (sdt.PointerField > Data.Length)
            {
#if DEBUG
                Console.WriteLine("PointerField outside packet");
#endif
                return false;
            }

            sdt.TableId = Data[sdt.PointerField + 0];
            if (TableId != 0x42) return false;

            sdt.SectionSyntaxIndicator = (Data[sdt.PointerField + 1] & 0x80) == 0x80;
            sdt.Reserved = (byte)((Data[sdt.PointerField + 1] >> 6) & 0x03);
            sdt.SectionLength = (ushort)(((Data[sdt.PointerField + 1] & 0xf) << 8) + Data[sdt.PointerField + 2]);
            sdt.TransportStreamId = (ushort)((Data[sdt.PointerField + 3] << 8) + Data[sdt.PointerField + 4]);
            //sdt.Reserved2 = (byte)((data[sdt.PointerField + 5] >> 6) & 0x03);
            sdt.VersionNumber = (byte)((Data[sdt.PointerField + 5] & 0x3E) >> 1);
            sdt.CurrentNextIndicator = (Data[sdt.PointerField + 5] & 0x01) == 0x01;
            sdt.SectionNumber = Data[sdt.PointerField + 6];
            sdt.LastSectionNumber = Data[sdt.PointerField + 7];

            sdt.OriginalNetworkId = (ushort)((Data[sdt.PointerField + 8] << 8) + Data[sdt.PointerField + 9]);
            //sdt.ReservedFutureUse = data[sdt.PointerField + 10];

            var startOfNextField = (ushort)(sdt.PointerField + 11);

            var transportStreamLoopEnd = (ushort)(sdt.SectionLength - 4);
            var items = new List<Item>();
            while (startOfNextField < transportStreamLoopEnd)
            {
                var item = new Item
                {
                    ServiceId = (ushort) ((Data[startOfNextField] << 8) + Data[startOfNextField + 1]),
                    EitScheduleFlag = ((Data[startOfNextField + 2]) & 0x02) == 0x02,
                    EitPresentFollowingFlag = ((Data[startOfNextField + 2]) & 0x01) == 0x01,
                    RunningStatus = (byte) ((Data[startOfNextField + 3] >> 5) & 0x07),
                    FreeCaMode = (Data[startOfNextField + 3] & 0x10) == 0x10,
                    DescriptorsLoopLength =
                        (ushort) (((Data[startOfNextField + 3] & 0xf) << 8) + Data[startOfNextField + 4])
                };
                
                // item.ReservedFutureUse = (byte)((data[startOfNextField + 2] >> 2) & 0x3F);

                var descriptors = new List<Descriptor>();

                startOfNextField = (ushort)(startOfNextField + 5);
                var endOfDescriptors = (ushort)(startOfNextField + item.DescriptorsLoopLength);
                if (endOfDescriptors > Data.Length)
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine("TODO: Figure out why sometimes descriptors are bigger than packet - now ignoring rest of packet");
#endif
                    return false;
                }
                while (startOfNextField < endOfDescriptors)
                {
                    var des = DescriptorFactory.DescriptorFromTsPacketPayload(Data, startOfNextField);
                    descriptors.Add(des);
                    startOfNextField += (ushort)(des.DescriptorLength + 2);
                }
                item.Descriptors = descriptors;
                items.Add(item);

            }
            sdt.Items = items;

            return true;
        }

    }

    /*  public class ServiceDescriptionTableFactory
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
                  sdt.Sections = new List<ServiceDescriptionTable.Item>();
                  do
                  {
                      ServiceDescriptionTable.Item section = new ServiceDescriptionTable.Item();
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
      }*/
}
