using System;
using System.Collections.Generic;
using System.Text;

namespace TsAnalyser.TsElements
{
    public class Text
    {
        public byte[] Characters { get; set; }
        public string Value { get { return ToString(); } }

        public Text(Text text)
        {
            Characters = new byte[text.Characters.Length];
            if (text != null)
            {
                Buffer.BlockCopy(text.Characters, 0, Characters, 0, text.Characters.Length);
            }
        }

        public Text(byte[] inputChars, int start, int length)
        {
            Characters = new byte[length];
            if (null != inputChars && inputChars.Length > start && inputChars.Length > length + start)
            {
                try
                {
                    Buffer.BlockCopy(inputChars, start, Characters, 0, length);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Overflow copying text chars from input - improve bounds checking! Exception: ", ex.Message);
                }
            }
        }

        public override string ToString()
        {
            if (Characters.Length == 0)
            {
                return "";
            }
            var ret = new byte[Characters.Length];
            var char0 = Characters[0];
            ushort start = 0;
            var characterTable = "ISO-8859-1";
            var ii = 0;
            if (Characters[0] >= 0x20 && Characters[0] <= 0xFF)
            {
                start = 0;
            }
            else
            {
                start = 1;

                switch (char0)
                {
                    case 0x01: characterTable = "ISO-8859-5"; break;
                    case 0x02: characterTable = "ISO-8859-6"; break;
                    case 0x03: characterTable = "ISO-8859-7"; break;
                    case 0x04: characterTable = "ISO-8859-8"; break;
                    case 0x05: characterTable = "ISO-8859-9"; break;
                    case 0x06: characterTable = "ISO-8859-10"; break;
                    case 0x07: characterTable = "ISO-8859-11"; break;
                    case 0x09: characterTable = "ISO-8859-13"; break;
                    case 0x0A: characterTable = "ISO-8859-14"; break;
                    case 0x0B: characterTable = "ISO-8859-15"; break;
                    case 0x10: characterTable = "ISO-8859"; break;
                    case 0x11: characterTable = "ISO-10646"; break;
                    case 0x12: characterTable = "KSX1001-2004"; break;
                    case 0x13: characterTable = "GB-2312-1980"; break;
                    case 0x14: characterTable = "Big5"; break;
                    case 0x15: characterTable = "UTF-8 "; break;
                }
            }

            for (int i = start; i < Characters.Length; i++)
            {
                var character = Characters[i];
                if (character >= 0x80 && character <= 0x9F)
                {
                    switch (character)
                    {
                        case 0x80: break;
                        case 0x81: break;
                        case 0x82: break;
                        case 0x83: break;
                        case 0x84: break;
                        case 0x85: break;
                        case 0x86: break;
                        case 0x87: break;
                        case 0x88: break;
                        case 0x8A: ret[ii++] = 10; ret[ii++] = 13; ; break;
                        case 0x8B: break;
                        case 0x8C: break;
                        case 0x8D: break;
                        case 0x8E: break;
                        case 0x8F: break;
                        default: break;
                    }
                }
                else if (character != 0)
                {
                    ret[ii++] = Characters[i];
                }
                else
                {
                   
                }
            }
            // }


            return Encoding.GetEncoding(characterTable).GetString(Encoding.Convert(Encoding.GetEncoding("ISO-8859-1"), Encoding.GetEncoding(characterTable), ret/*Encoding.UTF8.GetBytes(ret.Substring(start))*/)).Substring(0, ii);
        }
    }


    public class Descriptor
    {
        public Descriptor(byte[] stream, int start)
        {
            DescriptorTag = stream[start];
            DescriptorLength = stream[start + 1];
        }
        public byte DescriptorTag { get; set; }
        public byte DescriptorLength { get; set; }
    }

