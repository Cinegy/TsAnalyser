using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TsAnalyser
{
    public class Descriptor
    {
        public Descriptor(byte[] stream, int start)
        {
            this.DescriptorTag = stream[start];
            this.DescriptorLength = stream[start + 1];
        }
        public byte DescriptorTag { get; set; }
        public byte DescriptorLength { get; set; }
    }

    public class TeletextDescriptor : Descriptor
    {
        public TeletextDescriptor(byte[] stream, int start) : base(stream, start)
        {
            List<Language> languages = new List<Language>();
            int current_pos = start + 2;
            do
            {
                Language lang = new Language();
                lang.SO639LanguageCode = System.Text.Encoding.UTF8.GetString(stream, current_pos, 3);
                lang.TeletextType = (byte)(stream[current_pos + 3] & 0x1f);
                lang.TeletextMagazineNumber = (byte)((stream[current_pos + 3] >> 5) & 0x7);
                lang.TeletextPageNumber = stream[current_pos + 4];

                languages.Add(lang);

                current_pos += 5;

            } while (current_pos < start + 2 + DescriptorLength);
            Languages = languages;
        }
        public class Language
        {
            public String SO639LanguageCode { get; set; }
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
            this.Organization = System.Text.Encoding.UTF8.GetString(stream, start + 2, DescriptorLength);
        }
        public String Organization { get; set; }
    }

    public class StreamIdentifierSescriptor : Descriptor
    {
        public StreamIdentifierSescriptor(byte[] stream, int start) : base(stream, start)
        {
            this.ComponentTag = stream[start + 2];
        }

        public byte ComponentTag { get; set; }
    }

    public class ISO639LanguageDescriptor : Descriptor
    {
        public ISO639LanguageDescriptor(byte[] stream, int start) : base(stream, start)
        {
            this.Language = System.Text.Encoding.UTF8.GetString(stream, start + 2, 3);
            this.AudioType = stream[start + 5];
        }

        public String Language { get; set; }
        public byte AudioType { get; set; }
    }

    public class SubtitlingDescriptor : Descriptor
    {

        public SubtitlingDescriptor(byte[] stream, int start) : base(stream, start)
        {
            List<Language> languages = new List<Language>();
            int current_pos = start + 2;
            do
            {
                Language lang = new Language();
                lang.SO639LanguageCode = System.Text.Encoding.UTF8.GetString(stream, current_pos, 3);
                lang.SubtitlingType = stream[current_pos + 3];
                lang.CompositionPageId = (ushort)((stream[current_pos + 4] << 8) + stream[current_pos + 5]);
                lang.AncillaryPageId = (ushort)((stream[current_pos + 6] << 8) + stream[current_pos + 7]);

                languages.Add(lang);

                current_pos += 8;

            } while (current_pos < start + 2 + DescriptorLength);
            Languages = languages;
        }
        public class Language
        {
            public String SO639LanguageCode { get; set; }
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

    public class DescriptorFactory
    {
        public static Descriptor DescriptorFromTsPacketPayload(byte[] stream, int start)
        {
            switch (stream[start])
            {

                case 0x05: return new RegistrationDescriptor(stream, start);
                case 0x0a: return new ISO639LanguageDescriptor(stream, start);
                case 0x52: return new StreamIdentifierSescriptor(stream, start);
                case 0x56: return new TeletextDescriptor(stream, start);
                case 0x59: return new SubtitlingDescriptor(stream, start);
                case 0x66: return new DataBroadcastIdDescriptor(stream, start);

                default: return new Descriptor(stream, start);
            }
        }
    }
}
