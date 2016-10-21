using System.Collections.Generic;
using TsAnalyser.TsElements;

namespace TsAnalyser.Tables
{
    public class ProgramMapTable : Table
    {
        public class EsInfo
        {
            public byte StreamType { get; set; }//	96 + varA	8	uimsbf
            public byte Reserved { get; set; }//	104 + varA	3	bslbf
            public short ElementaryPid { get; set; }//	107 + varA	13	uimsbf
            public byte Reserved2 { get; set; }//	120 + varA	4	bslbf
            public ushort EsInfoLength { get; set; }//	124 + varA	12	uimsbf
            public IEnumerable<Descriptor> Descriptors { get; set; }

            //public string StreamTypeString
            //{
            //    get
            //    {
            //        if (StreamType <= 0x0E)
            //        {
            //            switch (StreamType)
            //            {
            //                case 0x00: return "ITU-T | ISO/IEC reserved";
            //                case 0x01: return "ISO/IEC 11172-2 Video";
            //                case 0x02: return "ITU-T Rec. H262 | ISO/IEC 13818-2 Video or ISO/IEC 11172-2 constrained parameter video stream";
            //                case 0x03: return "ISO/IEC 11172-3 Audio";
            //                case 0x04: return "ISO/IEC 13818-3 Audio";
            //                case 0x05: return "ITU-R Rec. H.222.0 | ISO/IEC 13818-1 private_sections";
            //                case 0x06: return "ITU-R Rec. H.222.0 | ISO/IEC 13818-1 PES packets containing private data";
            //                case 0x07: return "ISO/IEC 13522 MHEG";
            //                case 0x08: return "Annex A - DSM CC";
            //                case 0x09: return "ITU-T Rec. H222.1";
            //                case 0x0A: return "ISO/IEC 13818-6 type A";
            //                case 0x0B: return "ISO/IEC 13818-6 type B";
            //                case 0x0C: return "ISO/IEC 13818-6 type C";
            //                case 0x0D: return "ISO/IEC 13818-6 type D";
            //                case 0x0E: return "ISO/IEC 13818-1 auxiliary";
            //                default: return "Strange, > 0x0E && < 0x0F";
            //            }
            //        }
            //        else if (StreamType >= 0x0F && StreamType <= 0x7F)
            //        {
            //            return "ITU-T Rec H.222.0 | ISO/IEC 13818-1 reserved";
            //        }
            //        else
            //        {
            //            return "User private";
            //        }

            //    }
            //}
        }

