using System;
using System.Collections.Generic;
using TsDecoder.TransportStream;

namespace TtxDecoder.Teletext
{
    public class TeleText
    {
        public const byte TransmissionModeParallel = 0;
        public const byte TransmissionModeSerial = 1;
        public const byte DataUnitEbuTeletextNonsubtitle = 0x02;
        public const byte DataUnitEbuTeletextSubtitle = 0x03;
        public const byte DataUnitEbuTeletextInverted = 0x0c;
        public const byte DataUnitVps = 0xc3;
        public const byte DataUnitClosedCaptions = 0xc5;
        public const byte SizeOfTeletextPayload = 44;

        private byte _transmissionMode = TransmissionModeSerial;
        private bool _receivingData;
        private readonly PageBuffer _pageBuffer = new PageBuffer();
        
        private Utils.Charset _primaryCharset = new Utils.Charset
        {
            Current = 0x00,
            G0M29 = Utils.Undef,
            G0X28 = Utils.Undef
        };

        // private readonly Utils _utils = new Utils();

        public short Pid { get; set; }

        public ushort PageNumber { get; set; }// = 0x199;

        public TeleText(ushort page, short pid)
        {
            PageNumber = page;
            Pid = pid;
        }

        public bool DecodeTeletextData(Pes pes)
        {
            if (pes.PacketStartCodePrefix != Pes.DefaultPacketStartCodePrefix || pes.StreamId != Pes.PrivateStream1 ||
                pes.PesPacketLength <= 0) return false;

            ushort startOfSubtitleData = 7;
            if (pes.OptionalPesHeader.MarkerBits == 2)
            {
                startOfSubtitleData += (ushort)(3 + pes.OptionalPesHeader.PesHeaderLength);
            }
            while (startOfSubtitleData <= pes.PesPacketLength)
            {
                var dataUnitId = pes.Data[startOfSubtitleData];
                var dataUnitLenght = pes.Data[startOfSubtitleData + 1];

                if ((dataUnitId == DataUnitEbuTeletextNonsubtitle || dataUnitId == DataUnitEbuTeletextSubtitle) && dataUnitLenght == SizeOfTeletextPayload)
                {
                    var data = new byte[dataUnitLenght + 2];
                    Buffer.BlockCopy(pes.Data, startOfSubtitleData, data, 0, dataUnitLenght + 2);


                    //ETS 300 706 7.1
                    Utils.ReverseArray(ref data, 2, dataUnitLenght);
                    DecodeTeletextDataInternal(data);
                }

                startOfSubtitleData += (ushort)(dataUnitLenght + 2);
            }
            return false;
        }

