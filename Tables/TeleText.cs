using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TsAnalyser.Tables
{
    public class TeleText
    {
        public class TeleTextSubtitleEventArgs : EventArgs
        {
            public String[] Page { get; set; }
            public UInt16 PageNumber { get; set; }
            public Int16 PID { get; set; }

            public TeleTextSubtitleEventArgs(String[] page, UInt16 pageNumber, Int16 pid)
            {
                this.Page = new String[page.Length];

                for (int i = 0; i < page.Length; i++)
                {
                    this.Page[i] = page[i];
                }

                this.PageNumber = pageNumber;
                this.PID = pid;
            }
        }

        private class TeleTextUtils
        {
            public const byte UNDEF = 0xff;

            public struct charset
            {
                public byte current;
                public byte g0_m29;
                public byte g0_x28;
            }
            public charset primary_charset = new charset
            {
                current = 0x00,
                g0_m29 = UNDEF,
                g0_x28 = UNDEF
            };

            public TeleTextUtils()
            {

            }


            public static byte[] REVERSE_BYTE = {
                                    0x00, 0x80, 0x40, 0xc0, 0x20, 0xa0, 0x60, 0xe0, 0x10, 0x90, 0x50, 0xd0, 0x30, 0xb0, 0x70, 0xf0,
                                    0x08, 0x88, 0x48, 0xc8, 0x28, 0xa8, 0x68, 0xe8, 0x18, 0x98, 0x58, 0xd8, 0x38, 0xb8, 0x78, 0xf8,
                                    0x04, 0x84, 0x44, 0xc4, 0x24, 0xa4, 0x64, 0xe4, 0x14, 0x94, 0x54, 0xd4, 0x34, 0xb4, 0x74, 0xf4,
                                    0x0c, 0x8c, 0x4c, 0xcc, 0x2c, 0xac, 0x6c, 0xec, 0x1c, 0x9c, 0x5c, 0xdc, 0x3c, 0xbc, 0x7c, 0xfc,
                                    0x02, 0x82, 0x42, 0xc2, 0x22, 0xa2, 0x62, 0xe2, 0x12, 0x92, 0x52, 0xd2, 0x32, 0xb2, 0x72, 0xf2,
                                    0x0a, 0x8a, 0x4a, 0xca, 0x2a, 0xaa, 0x6a, 0xea, 0x1a, 0x9a, 0x5a, 0xda, 0x3a, 0xba, 0x7a, 0xfa,
                                    0x06, 0x86, 0x46, 0xc6, 0x26, 0xa6, 0x66, 0xe6, 0x16, 0x96, 0x56, 0xd6, 0x36, 0xb6, 0x76, 0xf6,
                                    0x0e, 0x8e, 0x4e, 0xce, 0x2e, 0xae, 0x6e, 0xee, 0x1e, 0x9e, 0x5e, 0xde, 0x3e, 0xbe, 0x7e, 0xfe,
                                    0x01, 0x81, 0x41, 0xc1, 0x21, 0xa1, 0x61, 0xe1, 0x11, 0x91, 0x51, 0xd1, 0x31, 0xb1, 0x71, 0xf1,
                                    0x09, 0x89, 0x49, 0xc9, 0x29, 0xa9, 0x69, 0xe9, 0x19, 0x99, 0x59, 0xd9, 0x39, 0xb9, 0x79, 0xf9,
                                    0x05, 0x85, 0x45, 0xc5, 0x25, 0xa5, 0x65, 0xe5, 0x15, 0x95, 0x55, 0xd5, 0x35, 0xb5, 0x75, 0xf5,
                                    0x0d, 0x8d, 0x4d, 0xcd, 0x2d, 0xad, 0x6d, 0xed, 0x1d, 0x9d, 0x5d, 0xdd, 0x3d, 0xbd, 0x7d, 0xfd,
                                    0x03, 0x83, 0x43, 0xc3, 0x23, 0xa3, 0x63, 0xe3, 0x13, 0x93, 0x53, 0xd3, 0x33, 0xb3, 0x73, 0xf3,
                                    0x0b, 0x8b, 0x4b, 0xcb, 0x2b, 0xab, 0x6b, 0xeb, 0x1b, 0x9b, 0x5b, 0xdb, 0x3b, 0xbb, 0x7b, 0xfb,
                                    0x07, 0x87, 0x47, 0xc7, 0x27, 0xa7, 0x67, 0xe7, 0x17, 0x97, 0x57, 0xd7, 0x37, 0xb7, 0x77, 0xf7,
                                    0x0f, 0x8f, 0x4f, 0xcf, 0x2f, 0xaf, 0x6f, 0xef, 0x1f, 0x9f, 0x5f, 0xdf, 0x3f, 0xbf, 0x7f, 0xff
                                };
            static byte[] PARITY_BYTE = {
                                    0x00, 0x01, 0x01, 0x00, 0x01, 0x00, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x01, 0x00,
                                    0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x01, 0x00, 0x01, 0x00, 0x00, 0x01,
                                    0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x01, 0x00, 0x01, 0x00, 0x00, 0x01,
                                    0x00, 0x01, 0x01, 0x00, 0x01, 0x00, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x01, 0x00,
                                    0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x01, 0x00, 0x01, 0x00, 0x00, 0x01,
                                    0x00, 0x01, 0x01, 0x00, 0x01, 0x00, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x01, 0x00,
                                    0x00, 0x01, 0x01, 0x00, 0x01, 0x00, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x01, 0x00,
                                    0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x01, 0x00, 0x01, 0x00, 0x00, 0x01,
                                    0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x01, 0x00, 0x01, 0x00, 0x00, 0x01,
                                    0x00, 0x01, 0x01, 0x00, 0x01, 0x00, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x01, 0x00,
                                    0x00, 0x01, 0x01, 0x00, 0x01, 0x00, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x01, 0x00,
                                    0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x01, 0x00, 0x01, 0x00, 0x00, 0x01,
                                    0x00, 0x01, 0x01, 0x00, 0x01, 0x00, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x01, 0x00,
                                    0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x01, 0x00, 0x01, 0x00, 0x00, 0x01,
                                    0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x01, 0x00, 0x01, 0x00, 0x00, 0x01,
                                    0x00, 0x01, 0x01, 0x00, 0x01, 0x00, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x01, 0x00
                                };



            static byte[] UN_HAM_8_4 = {
                                    0x01, 0xff, 0x01, 0x01, 0xff, 0x00, 0x01, 0xff, 0xff, 0x02, 0x01, 0xff, 0x0a, 0xff, 0xff, 0x07,/*15*/
	                                0xff, 0x00, 0x01, 0xff, 0x00, 0x00, 0xff, 0x00, 0x06, 0xff, 0xff, 0x0b, 0xff, 0x00, 0x03, 0xff,/*31*/
	                                0xff, 0x0c, 0x01, 0xff, 0x04, 0xff, 0xff, 0x07, 0x06, 0xff, 0xff, 0x07, 0xff, 0x07, 0x07, 0x07,/*47*/
	                                0x06, 0xff, 0xff, 0x05, 0xff, 0x00, 0x0d, 0xff, 0x06, 0x06, 0x06, 0xff, 0x06, 0xff, 0xff, 0x07,/*63*/
	                                0xff, 0x02, 0x01, 0xff, 0x04, 0xff, 0xff, 0x09, 0x02, 0x02, 0xff, 0x02, 0xff, 0x02, 0x03, 0xff,/*79*/
	                                0x08, 0xff, 0xff, 0x05, 0xff, 0x00, 0x03, 0xff, 0xff, 0x02, 0x03, 0xff, 0x03, 0xff, 0x03, 0x03,/*95*/
	                                0x04, 0xff, 0xff, 0x05, 0x04, 0x04, 0x04, 0xff, 0xff, 0x02, 0x0f, 0xff, 0x04, 0xff, 0xff, 0x07,/*111*/
	                                0xff, 0x05, 0x05, 0x05, 0x04, 0xff, 0xff, 0x05, 0x06, 0xff, 0xff, 0x05, 0xff, 0x0e, 0x03, 0xff,/*127*/
	                                0xff, 0x0c, 0x01, 0xff, 0x0a, 0xff, 0xff, 0x09, 0x0a, 0xff, 0xff, 0x0b, 0x0a, 0x0a, 0x0a, 0xff,/*143*/
	                                0x08, 0xff, 0xff, 0x0b, 0xff, 0x00, 0x0d, 0xff, 0xff, 0x0b, 0x0b, 0x0b, 0x0a, 0xff, 0xff, 0x0b,/*159*/
	                                0x0c, 0x0c, 0xff, 0x0c, 0xff, 0x0c, 0x0d, 0xff, 0xff, 0x0c, 0x0f, 0xff, 0x0a, 0xff, 0xff, 0x07,/*175*/
	                                0xff, 0x0c, 0x0d, 0xff, 0x0d, 0xff, 0x0d, 0x0d, 0x06, 0xff, 0xff, 0x0b, 0xff, 0x0e, 0x0d, 0xff,/*191*/
	                                0x08, 0xff, 0xff, 0x09, 0xff, 0x09, 0x09, 0x09, 0xff, 0x02, 0x0f, 0xff, 0x0a, 0xff, 0xff, 0x09,/*207*/
	                                0x08, 0x08, 0x08, 0xff, 0x08, 0xff, 0xff, 0x09, 0x08, 0xff, 0xff, 0x0b, 0xff, 0x0e, 0x03, 0xff,/*223*/
	                                0xff, 0x0c, 0x0f, 0xff, 0x04, 0xff, 0xff, 0x09, 0x0f, 0xff, 0x0f, 0x0f, 0xff, 0x0e, 0x0f, 0xff,/*239*/
	                                0x08, 0xff, 0xff, 0x05, 0xff, 0x0e, 0x0d, 0xff, 0xff, 0x0e, 0x0f, 0xff, 0x0e, 0x0e, 0xff, 0x0e/*255*/
                                };

            
            UInt16[][] G0 = {
                                    new UInt16[]{ //ETS 300 706, 15.6.1 Latin G0 Primary Set 
		                                0x0020, 0x0021, 0x0022, 0x00a3, 0x0024, 0x0025, 0x0026, 0x0027, 0x0028, 0x0029, 0x002a, 0x002b, 0x002c, 0x002d, 0x002e, 0x002f,
                                        0x0030, 0x0031, 0x0032, 0x0033, 0x0034, 0x0035, 0x0036, 0x0037, 0x0038, 0x0039, 0x003a, 0x003b, 0x003c, 0x003d, 0x003e, 0x003f,
                                        0x0040, 0x0041, 0x0042, 0x0043, 0x0044, 0x0045, 0x0046, 0x0047, 0x0048, 0x0049, 0x004a, 0x004b, 0x004c, 0x004d, 0x004e, 0x004f,
                                        0x0050, 0x0051, 0x0052, 0x0053, 0x0054, 0x0055, 0x0056, 0x0057, 0x0058, 0x0059, 0x005a, 0x00ab, 0x00bd, 0x00bb, 0x005e, 0x0023,
                                        0x002d, 0x0061, 0x0062, 0x0063, 0x0064, 0x0065, 0x0066, 0x0067, 0x0068, 0x0069, 0x006a, 0x006b, 0x006c, 0x006d, 0x006e, 0x006f,
                                        0x0070, 0x0071, 0x0072, 0x0073, 0x0074, 0x0075, 0x0076, 0x0077, 0x0078, 0x0079, 0x007a, 0x00bc, 0x00a6, 0x00be, 0x00f7, 0x007f
                                    },
                                    new UInt16[]{ //ETS 300 706, 15.6.4 Cyrillic G0 Primary Set - Option 1 - Serbian/Croatian
		                                0x0020, 0x0021, 0x0022, 0x0023, 0x0024, 0x0025, 0x044b, 0x0027, 0x0028, 0x0029, 0x002a, 0x002b, 0x002c, 0x002d, 0x002e, 0x002f,
                                        0x0030, 0x0031, 0x3200, 0x0033, 0x0034, 0x0035, 0x0036, 0x0037, 0x0038, 0x0039, 0x003a, 0x003b, 0x003c, 0x003d, 0x003e, 0x003f,
                                        0x0427, 0x0410, 0x0411, 0x0426, 0x0414, 0x0415, 0x0424, 0x0413, 0x0425, 0x0418, 0x0408, 0x041a, 0x041b, 0x041c, 0x041d, 0x041e,
                                        0x041f, 0x040c, 0x0420, 0x0421, 0x0422, 0x0423, 0x0412, 0x0403, 0x0409, 0x040a, 0x0417, 0x040b, 0x0416, 0x0402, 0x0428, 0x040f,
                                        0x0447, 0x0430, 0x0431, 0x0446, 0x0434, 0x0435, 0x0444, 0x0433, 0x0445, 0x0438, 0x0428, 0x043a, 0x043b, 0x043c, 0x043d, 0x043e,
                                        0x043f, 0x042c, 0x0440, 0x0441, 0x0442, 0x0443, 0x0432, 0x0423, 0x0429, 0x042a, 0x0437, 0x042b, 0x0436, 0x0422, 0x0448, 0x042f
                                    },
                                    new UInt16[]{ //ETS 300 706, 15.6.5 Cyrillic G0 Primary Set - Option 2 - Russian/Bulgarian
		                                0x0020, 0x0021, 0x0022, 0x0023, 0x0024, 0x0025, 0x044b, 0x0027, 0x0028, 0x0029, 0x002a, 0x002b, 0x002c, 0x002d, 0x002e, 0x002f,
                                        0x0030, 0x0031, 0x0032, 0x0033, 0x0034, 0x0035, 0x0036, 0x0037, 0x0038, 0x0039, 0x003a, 0x003b, 0x003c, 0x003d, 0x003e, 0x003f,
                                        0x042e, 0x0410, 0x0411, 0x0426, 0x0414, 0x0415, 0x0424, 0x0413, 0x0425, 0x0418, 0x0419, 0x041a, 0x041b, 0x041c, 0x041d, 0x041e,
                                        0x041f, 0x042f, 0x0420, 0x0421, 0x0422, 0x0423, 0x0416, 0x0412, 0x042c, 0x042a, 0x0417, 0x0428, 0x042d, 0x0429, 0x0427, 0x042b,
                                        0x044e, 0x0430, 0x0431, 0x0446, 0x0434, 0x0435, 0x0444, 0x0433, 0x0445, 0x0438, 0x0439, 0x043a, 0x043b, 0x043c, 0x043d, 0x043e,
                                        0x043f, 0x044f, 0x0440, 0x0441, 0x0442, 0x0443, 0x0436, 0x0432, 0x044c, 0x044a, 0x0437, 0x0448, 0x044d, 0x0449, 0x0447, 0x044b
                                    },
                                    new UInt16[]{ //ETS 300 706, 15.6.6 Cyrillic G0 Primary Set - Option 3 - Ukrainian
		                                0x0020, 0x0021, 0x0022, 0x0023, 0x0024, 0x0025, 0x00ef, 0x0027, 0x0028, 0x0029, 0x002a, 0x002b, 0x002c, 0x002d, 0x002e, 0x002f,
                                        0x0030, 0x0031, 0x0032, 0x0033, 0x0034, 0x0035, 0x0036, 0x0037, 0x0038, 0x0039, 0x003a, 0x003b, 0x003c, 0x003d, 0x003e, 0x003f,
                                        0x042e, 0x0410, 0x0411, 0x0426, 0x0414, 0x0415, 0x0424, 0x0413, 0x0425, 0x0418, 0x0419, 0x041a, 0x041b, 0x041c, 0x041d, 0x041e,
                                        0x041f, 0x042f, 0x0420, 0x0421, 0x0422, 0x0423, 0x0416, 0x0412, 0x042c, 0x0049, 0x0417, 0x0428, 0x042d, 0x0429, 0x0427, 0x00cf,
                                        0x044e, 0x0430, 0x0431, 0x0446, 0x0434, 0x0435, 0x0444, 0x0433, 0x0445, 0x0438, 0x0439, 0x043a, 0x043b, 0x043c, 0x043d, 0x043e,
                                        0x043f, 0x044f, 0x0440, 0x0441, 0x0442, 0x0443, 0x0436, 0x0432, 0x044c, 0x0069, 0x0437, 0x0448, 0x044d, 0x0449, 0x0447, 0x00ff
                                    },
                                    new UInt16[]{ //ETS 300 706, 15.6.8 Greek G0 Primary Set
		                                0x0020, 0x0021, 0x0022, 0x0023, 0x0024, 0x0025, 0x0026, 0x0027, 0x0028, 0x0029, 0x002a, 0x002b, 0x002c, 0x002d, 0x002e, 0x002f,
                                        0x0030, 0x0031, 0x0032, 0x0033, 0x0034, 0x0035, 0x0036, 0x0037, 0x0038, 0x0039, 0x003a, 0x003b, 0x003c, 0x003d, 0x003e, 0x003f,
                                        0x0390, 0x0391, 0x0392, 0x0393, 0x0394, 0x0395, 0x0396, 0x0397, 0x0398, 0x0399, 0x039a, 0x039b, 0x039c, 0x039d, 0x039e, 0x039f,
                                        0x03a0, 0x03a1, 0x03a2, 0x03a3, 0x03a4, 0x03a5, 0x03a6, 0x03a7, 0x03a8, 0x03a9, 0x03aa, 0x03ab, 0x03ac, 0x03ad, 0x03ae, 0x03af,
                                        0x03b0, 0x03b1, 0x03b2, 0x03b3, 0x03b4, 0x03b5, 0x03b6, 0x03b7, 0x03b8, 0x03b9, 0x03ba, 0x03bb, 0x03bc, 0x03bd, 0x03be, 0x03bf,
                                        0x03c0, 0x03c1, 0x03c2, 0x03c3, 0x03c4, 0x03c5, 0x03c6, 0x03c7, 0x03c8, 0x03c9, 0x03ca, 0x03cb, 0x03cc, 0x03cd, 0x03ce, 0x03cf
                                    }
	                                //{ //ETS 300 706, 15.6.10 Arabic G0 Primary Set
	                                //},
	                                //{ //ETS 300 706, 15.6.12 Hebrew G0 Primary Set
	                                //}
                            };

            public UInt16[][] G2 = {
                                    new UInt16[] { //ETS 300 706, 15.6.3 Latin G2 Supplementary Set
		                                0x0020, 0x00a1, 0x00a2, 0x00a3, 0x0024, 0x00a5, 0x0023, 0x00a7, 0x00a4, 0x2018, 0x201c, 0x00ab, 0x2190, 0x2191, 0x2192, 0x2193,
                                        0x00b0, 0x00b1, 0x00b2, 0x00b3, 0x00d7, 0x00b5, 0x00b6, 0x00b7, 0x00f7, 0x2019, 0x201d, 0x00bb, 0x00bc, 0x00bd, 0x00be, 0x00bf,
                                        0x0020, 0x0300, 0x0301, 0x0302, 0x0303, 0x0304, 0x0306, 0x0307, 0x0308, 0x0000, 0x030a, 0x0327, 0x005f, 0x030b, 0x0328, 0x030c,
                                        0x2015, 0x00b9, 0x00ae, 0x00a9, 0x2122, 0x266a, 0x20ac, 0x2030, 0x03B1, 0x0000, 0x0000, 0x0000, 0x215b, 0x215c, 0x215d, 0x215e,
                                        0x03a9, 0x00c6, 0x0110, 0x00aa, 0x0126, 0x0000, 0x0132, 0x013f, 0x0141, 0x00d8, 0x0152, 0x00ba, 0x00de, 0x0166, 0x014a, 0x0149,
                                        0x0138, 0x00e6, 0x0111, 0x00f0, 0x0127, 0x0131, 0x0133, 0x0140, 0x0142, 0x00f8, 0x0153, 0x00df, 0x00fe, 0x0167, 0x014b, 0x0020
                                    }/*,
                                    { //ETS 300 706, 15.6.7 Cyrillic G2 Supplementary Set
                                        
                                	},
                                	{ //ETS 300 706, 15.6.9 Greek G2 Supplementary Set
                                	},
                                	{ //ETS 300 706, 15.6.11 Arabic G2 Supplementary Set
                                	}*/
                            };

            public UInt16[][] G2_ACCENTS = {
	                                        // A B C D E F G H I J K L M N O P Q R S T U V W X Y Z a b c d e f g h i j k l m n o p q r s t u v w x y z
	                                        new UInt16[]{ // grave
		                                        0x00c0, 0x0000, 0x0000, 0x0000, 0x00c8, 0x0000, 0x0000, 0x0000, 0x00cc, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x00d2, 0x0000,
                                                0x0000, 0x0000, 0x0000, 0x0000, 0x00d9, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x00e0, 0x0000, 0x0000, 0x0000, 0x00e8, 0x0000,
                                                0x0000, 0x0000, 0x00ec, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x00f2, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x00f9, 0x0000,
                                                0x0000, 0x0000, 0x0000, 0x0000
                                            },
                                            new UInt16[]{ // acute
		                                        0x00c1, 0x0000, 0x0106, 0x0000, 0x00c9, 0x0000, 0x0000, 0x0000, 0x00cd, 0x0000, 0x0000, 0x0139, 0x0000, 0x0143, 0x00d3, 0x0000,
                                                0x0000, 0x0154, 0x015a, 0x0000, 0x00da, 0x0000, 0x0000, 0x0000, 0x00dd, 0x0179, 0x00e1, 0x0000, 0x0107, 0x0000, 0x00e9, 0x0000,
                                                0x0123, 0x0000, 0x00ed, 0x0000, 0x0000, 0x013a, 0x0000, 0x0144, 0x00f3, 0x0000, 0x0000, 0x0155, 0x015b, 0x0000, 0x00fa, 0x0000,
                                                0x0000, 0x0000, 0x00fd, 0x017a
                                            },
                                            new UInt16[]{ // circumflex
		                                        0x00c2, 0x0000, 0x0108, 0x0000, 0x00ca, 0x0000, 0x011c, 0x0124, 0x00ce, 0x0134, 0x0000, 0x0000, 0x0000, 0x0000, 0x00d4, 0x0000,
                                                0x0000, 0x0000, 0x015c, 0x0000, 0x00db, 0x0000, 0x0174, 0x0000, 0x0176, 0x0000, 0x00e2, 0x0000, 0x0109, 0x0000, 0x00ea, 0x0000,
                                                0x011d, 0x0125, 0x00ee, 0x0135, 0x0000, 0x0000, 0x0000, 0x0000, 0x00f4, 0x0000, 0x0000, 0x0000, 0x015d, 0x0000, 0x00fb, 0x0000,
                                                0x0175, 0x0000, 0x0177, 0x0000
                                            },
                                            new UInt16[]{ // tilde
		                                        0x00c3, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0128, 0x0000, 0x0000, 0x0000, 0x0000, 0x00d1, 0x00d5, 0x0000,
                                                0x0000, 0x0000, 0x0000, 0x0000, 0x0168, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x00e3, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
                                                0x0000, 0x0000, 0x0129, 0x0000, 0x0000, 0x0000, 0x0000, 0x00f1, 0x00f5, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0169, 0x0000,
                                                0x0000, 0x0000, 0x0000, 0x0000
                                            },
                                            new UInt16[]{ // macron
		                                        0x0100, 0x0000, 0x0000, 0x0000, 0x0112, 0x0000, 0x0000, 0x0000, 0x012a, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x014c, 0x0000,
                                                0x0000, 0x0000, 0x0000, 0x0000, 0x016a, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0101, 0x0000, 0x0000, 0x0000, 0x0113, 0x0000,
                                                0x0000, 0x0000, 0x012b, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x014d, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x016b, 0x0000,
                                                0x0000, 0x0000, 0x0000, 0x0000
                                            },
                                            new UInt16[]{ // breve
		                                        0x0102, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x011e, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
                                                0x0000, 0x0000, 0x0000, 0x0000, 0x016c, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0103, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
                                                0x011f, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x016d, 0x0000,
                                                0x0000, 0x0000, 0x0000, 0x0000
                                            },
                                            new UInt16[]{ // dot
		                                        0x0000, 0x0000, 0x010a, 0x0000, 0x0116, 0x0000, 0x0120, 0x0000, 0x0130, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
                                                0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x017b, 0x0000, 0x0000, 0x010b, 0x0000, 0x0117, 0x0000,
                                                0x0121, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
                                                0x0000, 0x0000, 0x0000, 0x017c
                                            },
                                            new UInt16[]{ // umlaut
		                                        0x00c4, 0x0000, 0x0000, 0x0000, 0x00cb, 0x0000, 0x0000, 0x0000, 0x00cf, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x00d6, 0x0000,
                                                0x0000, 0x0000, 0x0000, 0x0000, 0x00dc, 0x0000, 0x0000, 0x0000, 0x0178, 0x0000, 0x00e4, 0x0000, 0x0000, 0x0000, 0x00eb, 0x0000,
                                                0x0000, 0x0000, 0x00ef, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x00f6, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x00fc, 0x0000,
                                                0x0000, 0x0000, 0x00ff, 0x0000
                                            },
                                            new UInt16[]{
                                                0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
                                                0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
                                                0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
                                                0x0000, 0x0000, 0x0000, 0x0000
                                            },
                                            new UInt16[]{ // ring
		                                        0x00c5, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
                                                0x0000, 0x0000, 0x0000, 0x0000, 0x016e, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x00e5, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
                                                0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x016f, 0x0000,
                                                0x0000, 0x0000, 0x0000, 0x0000
                                            },
                                            new UInt16[]{ // cedilla
		                                        0x0000, 0x0000, 0x00c7, 0x0000, 0x0000, 0x0000, 0x0122, 0x0000, 0x0000, 0x0000, 0x0136, 0x013b, 0x0000, 0x0145, 0x0000, 0x0000,
                                                0x0000, 0x0156, 0x015e, 0x0162, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x00e7, 0x0000, 0x0000, 0x0000,
                                                0x0000, 0x0000, 0x0000, 0x0000, 0x0137, 0x013c, 0x0000, 0x0146, 0x0000, 0x0000, 0x0000, 0x0157, 0x015f, 0x0163, 0x0000, 0x0000,
                                                0x0000, 0x0000, 0x0000, 0x0000
                                            },
                                            new UInt16[]{
                                                0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
                                                0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
                                                0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
                                                0x0000, 0x0000, 0x0000, 0x0000
                                            },
                                            new UInt16[]{ // double acute
		                                        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0150, 0x0000,
                                                0x0000, 0x0000, 0x0000, 0x0000, 0x0170, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
                                                0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0151, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0171, 0x0000,
                                                0x0000, 0x0000, 0x0000, 0x0000
                                            },
                                            new UInt16[]{ // ogonek
		                                        0x0104, 0x0000, 0x0000, 0x0000, 0x0118, 0x0000, 0x0000, 0x0000, 0x012e, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
                                                0x0000, 0x0000, 0x0000, 0x0000, 0x0172, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0105, 0x0000, 0x0000, 0x0000, 0x0119, 0x0000,
                                                0x0000, 0x0000, 0x012f, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0173, 0x0000,
                                                0x0000, 0x0000, 0x0000, 0x0000
                                            },
                                            new UInt16[]{ // caron
		                                        0x0000, 0x0000, 0x010c, 0x010e, 0x011a, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x013d, 0x0000, 0x0147, 0x0000, 0x0000,
                                                0x0000, 0x0158, 0x0160, 0x0164, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x017d, 0x0000, 0x0000, 0x010d, 0x010f, 0x011b, 0x0000,
                                                0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x013e, 0x0000, 0x0148, 0x0000, 0x0000, 0x0000, 0x0159, 0x0161, 0x0165, 0x0000, 0x0000,
                                                0x0000, 0x0000, 0x0000, 0x017e
                                            }
                                    };

            byte[] G0_LATIN_NATIONAL_SUBSETS_MAP = {
                                                        0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07,
                                                        0x08, 0x01, 0x02, 0x03, 0x04, 0xff, 0x06, 0xff,
                                                        0x00, 0x01, 0x02, 0x09, 0x04, 0x05, 0x06, 0xff,
                                                        0xff, 0xff, 0xff, 0xff, 0xff, 0x0a, 0xff, 0x07,
                                                        0xff, 0xff, 0x0b, 0x03, 0x04, 0xff, 0x0c, 0xff,
                                                        0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
                                                        0xff, 0xff, 0xff, 0x09, 0xff, 0xff, 0xff, 0xff
                                                    };

            byte[] G0_LATIN_NATIONAL_SUBSETS_POSITIONS = {
                                                                0x03, 0x04, 0x20, 0x3b, 0x3c, 0x3d, 0x3e, 0x3f, 0x40, 0x5b, 0x5c, 0x5d, 0x5e
                                                            };
            struct G0_LATIN_NATIONAL_SUBSET
            {
                public String language;
                public UInt16[] characters;
            }
            G0_LATIN_NATIONAL_SUBSET[] G0_LATIN_NATIONAL_SUBSETS = new G0_LATIN_NATIONAL_SUBSET[]{
                                                                                                        new G0_LATIN_NATIONAL_SUBSET() {  // 0
		                                                                                                    language = "English",
                                                                                                            characters = new UInt16[] { 0x00a3, 0x0024, 0x0040, 0x00ab, 0x00bd, 0x00bb, 0x005e, 0x0023, 0x002d, 0x00bc, 0x00a6, 0x00be, 0x00f7 }
                                                                                                        },
                                                                                                        new G0_LATIN_NATIONAL_SUBSET() { // 1
		                                                                                                    language = "French",
                                                                                                            characters = new UInt16[]{ 0x00e9, 0x00ef, 0x00e0, 0x00eb, 0x00ea, 0x00f9, 0x00ee, 0x0023, 0x00e8, 0x00e2, 0x00f4, 0x00fb, 0x00e7 }
                                                                                                        },
                                                                                                        new G0_LATIN_NATIONAL_SUBSET() { // 2
		                                                                                                    language = "Swedish, Finnish, Hungarian",
                                                                                                            characters = new UInt16[]{ 0x0023, 0x00a4, 0x00c9, 0x00c4, 0x00d6, 0x00c5, 0x00dc, 0x005f, 0x00e9, 0x00e4, 0x00f6, 0x00e5, 0x00fc }
                                                                                                        },
                                                                                                        new G0_LATIN_NATIONAL_SUBSET() { // 3
		                                                                                                    language = "Czech, Slovak",
                                                                                                            characters = new UInt16[]{ 0x0023, 0x016f, 0x010d, 0x0165, 0x017e, 0x00fd, 0x00ed, 0x0159, 0x00e9, 0x00e1, 0x011b, 0x00fa, 0x0161 }
                                                                                                        },
                                                                                                        new G0_LATIN_NATIONAL_SUBSET() { // 4
		                                                                                                    language = "German",
                                                                                                            characters = new UInt16[]{ 0x0023, 0x0024, 0x00a7, 0x00c4, 0x00d6, 0x00dc, 0x005e, 0x005f, 0x00b0, 0x00e4, 0x00f6, 0x00fc, 0x00df }
                                                                                                        },
                                                                                                        new G0_LATIN_NATIONAL_SUBSET() { // 5
		                                                                                                    language = "Portuguese, Spanish",
                                                                                                            characters = new UInt16[] { 0x00e7, 0x0024, 0x00a1, 0x00e1, 0x00e9, 0x00ed, 0x00f3, 0x00fa, 0x00bf, 0x00fc, 0x00f1, 0x00e8, 0x00e0 }
                                                                                                        },
                                                                                                        new G0_LATIN_NATIONAL_SUBSET() { // 6
		                                                                                                    language = "Italian",
                                                                                                            characters = new UInt16[]{ 0x00a3, 0x0024, 0x00e9, 0x00b0, 0x00e7, 0x00bb, 0x005e, 0x0023, 0x00f9, 0x00e0, 0x00f2, 0x00e8, 0x00ec }
                                                                                                        },
                                                                                                        new G0_LATIN_NATIONAL_SUBSET() { // 7
		                                                                                                    language = "Rumanian",
                                                                                                            characters = new UInt16[]{ 0x0023, 0x00a4, 0x0162, 0x00c2, 0x015e, 0x0102, 0x00ce, 0x0131, 0x0163, 0x00e2, 0x015f, 0x0103, 0x00ee }
                                                                                                        },
                                                                                                        new G0_LATIN_NATIONAL_SUBSET() { // 8
	                                                                                                        language =  "Polish",
                                                                                                            characters = new UInt16[]{ 0x0023, 0x0144, 0x0105, 0x017b, 0x015a, 0x0141, 0x0107, 0x00f3, 0x0119, 0x017c, 0x015b, 0x0142, 0x017a }
                                                                                                        },
                                                                                                        new G0_LATIN_NATIONAL_SUBSET() { // 9
	                                                                                                        language =  "Turkish",
                                                                                                            characters = new UInt16[]   { 0x0054, 0x011f, 0x0130, 0x015e, 0x00d6, 0x00c7, 0x00dc, 0x011e, 0x0131, 0x015f, 0x00f6, 0x00e7, 0x00fc }
                                                                                                        },
                                                                                                        new G0_LATIN_NATIONAL_SUBSET() { // a
	                                                                                                        language =  "Serbian, Croatian, Slovenian",
                                                                                                            characters = new UInt16[]{ 0x0023, 0x00cb, 0x010c, 0x0106, 0x017d, 0x0110, 0x0160, 0x00eb, 0x010d, 0x0107, 0x017e, 0x0111, 0x0161 }
                                                                                                        },
                                                                                                        new G0_LATIN_NATIONAL_SUBSET() { // b
	                                                                                                        language =  "Estonian",
                                                                                                            characters = new UInt16[]   { 0x0023, 0x00f5, 0x0160, 0x00c4, 0x00d6, 0x017e, 0x00dc, 0x00d5, 0x0161, 0x00e4, 0x00f6, 0x017e, 0x00fc }
                                                                                                        },
                                                                                                        new G0_LATIN_NATIONAL_SUBSET() { // c
		                                                                                                    language = "Lettish, Lithuanian",
                                                                                                            characters = new UInt16[]{ 0x0023, 0x0024, 0x0160, 0x0117, 0x0119, 0x017d, 0x010d, 0x016b, 0x0161, 0x0105, 0x0173, 0x017e, 0x012f }
                                                                                                        }
                                                                                                };

            static String[] TTXT_COLOURS = {
	        //black,     red,       green,     yellow,    blue,      magenta,   cyan,      white
	        "#000000", "#ff0000", "#00ff00", "#ffff00", "#0000ff", "#ff00ff", "#00ffff", "#ffffff"
        };


            static byte LATIN = 0;
            static byte CYRILLIC1 = 1;
            static byte CYRILLIC2 = 2;
            static byte CYRILLIC3 = 3;
            static byte GREEK = 4;
            static byte ARABIC = 5;
            static byte HEBRE = 6;


            public static byte Magazine(UInt16 p) { return (byte)((p >> 8) & 0xf); }

            public static byte Page(UInt16 p) { return (byte)(p & 0xff); }

            public static void ReverseArray(ref byte[] arr, int start, int count)
            {
                int end = start + count;
                for (int i = start; i < end; i++)
                {
                    arr[i] = REVERSE_BYTE[arr[i]];
                }
            }

            public UInt16 ParityChar(byte c)
            {
                if (PARITY_BYTE[c] == 0)
                {
                    //Unrecoverable data error
                    return 0x20;
                }

                UInt16 r = (UInt16)(c & 0x7f);
                if (r >= 0x20) r = G0[0][r - 0x20];
                return r;
            }

            public static byte UnHam84(byte a)
            {
                byte r = UN_HAM_8_4[a];
                if (r == 0xff)
                {
                    r = 0;
                    //Unrecoverable data error;
                }

                return (byte)(r & 0x0f);
            }

            public static UInt32 UnHam2418(UInt32 a)
            {
                byte test = 0;

                // Tests A-F correspond to bits 0-6 respectively in 'test'.
                for (byte i = 0; i < 23; i++) test ^= (byte)(((a >> i) & 0x01) * (i + 33));
                // Only parity bit is tested for bit 24
                test ^= (byte)(((a >> 23) & 0x01) * 32);

                if ((test & 0x1f) != 0x1f)
                {
                    // Not all tests A-E correct
                    if ((test & 0x20) == 0x20)
                    {
                        // F correct: Double error
                        return 0xffffffff;
                    }
                    // Test F incorrect: Single error
                    a ^= (UInt32)(1 << (30 - test));
                }

                return ((a & 0x000004) >> 2) + ((a & 0x000070) >> 3) + ((a & 0x007f00) >> 4) + (a & 0x7f0000) >> 5;
            }

            public void remap_g0_charset(byte c)
            {
                if (c != primary_charset.current)
                {
                    byte m = G0_LATIN_NATIONAL_SUBSETS_MAP[c];
                    if (m == 0xff)
                    {
                        //not supported
                    }
                    else
                    {
                        for (byte j = 0; j < 13; j++) G0[LATIN][G0_LATIN_NATIONAL_SUBSETS_POSITIONS[j]] = G0_LATIN_NATIONAL_SUBSETS[m].characters[j];                        
                        primary_charset.current = c;
                    }
                }
            }
        }

        private class PageBuffer
        {
            private char[][] Buffer = new char[25][] {
                new char[40], new char[40], new char[40], new char[40], new char[40], new char[40], new char[40], new char[40], new char[40], new char[40], new char[40], new char[40], new char[40], new char[40], new char[40], new char[40], new char[40], new char[40], new char[40], new char[40], new char[40], new char[40], new char[40], new char[40], new char[40]
            };

            public void SetChar(int x, int y, char c)
            {
                Buffer[y][x] = c;
                changed = true;
            }

            public char GetChar(int x, int y)
            {
                return Buffer[y][x];
            }

            private bool changed = false;

            public bool isChanged()
            {
                return changed;
            }

            public void Clear()
            {
                for (int y = 0; y < Buffer.Length; y++)
                {
                    for (int x = 0; x < Buffer[y].Length; x++)
                    {
                        Buffer[y][x] = '\0';
                    }
                }
                changed = false;
            }
        }

        public const byte TRANSMISSION_MODE_PARALLEL = 0;
        public const byte TRANSMISSION_MODE_SERIAL = 1;

        public const byte DATA_UNIT_EBU_TELETEXT_NONSUBTITLE = 0x02;
        public const byte DATA_UNIT_EBU_TELETEXT_SUBTITLE = 0x03;
        public const byte DATA_UNIT_EBU_TELETEXT_INVERTED = 0x0c;
        public const byte DATA_UNIT_VPS = 0xc3;
        public const byte DATA_UNIT_CLOSED_CAPTIONS = 0xc5;

        public const byte SIZE_OF_TELETEXT_PAYLOAD = 44;

        public Int16 PID { get; set; }
        public UInt16 PageNumber { get; set; }// = 0x199;

        private byte transmissionMode = TRANSMISSION_MODE_SERIAL;
        private bool receivingData = false;

        private PageBuffer pageBuffer = new PageBuffer();
        private TeleTextUtils teleTextUtils = new TeleTextUtils();


        public TeleText(UInt16 page, Int16 pid)
        {
            PageNumber = page;
            this.PID = pid;
        }

        public bool DecodeTeletextData(PES pes)
        {
            if (pes.PacketStartCodePrefix == PES.PACKET_START_CODE_PREFIX && pes.StreamId == PES.PRIVATE_STREAM_1 && pes.PESPacketLength > 0)
            {
                UInt16 startOfSubtitleData = 7;
                if (pes.OptionalPESHeader.MarkerBits == 2)
                {
                    startOfSubtitleData += (UInt16)(3 + pes.OptionalPESHeader.PESHeaderLength);
                }
                while (startOfSubtitleData <= pes.PESPacketLength)
                {
                    byte dataUnitId = pes.Data[startOfSubtitleData];
                    byte dataUnitLenght = pes.Data[startOfSubtitleData + 1];

                    if ((dataUnitId == DATA_UNIT_EBU_TELETEXT_NONSUBTITLE || dataUnitId == DATA_UNIT_EBU_TELETEXT_SUBTITLE) && dataUnitLenght == SIZE_OF_TELETEXT_PAYLOAD)
                    {
                        byte[] data = new byte[dataUnitLenght + 2];
                        Buffer.BlockCopy(pes.Data, startOfSubtitleData, data, 0, dataUnitLenght + 2);


                        //ETS 300 706 7.1
                        TeleTextUtils.ReverseArray(ref data, 2, dataUnitLenght);
                        DecodeTeletextDataInternal(data, dataUnitId);
                    }

                    startOfSubtitleData += (ushort)(dataUnitLenght + 2);
                }

            }
            return false;
        }

        private void DecodeTeletextDataInternal(byte[] data, byte data_unit_id)
        {
            //ETS 300 706, 9.3.1
            byte address = (byte)((TeleTextUtils.UnHam84(data[5]) << 4) + TeleTextUtils.UnHam84(data[4]));
            byte m = (byte)(address & 0x7);
            if (m == 0)
                m = 8;

            byte y = (byte)((address >> 3) & 0x1f);

            //ETS 300 706, 9.4
            byte designationCode = 0;
            if (y > 25 && y < 32)
            {
                designationCode = TeleTextUtils.UnHam84(data[6]);
            }           

            //ETS 300 706, 9.3.1
            if (0 == y)
            {
                //ETS 300 706, 9.3.1.1
                UInt16 page_number = (UInt16)((m << 8) | (TeleTextUtils.UnHam84(data[7]) << 4) + TeleTextUtils.UnHam84(data[6]));

                //ETS 300 706 Table 2,C11
                transmissionMode = (byte)(TeleTextUtils.UnHam84(data[7]) & 0x01);             

                //ETS 300 706 Table 2, C12, C13, C14
                byte charset = (byte)(((TeleTextUtils.UnHam84(data[13]) & 0x08) + (TeleTextUtils.UnHam84(data[13]) & 0x04) + (TeleTextUtils.UnHam84(data[13]) & 0x02)) >> 1);
                

                //ETS 300 706 Table 2, C11
                if ((receivingData) && (
                                        ((transmissionMode == TRANSMISSION_MODE_SERIAL) && (TeleTextUtils.Page(page_number) != TeleTextUtils.Page(PageNumber))) ||
                                        ((transmissionMode == TRANSMISSION_MODE_PARALLEL) && (TeleTextUtils.Page(page_number) != TeleTextUtils.Page(PageNumber)) && (m == TeleTextUtils.Magazine(PageNumber)))))
                {
                    receivingData = false;
                    return;
                }


                if (page_number != PageNumber) //wrong page
                    return;

                if (pageBuffer.isChanged())
                {                 
                    ProcessBuffer();
                }
                teleTextUtils.primary_charset.g0_x28 = TeleTextUtils.UNDEF;

                byte c = (teleTextUtils.primary_charset.g0_m29 != TeleTextUtils.UNDEF) ? teleTextUtils.primary_charset.g0_m29 : charset;
                teleTextUtils.remap_g0_charset(c);

                pageBuffer.Clear();
                receivingData = true;
            }
            //ETS 300 706, 9.3.2
            if ((m == TeleTextUtils.Magazine(PageNumber)) && (y >= 1) && (y <= 23) && receivingData)
            {
                for (int x = 0; x < 40; x++)
                {
                    if (pageBuffer.GetChar(x, y) == '\0')
                    {
                        pageBuffer.SetChar(x, y, (char)teleTextUtils.ParityChar(data[6 + x]));
                    }
                }
            }
            else if ((m == TeleTextUtils.Magazine(PageNumber)) && (y == 26) && (receivingData))
            {
                // ETS 300 706, chapter 12.3.2: X/26 definition
                byte x26_row = 0;
                byte x26_col = 0;

                UInt32[] triplets = new UInt32[13] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                for (byte i = 1, j = 0; i < 40; i += 3, j++) triplets[j] = TeleTextUtils.UnHam2418((UInt32)((data[6 + i + 2] << 16) + (data[6 + i + 1] << 8) + data[6 + i]));

                for (byte j = 0; j < 13; j++)
                {
                    if (triplets[j] == 0xffffffff)
                    {
                        continue;
                    }

                    byte d = (byte)((triplets[j] & 0x3f800) >> 11);
                    byte mode = (byte)((triplets[j] & 0x7c0) >> 6);
                    byte a = (byte)(triplets[j] & 0x3f);
                    bool row_address_group = ((a >= 40) && (a <= 63));

                    // ETS 300 706, chapter 12.3.1, table 27: set active position
                    if ((mode == 0x04) && (row_address_group))
                    {
                        x26_row = (byte)(a - 40);
                        if (x26_row == 0) x26_row = 24;
                        x26_col = 0;
                    }

                    // ETS 300 706, chapter 12.3.1, table 27: termination marker
                    if ((mode >= 0x11) && (mode <= 0x1f) && (row_address_group)) break;

                    // ETS 300 706, chapter 12.3.1, table 27: character from G2 set
                    if ((mode == 0x0f) && (!row_address_group))
                    {
                        x26_col = a;
                        if (d > 31) pageBuffer.SetChar(x26_col, x26_row, (char)teleTextUtils.G2[0][d - 0x20]);
                    }

                    // ETS 300 706, chapter 12.3.1, table 27: G0 character with diacritical mark
                    if ((mode >= 0x11) && (mode <= 0x1f) && (!row_address_group))
                    {
                        x26_col = a;

                        // A - Z
                        if ((d >= 65) && (d <= 90)) pageBuffer.SetChar(x26_col, x26_row, (char)teleTextUtils.G2_ACCENTS[mode - 0x11][d - 65]);
                        // a - z
                        else if ((d >= 97) && (d <= 122)) pageBuffer.SetChar(x26_col, x26_row, (char)teleTextUtils.G2_ACCENTS[mode - 0x11][d - 71]);
                        // other
                        else pageBuffer.SetChar(x26_col, x26_row, (char)teleTextUtils.ParityChar(d));
                    }
                }
            }
            else if ((m == TeleTextUtils.Magazine(PageNumber)) && (y == 28) && (receivingData))
            {
                // TODO:
                //   ETS 300 706, chapter 9.4.7: Packet X/28/4
                //   Where packets 28/0 and 28/4 are both transmitted as part of a page, packet 28/0 takes precedence over 28/4 for all but the colour map entry coding.
                if ((designationCode == 0) || (designationCode == 4))
                {
                    // ETS 300 706, chapter 9.4.2: Packet X/28/0 Format 1
                    // ETS 300 706, chapter 9.4.7: Packet X/28/4
                    UInt32 triplet0 = TeleTextUtils.UnHam2418((UInt32)((data[6 + 3] << 16) + (data[6 + 2] << 8) + data[6 + 1]));

                    if (triplet0 == 0xffffffff)
                    {
                        // invalid data (HAM24/18 uncorrectable error detected), skip group                        
                    }
                    else
                    {
                        // ETS 300 706, chapter 9.4.2: Packet X/28/0 Format 1 only
                        if ((triplet0 & 0x0f) == 0x00)
                        {
                            teleTextUtils.primary_charset.g0_x28 = (byte)((triplet0 & 0x3f80) >> 7);
                            teleTextUtils.remap_g0_charset(teleTextUtils.primary_charset.g0_x28);
                        }
                    }
                }
            }
            else if ((m == TeleTextUtils.Magazine(PageNumber)) && (y == 29))
            {
                // TODO:
                //   ETS 300 706, chapter 9.5.1 Packet M/29/0
                //   Where M/29/0 and M/29/4 are transmitted for the same magazine, M/29/0 takes precedence over M/29/4.
                if ((designationCode == 0) || (designationCode == 4))
                {
                    // ETS 300 706, chapter 9.5.1: Packet M/29/0
                    // ETS 300 706, chapter 9.5.3: Packet M/29/4
                    UInt32 triplet0 = TeleTextUtils.UnHam2418((UInt32)((data[6 + 3] << 16) + (data[6 + 2] << 8) + data[6 + 1]));

                    if (triplet0 == 0xffffffff)
                    {
                        // invalid data (HAM24/18 uncorrectable error detected), skip group                       
                    }
                    else
                    {
                        // ETS 300 706, table 11: Coding of Packet M/29/0
                        // ETS 300 706, table 13: Coding of Packet M/29/4
                        if ((triplet0 & 0xff) == 0x00)
                        {
                            teleTextUtils.primary_charset.g0_m29 = (byte)((triplet0 & 0x3f80) >> 7);
                            // X/28 takes precedence over M/29
                            if (teleTextUtils.primary_charset.g0_x28 == TeleTextUtils.UNDEF)
                            {
                                teleTextUtils.remap_g0_charset(teleTextUtils.primary_charset.g0_m29);
                            }
                        }
                    }
                }
            }

        }

        public void ProcessBuffer()
        {
            String[] page = new String[25];
            for (int y = 0; y < 25; y++)
            {
                page[y] = "";
                for (int x = 0; x < 40; x++)
                {
                    char c = pageBuffer.GetChar(x, y);
                    if (c == '\0')
                    {
                        page[y] += " ";
                    }
                    else
                    {
                        page[y] += c;
                    }
                }
            }

            OnTeletextPageRecieved(page, PageNumber, PID);
            //System.Threading.Thread.Sleep(1000);
        }

        public event EventHandler TeletextPageRecieved;

        protected virtual void OnTeletextPageRecieved(String[] page, UInt16 pageNumber, Int16 pid)
        {
            if (TeletextPageRecieved != null)
            {
                TeletextPageRecieved.BeginInvoke(this, new TeleTextSubtitleEventArgs(page, pageNumber, pid), EndAsyncEvent, null);
            }
        }


        private void EndAsyncEvent(IAsyncResult iar)
        {
            var ar = (System.Runtime.Remoting.Messaging.AsyncResult)iar;
            var invokedMethod = (EventHandler)ar.AsyncDelegate;

            try
            {
                invokedMethod.EndInvoke(iar);
            }
            catch
            {
                //nothing to do
            }
        }
    }
}
