using System;
using System.Collections.Generic;
using System.Diagnostics;
using TsDecoder.TransportStream;

namespace TtxDecoder.Teletext
{
    public class TeleTextDecoder
    {
        private TeletextDescriptor _currentTeletextDescriptor;
        private Pes _currentTeletextPes;
        private Dictionary<ushort, TeleText> _teletextSubtitlePage;

        /// <summary>
        /// The TS Packet ID that has been selected as the elementary stream containing teletext data
        /// </summary>
        public short TeletextPid { get; private set; } = -1;

        /// <summary>
        /// A Dictionary of decoded Teletext pages, where each page contains a number of strings
        /// </summary>
        public Dictionary<ushort, string[]> TeletextDecodedSubtitlePage { get; } = new Dictionary<ushort, string[]>();
        
        /// <summary>
        /// The Program Number of the service that is used as source for teletext data - can be set by constructor only, otherwise default program will be used.
        /// </summary>
        public ushort ProgramNumber { get; private set; }

        public TeleTextDecoder(ushort programNumber)
        {
            ProgramNumber = programNumber;
        }

        public TeleTextDecoder()
        {
        }

        private void Setup(TsDecoder.TransportStream.TsDecoder tsDecoder)
        {
            if (tsDecoder == null || _currentTeletextDescriptor != null) return;

            lock (tsDecoder)
            {
                if (ProgramNumber == 0)
                {
                    var pmt = tsDecoder.GetSelectedPmt(ProgramNumber);
                    if (pmt != null)
                    {
                        ProgramNumber = pmt.ProgramNumber;
                    }
                }

                if (ProgramNumber == 0) return;

                var esStream = tsDecoder.GetEsStreamForProgramNumberByTag(ProgramNumber, 0x6, 0x56);

                _currentTeletextDescriptor =
                    tsDecoder.GetDescriptorForProgramNumberByTag<TeletextDescriptor>(ProgramNumber, 6, 0x56);

                if (_currentTeletextDescriptor == null) return;

                foreach (var lang in _currentTeletextDescriptor.Languages)
                {
                  //  if (lang.TeletextType != 0x02 && lang.TeletextType != 0x05)
                    //    continue;

                    TeletextPid = esStream.ElementaryPid;

                    if (_teletextSubtitlePage == null)
                    {
                        _teletextSubtitlePage = new Dictionary<ushort, TeleText>();
                    }

                    var m = lang.TeletextMagazineNumber;
                    if (lang.TeletextMagazineNumber == 0)
                    {
                        m = 8;
                    }
                    var page = (ushort)((m << 8) + lang.TeletextPageNumber);

                    if (_teletextSubtitlePage.ContainsKey(page)) continue;

                    _teletextSubtitlePage.Add(page, new TeleText(page, TeletextPid));
                    _teletextSubtitlePage[page].TeletextPageRecieved += TeleTextDecoder_TeletextPageRecieved;

                }
            }
        }

        public void AddPacket(TsDecoder.TransportStream.TsDecoder tsDecoder, TsPacket tsPacket)
        {
            if (_currentTeletextDescriptor == null)
            {
                Setup(tsDecoder);

                if (_currentTeletextDescriptor != null)
                {
                    Debug.WriteLine($"Locked onto teletext PID for {TeletextPid} ");
                }
            }

            if (tsPacket.Pid != TeletextPid) return;

            if (tsPacket.PayloadUnitStartIndicator)
            {
                if (_currentTeletextPes != null)
                {
                    if (_currentTeletextPes.HasAllBytes())
                    {
                        _currentTeletextPes.Decode();
                        foreach (var key in _teletextSubtitlePage.Keys)
                        {
                            _teletextSubtitlePage[key].DecodeTeletextData(_currentTeletextPes);
                        }
                    }
                }

                _currentTeletextPes = new Pes(tsPacket);
            }
            else
            {
                _currentTeletextPes?.Add(tsPacket);
            }
        }

        private void TeleTextDecoder_TeletextPageRecieved(object sender, EventArgs e)
        {
            var teletextArgs = (TeleTextSubtitleEventArgs)e;

            lock (TeletextDecodedSubtitlePage)
            {
                if (!TeletextDecodedSubtitlePage.ContainsKey(teletextArgs.PageNumber))
                {
                    TeletextDecodedSubtitlePage.Add(teletextArgs.PageNumber, new string[0]);
                }

                TeletextDecodedSubtitlePage[teletextArgs.PageNumber] = teletextArgs.Page;
            }
        }
    }
}
