using System;
using System.Text;

namespace TsAnalyser.TransportStream
{
    public class Text
    {
        private readonly byte[] _characters;

        public string Value => ToString();

        public Text(Text text)
        {
            if (text == null) return;

            _characters = new byte[text._characters.Length];
            Buffer.BlockCopy(text._characters, 0, _characters, 0, text._characters.Length);
        }

        public Text(byte[] inputChars, int start, int length)
        {
            _characters = new byte[length];

            if (null == inputChars || inputChars.Length <= start || inputChars.Length <= length + start) return;

            try
            {
                Buffer.BlockCopy(inputChars, start, _characters, 0, length);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Overflow copying text chars from input - improve bounds checking! Exception: ", ex.Message);
            }
        }

        public override string ToString()
        {
            if (_characters.Length == 0)
            {
                return "";
            }
            var ret = new byte[_characters.Length];
            var char0 = _characters[0];
            ushort start;
            var characterTable = "ISO-8859-1";
            var ii = 0;
            if (_characters[0] >= 0x20 && _characters[0] <= 0xFF)
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

            for (int i = start; i < _characters.Length; i++)
            {
                var character = _characters[i];
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
                        case 0x8A: ret[ii++] = 10; ret[ii++] = 13; break;
                        case 0x8B: break;
                        case 0x8C: break;
                        case 0x8D: break;
                        case 0x8E: break;
                        case 0x8F: break;
                        // ReSharper disable once RedundantEmptyDefaultSwitchBranch
                        default: break;
                    }
                }
                else if (character != 0)
                {
                    ret[ii++] = _characters[i];
                }
            }

            return Encoding.GetEncoding(characterTable).GetString(Encoding.Convert(Encoding.GetEncoding("ISO-8859-1"), Encoding.GetEncoding(characterTable), ret/*Encoding.UTF8.GetBytes(ret.Substring(start))*/)).Substring(0, ii);
        }
    }
}