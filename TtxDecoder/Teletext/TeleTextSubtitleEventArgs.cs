using System;
using System.Collections.Generic;

namespace TtxDecoder.Teletext
{
    public class TeleTextSubtitleEventArgs : EventArgs
    {
        public string[] Page { get; set; }
        public ushort PageNumber { get; set; }
        public short Pid { get; set; }

        public TeleTextSubtitleEventArgs(IList<string> page, ushort pageNumber, short pid)
        {
            Page = new string[page.Count];

            for (var i = 0; i < page.Count; i++)
            {
                Page[i] = page[i];
            }

            PageNumber = pageNumber;
            Pid = pid;
        }
    }
}