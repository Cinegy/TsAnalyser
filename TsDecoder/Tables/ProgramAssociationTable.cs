// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace TsDecoder.Tables
{
    public class ProgramAssociationTable : Table
    {
        public short TransportStreamId { get; set; }
        public byte VersionNumber { get; set; }
        public bool CurrentNextIndicator { get; set; }
        public byte SectionNumber { get; set; }
        public byte LastSectionNumber { get; set; }
        public short[] ProgramNumbers { get; set; }
        public short[] Pids { get; set; }
        public int Crc { get; set; }
    }
}