        public static Dictionary<int, string> ElementaryStreamTypeDescriptions = new Dictionary<int, string>()
        {
            { 0, "Reserved" },
            {1,"ISO/IEC 11172-2 (MPEG-1 video) in a packetized stream" },
            {2,"ITU-T Rec. H.262 (MPEG-2 higher rate interlaced video) in a packetized stream"},
            {3,"ISO/IEC 11172-3 (MPEG-1 audio) in a packetized stream"},
            {4,"ISO/IEC 13818-3 (MPEG-2 halved sample rate audio) in a packetized stream"},
            {5,"ITU-T Rec. H.222 and ISO/IEC 13818-1 (MPEG-2 tabled data) privately defined"},
            {6,"ITU-T Rec. H.222 and ISO/IEC 13818-1 (MPEG-2 packetized data) privately defined (i.e., DVB subtitles/VBI and AC-3)"},
            {7,"ISO/IEC 13522 (MHEG) in a packetized stream"},
            {8,"ITU-T Rec. H.222 and ISO/IEC 13818-1 DSM CC in a packetized stream"},
            {9,"ITU-T Rec. H.222 and ISO/IEC 13818-1/11172-1 auxiliary data in a packetized stream"},
            {10,"ISO/IEC 13818-6 DSM CC multiprotocol encapsulation"},
            {11, "ISO/IEC 13818-6 DSM CC U-N messages"},
            {12, "ISO/IEC 13818-6 DSM CC stream descriptors"},
            {13, "ISO/IEC 13818-6 DSM CC tabled data"},
            {14,"ISO/IEC 13818-1 auxiliary data in a packetized stream"},
            {15,"ISO/IEC 13818-7 ADTS AAC (MPEG-2 lower bit-rate audio) in a packetized stream"},
            {16,"ISO/IEC 14496-2 (MPEG-4 H.263 based video) in a packetized stream"},
            {17,"ISO/IEC 14496-3 (MPEG-4 LOAS multi-format framed audio) in a packetized stream"},
            {18,"ISO/IEC 14496-1 (MPEG-4 FlexMux) in a packetized stream"},
            {19,"ISO/IEC 14496-1 (MPEG-4 FlexMux) in ISO/IEC 14496 tables"},
            {20, "ISO/IEC 13818-6 DSM CC synchronized download protocol"},
            {21, "Packetized metadata"},
            {22, "Sectioned metadata"},
            {23, "ISO/IEC 13818-6 DSM CC Data Carousel metadata"},
            {24, "ISO/IEC 13818-6 DSM CC Object Carousel metadata"},
            {25, "ISO/IEC 13818-6 Synchronized Download Protocol metadata"},
            {26, "ISO/IEC 13818-11 IPMP"},
            {27,"ITU-T Rec. H.264 and ISO/IEC 14496-10 (lower bit-rate video) in a packetized stream"},
            {36,"ITU-T Rec. H.265 and ISO/IEC 23008-2 (Ultra HD video) in a packetized stream"},
            {66,"Chinese Video Standard in a packetized stream"},
            {128,"ITU-T Rec. H.262 and ISO/IEC 13818-2 for DigiCipher II or PCM audio for Blu-ray in a packetized stream"},
            {129,"Dolby Digital up to six channel audio for ATSC and Blu-ray in a packetized stream"},
            {130,"SCTE subtitle or DTS 6 channel audio for Blu-ray in a packetized stream"},
            {131,"Dolby TrueHD lossless audio for Blu-ray in a packetized stream"},
            {132,"Dolby Digital Plus up to 16 channel audio for Blu-ray in a packetized stream"},
            {133,"DTS 8 channel audio for Blu-ray in a packetized stream"},
            {134,"DTS 8 channel lossless audio for Blu-ray in a packetized stream"},
            {135,"Dolby Digital Plus up to 16 channel audio for ATSC in a packetized stream"},
            {144,"Blu-ray Presentation Graphic Stream (subtitling) in a packetized stream"},
            {145, "ATSC DSM CC Network Resources table"},
            {192,"DigiCipher II text in a packetized stream"},
            {193,"Dolby Digital up to six channel audio with AES-128-CBC data encryption in a packetized stream"},
            {194,"ATSC DSM CC synchronous data or Dolby Digital Plus up to 16 channel audio with AES-128-CBC data encryption in a packetized stream"},
            {207,"ISO/IEC 13818-7 ADTS AAC with AES-128-CBC frame encryption in a packetized stream"},
            {209,"BBC Dirac (Ultra HD video) in a packetized stream"},
            {219,"ITU-T Rec. H.264 and ISO/IEC 14496-10 with AES-128-CBC slice encryption in a packetized stream"},
            {234,"Microsoft Windows Media Video 9 (lower bit-rate video) in a packetized stream"}
        };

        public static Dictionary<int, string> ShortElementaryStreamTypeDescriptions = new Dictionary<int, string>()
        {
            { 0, "Reserved" },
            {1,"MPEG-1 video" },
            {2,"MPEG-2 higher rate interlaced video"},
            {3,"MPEG-1 audio"},
            {4,"MPEG-2 halved sample rate audio"},
            {5,"MPEG-2 tabled data"},
            {6,"MPEG-2 packetized data privately defined"},
            {7,"MHEG"},
            {8,"DSM CC"},
            {9,"Auxiliary data"},
            {10,"DSM CC multiprotocol encapsulation"},
            {11, "DSM CC U-N messages"},
            {12, "DSM CC stream descriptors"},
            {13, "DSM CC tabled data"},
            {14,"ISO/IEC 13818-1 auxiliary data"},
            {15,"ADTS AAC (MPEG-2 lower bit-rate audio)"},
            {16,"MPEG-4 H.263 based video"},
            {17,"MPEG-4 LOAS multi-format framed audio"},
            {18,"MPEG-4 FlexMux"},
            {19,"MPEG-4 FlexMux"},
            {20,"DSM CC synchronized download protocol"},
            {21,"Packetized metadata"},
            {22,"Sectioned metadata"},
            {23,"DSM CC Data Carousel metadata"},
            {24,"DSM CC Object Carousel metadata"},
            {25,"Synchronized Download Protocol metadata"},
            {26,"ISO/IEC 13818-11 IPMP"},
            {27,"H.264 video"},
            {36,"H.265/HEVC video"},
            {66,"Chinese Video Standard"},
            {128,"DigiCipher II or PCM audio for Blu-ray"},
            {129,"Dolby Digital for ATSC and Blu-ray"},
            {130,"SCTE subtitle or DTS 6 channel audio"},
            {131,"Dolby TrueHD lossless audio"},
            {132,"Dolby Digital Plus for BluRay"},
            {133,"DTS 8 channel audio"},
            {134,"DTS 8 channel lossless audio"},
            {135,"Dolby Digital Plus for ATSC"},
            {144,"Blu-ray Presentation Graphic Stream (subtitling)"},
            {145,"ATSC DSM CC Network Resources table"},
            {192,"DigiCipher II text in a packetized stream"},
            {193,"Dolby Digital with data encryption"},
            {194,"ATSC DSM CC data or Dolby D Plus with data encryption"},
            {207,"ADTS AAC with frame encryption"},
            {209,"BBC Dirac (Ultra HD video)"},
            {219,"H.264 with encryption"},
            {234,"Microsoft WMV 9 (lower bit-rate video)"}
        };

