using System;
using System.Collections.Generic;
using System.IO;
using TsAnalyser.Metrics;
using TsAnalyser.TransportStream;

namespace TsAnalyser.Tables
{
    public class TableFactory
    {
        public int TablePid { get; private set; } = -1;
        protected ITable InProgressTable { get; set; }
        
        public delegate void TableChangeEventHandler(object sender, TransportStreamEventArgs args);

        protected int GetDescriptors(int descriptorsLength, int startOfNextField)
        {
            var descriptors = new List<Descriptor>();
            var startPos = startOfNextField;
            while (startOfNextField < startPos + descriptorsLength)
            {
                var des = DescriptorFactory.DescriptorFromData(Data, startOfNextField);
                descriptors.Add(des);
                startOfNextField += (byte) (des.DescriptorLength + 2);
            }

            InProgressTable.Descriptors = descriptors;

            return startOfNextField;
        }

        private ushort _tableBytes;
        internal byte[] Data;

        internal bool HasAllBytes()
        {
            return _tableBytes >= InProgressTable.SectionLength + 3 && InProgressTable.SectionLength > 0;
        }

        internal void CheckPid(int newPid)
        {
            if (TablePid == newPid) return;

            if (TablePid == -1)
            {
                TablePid = newPid;
            }
            else
            {
                throw new InvalidDataException("TableFactory cannot have mixed PIDs added after startup");
            }
        }

        protected void AddData(TsPacket packet)
        {
            CheckPid(packet.Pid);

            if (packet.PayloadUnitStartIndicator)
            {
                Data = new byte[InProgressTable.SectionLength + 3];
                _tableBytes = 0;
            }

            if ((InProgressTable.SectionLength + 3 - _tableBytes) > packet.Payload.Length)
            {
                Buffer.BlockCopy(packet.Payload, 0, Data, _tableBytes, packet.Payload.Length);
                _tableBytes += (ushort)(packet.Payload.Length);
            }
            else
            {
                Buffer.BlockCopy(packet.Payload, 0, Data, _tableBytes, (InProgressTable.SectionLength + 3 - _tableBytes));
                _tableBytes += (ushort)(InProgressTable.SectionLength + 3 - _tableBytes);
            }
        }
        
        // The associated table has changed / been updated
        public event TableChangeEventHandler TableChangeDetected;
        
        protected void OnTableChangeDetected()
        {
            var handler = TableChangeDetected;
            if (handler == null) return;

            var generatingPid = -1;

            if (InProgressTable != null)
            {
                generatingPid = InProgressTable.Pid;
            }
            
            var args = new TransportStreamEventArgs { TsPid = generatingPid };
            handler(this, args);
        }
    }
}