        private void DecodeTeletextDataInternal(IList<byte> data)
        {
            //ETS 300 706, 9.3.1
            var address = (byte)((Utils.UnHam84(data[5]) << 4) + Utils.UnHam84(data[4]));
            var m = (byte)(address & 0x7);
            if (m == 0)
                m = 8;

            var y = (byte)((address >> 3) & 0x1f);

            //ETS 300 706, 9.4
            byte designationCode = 0;
            if (y > 25 && y < 32)
            {
                designationCode = Utils.UnHam84(data[6]);
            }

            //ETS 300 706, 9.3.1
            if (0 == y)
            {
                //ETS 300 706, 9.3.1.1
                var pageNumber = (ushort)((m << 8) | (Utils.UnHam84(data[7]) << 4) + Utils.UnHam84(data[6]));

                //ETS 300 706 TableOld 2,C11
                _transmissionMode = (byte)(Utils.UnHam84(data[7]) & 0x01);

                //ETS 300 706 TableOld 2, C12, C13, C14
                var charset = (byte)(((Utils.UnHam84(data[13]) & 0x08) + (Utils.UnHam84(data[13]) & 0x04) + (Utils.UnHam84(data[13]) & 0x02)) >> 1);


                //ETS 300 706 TableOld 2, C11
                if ((_receivingData) && (
                                        ((_transmissionMode == TransmissionModeSerial) && (Utils.Page(pageNumber) != Utils.Page(PageNumber))) ||
                                        ((_transmissionMode == TransmissionModeParallel) && (Utils.Page(pageNumber) != Utils.Page(PageNumber)) && (m == Utils.Magazine(PageNumber)))))
                {
                    _receivingData = false;
                    return;
                }


                if (pageNumber != PageNumber) //wrong page
                    return;

                if (_pageBuffer.IsChanged())
                {
                    ProcessBuffer();
                }
                
                _primaryCharset.G0X28 = Utils.Undef;

                var c = (_primaryCharset.G0M29 != Utils.Undef) ? _primaryCharset.G0M29 : charset;
                Utils.remap_g0_charset(c, _primaryCharset);

                _pageBuffer.Clear();
                _receivingData = true;
            }
            //ETS 300 706, 9.3.2
            if ((m == Utils.Magazine(PageNumber)) && (y >= 1) && (y <= 23) && _receivingData)
            {
                for (var x = 0; x < 40; x++)
                {
                    if (_pageBuffer.GetChar(x, y) == '\0')
                    {
                        _pageBuffer.SetChar(x, y, (char)Utils.ParityChar(data[6 + x]));
                    }
                }
            }
            else if ((m == Utils.Magazine(PageNumber)) && (y == 26) && (_receivingData))
            {
                // ETS 300 706, chapter 12.3.2: X/26 definition
                byte x26Row = 0;

                var triplets = new uint[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                for (byte i = 1, j = 0; i < 40; i += 3, j++) triplets[j] = Utils.UnHam2418((uint)((data[6 + i + 2] << 16) + (data[6 + i + 1] << 8) + data[6 + i]));

                for (byte j = 0; j < 13; j++)
                {
                    if (triplets[j] == 0xffffffff)
                    {
                        continue;
                    }

                    var d = (byte)((triplets[j] & 0x3f800) >> 11);
                    var mode = (byte)((triplets[j] & 0x7c0) >> 6);
                    var a = (byte)(triplets[j] & 0x3f);
                    var rowAddressGroup = ((a >= 40) && (a <= 63));

                    // ETS 300 706, chapter 12.3.1, table 27: set active position
                    byte x26Col;
                    if ((mode == 0x04) && (rowAddressGroup))
                    {
                        x26Row = (byte)(a - 40);
                        if (x26Row == 0) x26Row = 24;
                    }

                    // ETS 300 706, chapter 12.3.1, table 27: termination marker
                    if ((mode >= 0x11) && (mode <= 0x1f) && (rowAddressGroup)) break;

                    // ETS 300 706, chapter 12.3.1, table 27: character from G2 set
                    if ((mode == 0x0f) && (!rowAddressGroup))
                    {
                        x26Col = a;
                        if (d > 31) _pageBuffer.SetChar(x26Col, x26Row, (char)Utils.G2[0][d - 0x20]);
                    }

                    // ETS 300 706, chapter 12.3.1, table 27: G0 character with diacritical mark
                    if ((mode >= 0x11) && (mode <= 0x1f) && (!rowAddressGroup))
                    {
                        x26Col = a;

                        // A - Z
                        if ((d >= 65) && (d <= 90)) _pageBuffer.SetChar(x26Col, x26Row, (char)Utils.G2Accents[mode - 0x11][d - 65]);
                        // a - z
                        else if ((d >= 97) && (d <= 122)) _pageBuffer.SetChar(x26Col, x26Row, (char)Utils.G2Accents[mode - 0x11][d - 71]);
                        // other
                        else _pageBuffer.SetChar(x26Col, x26Row, (char)Utils.ParityChar(d));
                    }
                }
            }
            else if ((m == Utils.Magazine(PageNumber)) && (y == 28) && (_receivingData))
            {
                // TODO:
                //   ETS 300 706, chapter 9.4.7: Packet X/28/4
                //   Where packets 28/0 and 28/4 are both transmitted as part of a page, packet 28/0 takes precedence over 28/4 for all but the colour map entry coding.

                if ((designationCode != 0) && (designationCode != 4)) return;

                // ETS 300 706, chapter 9.4.2: Packet X/28/0 Format 1
                // ETS 300 706, chapter 9.4.7: Packet X/28/4
                var triplet0 = Utils.UnHam2418((uint)((data[6 + 3] << 16) + (data[6 + 2] << 8) + data[6 + 1]));

                if (triplet0 == 0xffffffff)
                {
                    // invalid data (HAM24/18 uncorrectable error detected), skip group                        
                }
                else
                {
                    // ETS 300 706, chapter 9.4.2: Packet X/28/0 Format 1 only
                    if ((triplet0 & 0x0f) == 0x00)
                    {
                        _primaryCharset.G0X28 = (byte)((triplet0 & 0x3f80) >> 7);
                        Utils.remap_g0_charset(_primaryCharset.G0X28, _primaryCharset);
                    }
                }
            }
            else if ((m == Utils.Magazine(PageNumber)) && (y == 29))
            {
                // TODO:
                //   ETS 300 706, chapter 9.5.1 Packet M/29/0
                //   Where M/29/0 and M/29/4 are transmitted for the same magazine, M/29/0 takes precedence over M/29/4.
                if ((designationCode == 0) || (designationCode == 4))
                {
                    // ETS 300 706, chapter 9.5.1: Packet M/29/0
                    // ETS 300 706, chapter 9.5.3: Packet M/29/4
                    var triplet0 = Utils.UnHam2418((uint)((data[6 + 3] << 16) + (data[6 + 2] << 8) + data[6 + 1]));

                    if (triplet0 == 0xffffffff)
                    {
                        // invalid data (HAM24/18 uncorrectable error detected), skip group                       
                    }
                    else
                    {
                        // ETS 300 706, table 11: Coding of Packet M/29/0
                        // ETS 300 706, table 13: Coding of Packet M/29/4
                        if ((triplet0 & 0xff) != 0x00) return;
                        
                        _primaryCharset.G0M29 = (byte)((triplet0 & 0x3f80) >> 7);
                        // X/28 takes precedence over M/29
                        if (_primaryCharset.G0X28 == Utils.Undef)
                        {
                            Utils.remap_g0_charset(_primaryCharset.G0M29, _primaryCharset);
                        }
                    }
                }
            }

        }

        public void ProcessBuffer()
        {
            var page = new string[25];
            for (var y = 0; y < 25; y++)
            {
                page[y] = "";
                for (var x = 0; x < 40; x++)
                {
                    var c = _pageBuffer.GetChar(x, y);
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

            OnTeletextPageRecieved(page, PageNumber, Pid);
            //System.Threading.Thread.Sleep(1000);
        }

        public event EventHandler TeletextPageRecieved;

        protected virtual void OnTeletextPageRecieved(string[] page, ushort pageNumber, short pid)
        {
            TeletextPageRecieved?.BeginInvoke(this, new TeleTextSubtitleEventArgs(page, pageNumber, pid), EndAsyncEvent, null);
        }


        private static void EndAsyncEvent(IAsyncResult iar)
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
