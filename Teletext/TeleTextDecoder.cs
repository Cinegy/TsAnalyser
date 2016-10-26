using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using TsAnalyser.TransportStream;

namespace TsAnalyser.Teletext
{
    public class TeleTextDecoder
    {
        private TeletextDescriptor _currentTeletextDescriptor;


        private readonly Dictionary<short, Dictionary<ushort, TeleText>> _teletextSubtitlePages = new Dictionary<short, Dictionary<ushort, TeleText>>();
        private readonly Dictionary<short, Pes> _teletextSubtitleBuffers = new Dictionary<short, Pes>();
        public readonly object TeletextSubtitleDecodedPagesLock = new object();
        public readonly Dictionary<short, Dictionary<ushort, string[]>> TeletextDecodedSubtitlePages = new Dictionary<short, Dictionary<ushort, string[]>>();


        private void Setup(TsDecoder tsDecoder)
       {
           if (tsDecoder == null || _currentTeletextDescriptor != null) return;

           lock (tsDecoder)
           {
               var pmt = tsDecoder?.ProgramMapTables?.OrderBy(t => t.ProgramNumber).FirstOrDefault();

               var esStream = tsDecoder.GetEsStreamForProgramNumberByTag(pmt?.ProgramNumber, 0x6, 0x56);

               _currentTeletextDescriptor =
                   tsDecoder?.GetDescriptorForProgramNumberByTag<TeletextDescriptor>(pmt?.ProgramNumber,
                       6,
                       0x56);

               if (_currentTeletextDescriptor == null) return;

               foreach (var lang in _currentTeletextDescriptor.Languages)
               {
                   if (lang.TeletextType != 0x02 && lang.TeletextType != 0x05)
                       continue;

                   if (!_teletextSubtitlePages.ContainsKey(esStream.ElementaryPid))
                   {
                       _teletextSubtitlePages.Add(esStream.ElementaryPid,
                           new Dictionary<ushort, TeleText>());
                       _teletextSubtitleBuffers.Add(esStream.ElementaryPid, null);
                   }
                   var m = lang.TeletextMagazineNumber;
                   if (lang.TeletextMagazineNumber == 0)
                   {
                       m = 8;
                   }
                   var page = (ushort)((m << 8) + lang.TeletextPageNumber);
                   //var pageStr = $"{page:X}";

                   // if (page == 0x199)
                   {
                       if (
                           _teletextSubtitlePages[esStream.ElementaryPid]
                               .ContainsKey(page)) continue;

                       _teletextSubtitlePages[esStream.ElementaryPid].Add(page,
                           new TeleText(page, esStream.ElementaryPid));
                        _teletextSubtitlePages[esStream.ElementaryPid][page]
                            .TeletextPageRecieved += TeleTextDecoder_TeletextPageRecieved; 
                   }
               }
           }
       }

        public void AddPacket(TsDecoder tsDecoder,TsPacket tsPacket)
        {
            if (_currentTeletextDescriptor == null)
            {
                Setup(tsDecoder);

                if (_currentTeletextDescriptor != null)
                {
                    Debug.WriteLine($"Locked onto teletext PID for {_teletextSubtitlePages.Keys.FirstOrDefault()} ");
                }
            }

            if (_teletextSubtitlePages?.ContainsKey(tsPacket.Pid) == false) return;
            
            if (tsPacket.PayloadUnitStartIndicator)
            {
                if (null != _teletextSubtitleBuffers[tsPacket.Pid])
                {
                    if (_teletextSubtitleBuffers[tsPacket.Pid].HasAllBytes())
                    {
                        _teletextSubtitleBuffers[tsPacket.Pid].Decode();
                        foreach (var key in _teletextSubtitlePages[tsPacket.Pid].Keys)
                        {
                            _teletextSubtitlePages[tsPacket.Pid][key].DecodeTeletextData(_teletextSubtitleBuffers[tsPacket.Pid]);
                        }
                    }
                }

                _teletextSubtitleBuffers[tsPacket.Pid] = new Pes(tsPacket);
            }
            else if (_teletextSubtitleBuffers[tsPacket.Pid] != null)
            {
                _teletextSubtitleBuffers[tsPacket.Pid].Add(tsPacket);
            }
        }

        private void TeleTextDecoder_TeletextPageRecieved(object sender, EventArgs e)
        {
  
            var teletextArgs = (TeleTextSubtitleEventArgs)e;
            lock (TeletextSubtitleDecodedPagesLock)
            {
                if (!TeletextDecodedSubtitlePages.ContainsKey(teletextArgs.Pid))
                {
                    TeletextDecodedSubtitlePages.Add(teletextArgs.Pid, new Dictionary<ushort, string[]>());
                }

                if (!TeletextDecodedSubtitlePages.ContainsKey(teletextArgs.Pid)) return;

                if (!TeletextDecodedSubtitlePages[teletextArgs.Pid].ContainsKey(teletextArgs.PageNumber))
                {
                    TeletextDecodedSubtitlePages[teletextArgs.Pid].Add(teletextArgs.PageNumber, new string[0]);
                }

                if (TeletextDecodedSubtitlePages[teletextArgs.Pid].ContainsKey(teletextArgs.PageNumber))
                {

                }

                TeletextDecodedSubtitlePages[teletextArgs.Pid][teletextArgs.PageNumber] = teletextArgs.Page;
            }
        }
    }
}
