using System.Collections.Generic;
using TsAnalyser.TransportStream;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace TsAnalyser.Tables
{
    public class ProgramMapTable : Table
    {
        public ushort ProgramNumber { get; set; }
        public byte VersionNumber { get; set; }
        public bool CurrentNextIndicator { get; set; }
        public byte SectionNumber { get; set; }
        public byte LastSectionNumber { get; set; }
        public ushort PcrPid { get; set; }
        public ushort ProgramInfoLength { get; set; }
        public List<EsInfo> EsStreams { get; set; }
    }
}