    public class TeletextDescriptor : Descriptor
    {
        public static Dictionary<byte, string> TeletextTypes = new Dictionary<byte, string>(){
            {0x00 , "reserved for future use"},
            {0x01 ,  "initial Teletext page"},
            {0x02 ,  "Teletext subtitle page"},
            {0x03 ,  "additional information page"},
            {0x04 ,  "programme schedule page"},
            {0x05 ,  "Teletext subtitle page for hearing impaired people"},
            {0x07, "reserved for future use"},
            {0x08, "reserved for future use"},
            {0x09, "reserved for future use"},
            {0x0A, "reserved for future use"},
            {0x0B, "reserved for future use"},
            {0x0C, "reserved for future use"},
            {0x0D, "reserved for future use"},
            {0x0E, "reserved for future use"},
            {0x0F, "reserved for future use"},
            {0x10, "reserved for future use"},
            {0x11, "reserved for future use"},
            {0x12, "reserved for future use"},
            {0x13, "reserved for future use"},
            {0x14, "reserved for future use"},
            {0x15, "reserved for future use"},
            {0x16, "reserved for future use"},
            {0x17, "reserved for future use"},
            {0x18, "reserved for future use"},
            {0x19, "reserved for future use"},
            {0x1A, "reserved for future use"},
            {0x1B, "reserved for future use"},
            {0x1C, "reserved for future use"},
            {0x1D, "reserved for future use"},
            {0x1E, "reserved for future use"},
            {0x1F, "reserved for future use"}
        };
        

        public TeletextDescriptor(byte[] stream, int start)
            : base(stream, start)
        {
            var languages = new List<Language>();
            var currentPos = start + 2;
            do
            {
                var lang = new Language();
                lang.Iso639LanguageCode = Encoding.UTF8.GetString(stream, currentPos, 3);
                // lang.TeletextType = (byte)(stream[current_pos + 3] & 0x1f);
                lang.TeletextType = (byte)((stream[currentPos + 3] >> 3) & 0x01f);
                lang.TeletextMagazineNumber = (byte)((stream[currentPos + 3]) & 0x7);
                //lang.TeletextMagazineNumber = (byte)((stream[current_pos + 3] >> 5) & 0x7);
                lang.TeletextPageNumber = stream[currentPos + 4];

                languages.Add(lang);

                currentPos += 5;

            } while (currentPos < start + 2 + DescriptorLength);
            Languages = languages;
        }
        public class Language
        {
            public Language() { }
            public Language(Language lang)
            {
                Iso639LanguageCode = lang.Iso639LanguageCode;
                TeletextType = lang.TeletextType;
                TeletextMagazineNumber = lang.TeletextMagazineNumber;
                TeletextPageNumber = lang.TeletextPageNumber;
            }

            public string Iso639LanguageCode { get; set; }
            public byte TeletextType { get; set; }
            public byte TeletextMagazineNumber { get; set; }
            public byte TeletextPageNumber { get; set; }
        }
        public IEnumerable<Language> Languages { get; set; }        
    }

    public class RegistrationDescriptor : Descriptor
    {
        public RegistrationDescriptor(byte[] stream, int start) : base(stream, start)
        {
            if ((stream.Length - start - 2) > DescriptorLength)
            {
                Organization = Encoding.UTF8.GetString(stream, start + 2, DescriptorLength);
            }
        }
        public string Organization { get; set; }
    }

    public class StreamIdentifierSescriptor : Descriptor
    {
        public StreamIdentifierSescriptor(byte[] stream, int start) : base(stream, start)
        {
            ComponentTag = stream[start + 2];
        }

        public byte ComponentTag { get; set; }
    }

    public class Iso639LanguageDescriptor : Descriptor
    {
        public Iso639LanguageDescriptor(byte[] stream, int start) : base(stream, start)
        {
            Language = Encoding.UTF8.GetString(stream, start + 2, 3);
            AudioType = stream[start + 5];
        }

        public string Language { get; set; }
        public byte AudioType { get; set; }
    }

