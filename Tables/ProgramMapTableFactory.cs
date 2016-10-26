using System.Collections.Generic;
using System.Diagnostics;
using TsAnalyser.TransportStream;

namespace TsAnalyser.Tables
{
    public class ProgramMapTableFactory : TableFactory
    {
        public ProgramMapTable ProgramMapTable { get; private set; }

        private new ProgramMapTable InProgressTable
        {
            get { return base.InProgressTable as ProgramMapTable; }
            set { base.InProgressTable = value; }
        }

        public void AddPacket(TsPacket packet)
        {
            CheckPid(packet.Pid);

            if (packet.PayloadUnitStartIndicator)
            {
                InProgressTable = new ProgramMapTable { Pid = packet.Pid, PointerField = packet.Payload[0] };

                if (InProgressTable.PointerField > packet.Payload.Length)
                {
                    Debug.Assert(true, "Program Map Table has packet pointer outside the packet.");
                }

                var pos = 1 + InProgressTable.PointerField;

                InProgressTable.VersionNumber = (byte)(packet.Payload[pos + 5] & 0x3E);

                if (ProgramMapTable?.VersionNumber == InProgressTable.VersionNumber)
                {
                    InProgressTable = null;
                    return;
                }

                InProgressTable.TableId = packet.Payload[pos];
                InProgressTable.SectionLength =
                    (short)(((packet.Payload[pos + 1] & 0x3) << 8) + packet.Payload[pos + 2]);
                InProgressTable.ProgramNumber = (ushort)((packet.Payload[pos + 3] << 8) + packet.Payload[pos + 4]);
                InProgressTable.CurrentNextIndicator = (packet.Payload[pos + 5] & 0x1) != 0;
                InProgressTable.SectionNumber = packet.Payload[pos + 6];
                InProgressTable.LastSectionNumber = packet.Payload[pos + 7];
                InProgressTable.PcrPid = (ushort)(((packet.Payload[pos + 8] & 0x1f) << 8) + packet.Payload[pos + 9]);
                InProgressTable.ProgramInfoLength =
                    (ushort)(((packet.Payload[pos + 10] & 0x3) << 8) + packet.Payload[pos + 11]);

            }

            if (InProgressTable == null) return;

            AddData(packet);

            if (!HasAllBytes()) return;

            var startOfNextField = GetDescriptors(InProgressTable.ProgramInfoLength, InProgressTable.PointerField + 13);

            InProgressTable.EsStreams = ReadEsInfoElements(InProgressTable.SectionLength, startOfNextField);

            ProgramMapTable = InProgressTable;

            OnTableChangeDetected();
        }

        private List<EsInfo> ReadEsInfoElements(short sectionLength, int startOfNextField)
        {
            var streams = new List<EsInfo>();

            while (startOfNextField < sectionLength)
            {
                var es = new EsInfo
                {
                    StreamType = Data[startOfNextField],
                    ElementaryPid = (short)(((Data[startOfNextField + 1] & 0x1f) << 8) + Data[startOfNextField + 2]),
                    EsInfoLength = (ushort)(((Data[startOfNextField + 3] & 0x3) << 8) + Data[startOfNextField + 4])
                };

                var descriptors = new List<Descriptor>();

                startOfNextField = startOfNextField + 5;
                var endOfDescriptors = startOfNextField + es.EsInfoLength;
                while (startOfNextField < endOfDescriptors)
                {
                    var des = DescriptorFactory.DescriptorFromData(Data, startOfNextField);
                    descriptors.Add(des);
                    startOfNextField += (des.DescriptorLength + 2);
                }

                es.Descriptors = descriptors;
                streams.Add(es);
            }

            return streams;
        }

    }
}