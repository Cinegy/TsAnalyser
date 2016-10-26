using System.Diagnostics;
using TsAnalyser.TransportStream;

namespace TsAnalyser.Tables
{
    public class ProgramAssociationTableFactory : TableFactory
    {
        public ProgramAssociationTable ProgramAssociationTable { get; private set; }
        
        private new ProgramAssociationTable InProgressTable
        {
            get { return base.InProgressTable as ProgramAssociationTable; }
            set { base.InProgressTable = value; }
        }
        
        public void AddPacket(TsPacket packet)
        {
            CheckPid(packet.Pid);

            if (!packet.PayloadUnitStartIndicator) return;

            InProgressTable = new ProgramAssociationTable {PointerField = packet.Payload[0]};
            
            if (InProgressTable.PointerField > packet.Payload.Length)
            {
                Debug.Assert(true, "Program Association TableOld has packet pointer outside the packet.");
            }

            var pos = 1 + InProgressTable.PointerField;

            InProgressTable.VersionNumber = (byte) (packet.Payload[pos + 5] & 0x3E);
        
            if (ProgramAssociationTable != null &&
                ProgramAssociationTable.VersionNumber == InProgressTable.VersionNumber)
            {
                InProgressTable = null;
                return;
            }

            InProgressTable.TableId = packet.Payload[pos];
            InProgressTable.SectionLength = (short)(((packet.Payload[pos + 1] & 0x3) << 8) + packet.Payload[pos + 2]);
            InProgressTable.TransportStreamId = (short)((packet.Payload[pos + 3] << 8) + packet.Payload[pos + 4]);

            InProgressTable.CurrentNextIndicator = (packet.Payload[pos + 5] & 0x1) != 0;
            InProgressTable.SectionNumber = packet.Payload[pos + 6];
            InProgressTable.LastSectionNumber = packet.Payload[pos + 7];

            InProgressTable.ProgramNumbers = new short[(InProgressTable.SectionLength - 9)/4];
            InProgressTable.Pids = new short[(InProgressTable.SectionLength - 9)/4];
            var programStart = pos + 8;

            for (var i = 0; i < (InProgressTable.SectionLength - 9)/4; i++)
            {
                InProgressTable.ProgramNumbers[i] =
                    (short)
                        ((packet.Payload[programStart + (i*4)] << 8) + packet.Payload[programStart + 1 + (i*4)]);
                InProgressTable.Pids[i] =
                    (short)
                        (((packet.Payload[programStart + 2 + (i*4)] & 0x1F) << 8) +
                         packet.Payload[programStart + 3 + (i*4)]);      
            }

            ProgramAssociationTable = InProgressTable;

            OnTableChangeDetected();

        }
    }
}