    public class SubtitlingDescriptor : Descriptor
    {

        public SubtitlingDescriptor(byte[] stream, int start) : base(stream, start)
        {
            var languages = new List<Language>();
            var currentPos = start + 2;
            do
            {
                var lang = new Language();
                lang.So639LanguageCode = Encoding.UTF8.GetString(stream, currentPos, 3);
                lang.SubtitlingType = stream[currentPos + 3];
                lang.CompositionPageId = (ushort)((stream[currentPos + 4] << 8) + stream[currentPos + 5]);
                lang.AncillaryPageId = (ushort)((stream[currentPos + 6] << 8) + stream[currentPos + 7]);

                languages.Add(lang);

                currentPos += 8;

            } while (currentPos < start + 2 + DescriptorLength);
            Languages = languages;
        }
        public class Language
        {
            public string So639LanguageCode { get; set; }
            public byte SubtitlingType { get; set; }
            public ushort CompositionPageId { get; set; }
            public ushort AncillaryPageId { get; set; }
        }
        public IEnumerable<Language> Languages { get; set; }
    }

    public class DataBroadcastIdDescriptor : Descriptor
    {
        public DataBroadcastIdDescriptor(byte[] stream, int start) : base(stream, start)
        {
            DataBroadcastId = (ushort)((stream[start + 2] << 8) + stream[start + 3]);
        }

        public ushort DataBroadcastId { get; set; }
    }

    public class ServiceListDescriptor : Descriptor
    {
        public static string ServiceTypeDescription(byte serviceType)
        {
            if (serviceType <= 0x0C)
            {
                switch (serviceType)
                {
                    case 0x00: return "reserved for future use";
                    case 0x01: return "digital television service";
                    case 0x02: return "digital radio sound service";
                    case 0x03: return "teletext service";
                    case 0x04: return "NVOD reference service";
                    case 0x05: return "NVOD time-shifted service";
                    case 0x06: return "mosaic service";
                    case 0x07: return "PAL coded signal";
                    case 0x08: return "SECAM coded signal";
                    case 0x09: return "D/D2-MAC";
                    case 0x0A: return "FM Radio";
                    case 0x0B: return "NTSC coded signal";
                    case 0x0C: return "data broadcast service";
                }
            }
            else if (serviceType >= 0x0D && serviceType <= 0x7F)
            {
                return "reserved for future use";
            }
            else if (serviceType >= 0x80 && serviceType <= 0xFE)
            {
                return "user defined";
            }

            return "Forbidden";
        }

        public class Service
        {
            public Service() { }
            public Service(Service service)
            {
                ServiceId = service.ServiceId;
                ServiceType = service.ServiceType;
            }

            public ushort ServiceId { get; set; }
            public byte ServiceType { get; set; }
            public string ServiceTypeString { get { return ServiceTypeDescription(ServiceType); } }
        }
        
        public ServiceListDescriptor(byte[] stream, int start) : base(stream, start)
        {
            var services = new List<Service>();
            var startOfNextBlock = (ushort)(start + 2);
            while (startOfNextBlock < (start + DescriptorLength + 2))
            {
                var service = new Service();

                service.ServiceId = (ushort)((stream[startOfNextBlock] << 8) + stream[startOfNextBlock + 1]);
                service.ServiceType = stream[startOfNextBlock + 2];

                startOfNextBlock += 3;

                services.Add(service);
            }
            Services = services;
        }
        public IEnumerable<Service> Services { get; set; }

    }

