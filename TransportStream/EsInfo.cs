using System.Collections.Generic;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace TsAnalyser.TransportStream
{
    public class EsInfo
    {
        public byte StreamType { get; set; }
        public short ElementaryPid { get; set; }
        public ushort EsInfoLength { get; set; }
        public IEnumerable<Descriptor> Descriptors { get; set; }
    }
}