        public ushort ProgramNumber;
        public byte VersionNumber;
        public bool CurrentNextIndicator;
        public byte SectionNumber;
        public byte LastSectionNumber;
        public ushort PcrPid { get; set; }
        public ushort ProgramInfoLength;
        public List<Descriptor> Descriptors;
        public List<EsInfo> EsStreams { get; set; }
        
        public ProgramMapTable(TsPacket packet) : base(packet)
        {

        }

        public override bool ProcessTable()
        {
            PointerField = 0;

            //if (packet.PayloadUnitStartIndicator && packet.AdaptationFieldFlag && packet.AdaptationField.TransportPrivateDataFlag)
            {
                PointerField = Data[0];
            }
            PointerField++;

            TableId = Data[PointerField + 0];
            SectionSyntaxIndicator = (Data[PointerField + 1] & 0x80) == 0x80;
            Reserved = (byte)((Data[PointerField + 1] >> 6) & 0x03);
            SectionLength = (ushort)(((Data[PointerField + 1] & 0x3) << 8) + Data[PointerField + 2]);
            ProgramNumber = (ushort)((Data[PointerField + 3] << 8) + Data[PointerField + 4]);
           // Reserved2 = (byte)((data[PointerField + 5] >> 6) & 0x03);
            VersionNumber = (byte)((Data[PointerField + 5] & 0x3E) >> 1);
            CurrentNextIndicator = (Data[PointerField + 5] & 0x01) == 0x01;
            SectionNumber = Data[PointerField + 6];
            LastSectionNumber = Data[PointerField + 7];

          //  Reserved3 = (byte)((data[PointerField + 8] >> 5) & 0x07);
            PcrPid = (ushort)(((Data[PointerField + 8] & 0x1f) << 8) + Data[PointerField + 9]);
           // Reserved4 = (byte)((data[PointerField + 10] >> 4) & 0x0F);
            ProgramInfoLength = (ushort)(((Data[PointerField + 10] & 0x3) << 8) + Data[PointerField + 11]);

            var startOfNextField = (byte)(PointerField + 12);

            var ver = (byte)((Data[PointerField + 5] & 0x3E) >> 1);
            if (VersionNumber == ver && Descriptors != null) return false;

            var descriptors = new List<Descriptor>();
            while (startOfNextField < PointerField + 12 + ProgramInfoLength)
            {
                var des = DescriptorFactory.DescriptorFromTsPacketPayload(Data, startOfNextField);
                descriptors.Add(des);
                startOfNextField += (byte)(des.DescriptorLength + 2);
            }
            Descriptors = descriptors;


            var transportStreamLoopEnd = (byte)(SectionLength);
            var streams = new List<EsInfo>();
            while (startOfNextField < transportStreamLoopEnd)
            {
                var es = new EsInfo();
                es.StreamType = Data[startOfNextField];
                es.Reserved = (byte)((Data[startOfNextField + 1] >> 5) & 0x07);
                es.ElementaryPid = (short)(((Data[startOfNextField + 1] & 0x1f) << 8) + Data[startOfNextField + 2]);
                es.Reserved2 = (byte)((Data[startOfNextField + 3] >> 4) & 0x0F);
                es.EsInfoLength = (ushort)(((Data[startOfNextField + 3] & 0x3) << 8) + Data[startOfNextField + 4]);

                descriptors = new List<Descriptor>();

                startOfNextField = (byte)(startOfNextField + 5);
                var endOfDescriptors = (byte)(startOfNextField + es.EsInfoLength);
                while (startOfNextField < endOfDescriptors)
                {
                    var des = DescriptorFactory.DescriptorFromTsPacketPayload(Data, startOfNextField);
                    descriptors.Add(des);
                    startOfNextField += (byte)(des.DescriptorLength + 2);
                }
                es.Descriptors = descriptors;
                streams.Add(es);

            }
            EsStreams = streams;

            return true;
        }
    }
}
