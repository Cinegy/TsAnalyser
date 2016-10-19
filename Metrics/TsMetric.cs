using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using TsAnalyser.Tables;
using TsAnalyser.Teletext;
using TsAnalyser.TsElements;

namespace TsAnalyser.Metrics
{
    public class TsMetric
    {

        public ProgAssociationTable ProgAssociationTable { get; private set; }
        public ProgramMapTable ProgramMapTable { get; private set; }

        public void AddPacket(TsPacket newPacket)
        {
            try
            {
                if (newPacket.TransportErrorIndicator) return;

                if (newPacket.Pid == 0x00)
                {
                    ProgAssociationTable = ProgAssociationTableFactory.ProgAssociationTableFromTsPackets(new[] { newPacket });
                }

                CheckPmt(ref newPacket);
                
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Exception generated within AddPacket method: " + ex.Message);
            }
        }


        private void CheckPmt(ref TsPacket tsPacket)
        {
            if (ProgAssociationTable == null || tsPacket.Pid != ProgAssociationTable.PmtPid) return;

            if (tsPacket.PayloadUnitStartIndicator)
            {
                ProgramMapTable = new ProgramMapTable(tsPacket);
            }
            else
            {
                if (ProgramMapTable != null && !ProgramMapTable.HasAllBytes())
                {
                    ProgramMapTable?.Add(tsPacket);
                }
            }

            if (ProgramMapTable == null || !ProgramMapTable.HasAllBytes()) return;

            if (ProgramMapTable.ProcessTable())
            {
                //TODO: Wiring up to API needed here

                //if (_tsAnalyserApi != null && _tsAnalyserApi.ProgramMetrics == null) _tsAnalyserApi.ProgramMetrics = _programMapTable;

                //TODO: Sort out teletext here
                //if (_decodeTeletext)
                //{
                //    foreach (var esStream in ProgramMapTable.EsStreams)
                //    {
                //        foreach (
                //            var descriptor in
                //                esStream.Descriptors.Where(d => d.DescriptorTag == 0x56))
                //        {
                //            var teletext = descriptor as TeletextDescriptor;
                //            if (null == teletext) continue;

                //            foreach (var lang in teletext.Languages)
                //            {
                //                if (lang.TeletextType != 0x02 && lang.TeletextType != 0x05)
                //                    continue;

                //                if (!TeletextSubtitlePages.ContainsKey(esStream.ElementaryPid))
                //                {
                //                    TeletextSubtitlePages.Add(esStream.ElementaryPid,
                //                        new Dictionary<ushort, TeleText>());
                //                    TeletextSubtitleBuffers.Add(esStream.ElementaryPid, null);
                //                }
                //                var m = lang.TeletextMagazineNumber;
                //                if (lang.TeletextMagazineNumber == 0)
                //                {
                //                    m = 8;
                //                }
                //                var page = (ushort)((m << 8) + lang.TeletextPageNumber);
                //                //var pageStr = $"{page:X}";

                //                // if (page == 0x199)
                //                {
                //                    if (
                //                        TeletextSubtitlePages[esStream.ElementaryPid]
                //                            .ContainsKey(page)) continue;

                //                    TeletextSubtitlePages[esStream.ElementaryPid].Add(page,
                //                        new TeleText(page, esStream.ElementaryPid));
                //                    TeletextSubtitlePages[esStream.ElementaryPid][page]
                //                        .TeletextPageRecieved += TeletextPageRecievedMethod;
                //                }
                //            }
                //        }
                //    }
                //}
            }
            else
            {
                ProgramMapTable = null;
            }
        }
    }
}
