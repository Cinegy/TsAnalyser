using System.Collections.Generic;
using TsDecoder.TransportStream;

namespace TsDecoder.Tables
{
    public class Table : ITable
    {
        public short Pid { get; set; }
        public byte PointerField { get; set; }
        public byte TableId { get; set; }
        public short SectionLength { get; set; }
        public List<Descriptor> Descriptors { get; set; }
    }
}
