using System.Collections.Generic;
using TsDecoder.TransportStream;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace TsDecoder.Tables
{

    public class ServiceDescriptionTable : Table
    {
        public ushort TransportStreamId { get; set; }
        public byte VersionNumber { get; set; }
        public bool CurrentNextIndicator { get; set; }
        public byte SectionNumber { get; set; }
        public byte LastSectionNumber { get; set; }
        public ushort OriginalNetworkId { get; set; }
        public List<ServiceDescriptionItem> Items { get; set; }
    }

    public class ServiceDescriptionItem
    {
        public ushort ServiceId;
        public bool EitScheduleFlag;
        public bool EitPresentFollowingFlag;
        public byte RunningStatus;
        public bool FreeCaMode;
        public ushort DescriptorsLoopLength;
        public List<Descriptor> Descriptors { get; set; }
        public byte DvbDescriptorTag;
        public byte DescriptorLength;
    }
}