    public class ServiceDescriptor : Descriptor
    {        
        public static string GetServiceTypeDescription(byte serviceType)
        {
            switch (serviceType)
            {
                case 0x00: return "reserved for future use";
                case 0x01: return "digital television service (see note 1)";
                case 0x02: return "digital radio sound service (see note 2)";
                case 0x03: return "Teletext service";
                case 0x04: return "NVOD reference service (see note 1)";
                case 0x05: return "NVOD time-shifted service (see note 1)";
                case 0x06: return "mosaic service";
                case 0x07: return "FM radio service";
                case 0x08: return "DVB SRM service [49]";
                case 0x09: return "reserved for future use";
                case 0x0A: return "advanced codec digital radio sound service";
                case 0x0B: return "H.264/AVC mosaic service";
                case 0x0C: return "data broadcast service";
                case 0x0D: return "reserved for Common Interface Usage (EN 50221[37])";
                case 0x0E: return "RCS Map (see EN301790[7])";
                case 0x0F: return "RCS FLS (see EN301790[7])";
                case 0x10: return "DVB MHP service 0x11 MPEG-2 HD digital television service";
                case 0x16: return "H.264/AVC SD digital television service";
                case 0x17: return "H.264/AVC SD NVOD time-shifted service";
                case 0x18: return "H.264/AVC SD NVOD reference service";
                case 0x19: return "H.264/AVC HD digital television service";
                case 0x1A: return "H.264/AVC HD NVOD time-shifted service";
                case 0x1B: return "H.264/AVC HD NVOD reference service";
                case 0x1C: return "H.264/AVC frame compatible plano-stereoscopic HD digital television service (see note 3)";
                case 0x1D: return "H.264/AVC frame compatible plano-stereoscopic HD NVOD time-shifted service (see note 3)";
                case 0x1E: return "H.264/AVC frame compatible plano-stereoscopic HD NVOD reference service (see note 3)";
                case 0x1F: return "HEVC digital television service";
                case 0xFF: return "reserved for future use";
            }
            if (serviceType >= 0x20 || serviceType <= 0x7F)
            {
                return "reserved for future use";
            }
            else if (serviceType >= 0x80 || serviceType <= 0xFE)
            {
                return "user defined";
            }
            else if (serviceType >= 0x12 || serviceType <= 0x15)
            {
                return "reserved for future use";
            }
            return "unknown";
        }

        public ServiceDescriptor(byte[] stream, int start) : base(stream, start)
        {
            ServiceType = stream[start + 2];
            ServiceProviderNameLength = stream[start + 3];
            ServiceProviderName = new Text(stream, start + 4, ServiceProviderNameLength); // new Text(System.Text.Encoding.UTF8.GetString(stream, start + 4, ServiceProviderNameLength));
            ServiceNameLength = stream[start + 4 + ServiceProviderNameLength];
            ServiceName = new Text(stream, start + 4 + ServiceProviderNameLength + 1, ServiceNameLength);//new Text(System.Text.Encoding.UTF8.GetString(stream, start + 4 + ServiceProviderNameLength + 1, ServiceNameLength));
        }
        public byte ServiceType { get; set; }//8 uimsbf
        public string ServiceTypeDescription { get { return GetServiceTypeDescription(ServiceType); } }
        public byte ServiceProviderNameLength { get; set; }// 8 uimsbf
        public Text ServiceProviderName { get; set; }// 
        public byte ServiceNameLength { get; set; }// 8 uimsbf 
        public Text ServiceName { get; set; }              
    }

    public class DescriptorFactory
    {
        public static Descriptor DescriptorFromTsPacketPayload(byte[] stream, int start)
        {
            switch (stream[start])
            {

                case 0x05: return new RegistrationDescriptor(stream, start);
                case 0x0a: return new Iso639LanguageDescriptor(stream, start);               
                case 0x41: return new ServiceListDescriptor(stream, start);
                case 0x48: return new ServiceDescriptor(stream, start);
                case 0x52: return new StreamIdentifierSescriptor(stream, start);
                case 0x56: return new TeletextDescriptor(stream, start);
                case 0x59: return new SubtitlingDescriptor(stream, start);
                case 0x66: return new DataBroadcastIdDescriptor(stream, start);

                default: return new Descriptor(stream, start);
            }
        }
    }
}
