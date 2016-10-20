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
        public List<ProgramMapTable> ProgramMapTables { get; private set; }

        public Object NetworkInformationTable { get; private set; }

        public TsMetric()
        {
            ProgramMapTables = new List<ProgramMapTable>();
        }

        public void AddPacket(TsPacket newPacket)
        {
            try
            {
                if (newPacket.TransportErrorIndicator) return;

                if (newPacket.Pid == 0x00)
                {
                    ProgAssociationTable = ProgAssociationTableFactory.ProgAssociationTableFromTsPackets(new[] { newPacket });
                }

                CheckPmt(newPacket);
                
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Exception generated within AddPacket method: " + ex.Message);
            }
        }
        
        private void CheckPmt(TsPacket tsPacket)
        {
            if (ProgAssociationTable == null) return;

            if (tsPacket.Pid == 0x0010)
            {
                //Pid 0x0010 is a NIT packet
                //TODO: Decode NIT, and store
                NetworkInformationTable = new object();
                return;
            }

            lock (ProgramMapTables)
            {
                if (!ProgAssociationTable.Pids.Contains(tsPacket.Pid)) return;
                
                var pmt = ProgramMapTables.SingleOrDefault(item => item.Pid == tsPacket.Pid);

                if (pmt == null && tsPacket.PayloadUnitStartIndicator)
                {
                    pmt = new ProgramMapTable(tsPacket);
                    ProgramMapTables.Add(pmt);
                }
                else if (pmt != null && !tsPacket.PayloadUnitStartIndicator)
                {
                    if (!pmt.HasAllBytes())
                    {
                        pmt?.Add(tsPacket);
                    }
                }
                
                if (pmt == null || !pmt.HasAllBytes()) return;

                if (pmt.ProcessTable())
                {

                    Debug.WriteLine($"\t\t\t\nProgram Map Table (PMT Pid: {pmt.Pid}, Program Number: {pmt.ProgramNumber} :\n----------------\t\t\t\t");

                    //if (ProgramMapTable?.EsStreams != null)
                    //{
                    //    foreach (var stream in ProgramMapTable?.EsStreams)
                    //    {
                    //        PrintToConsole(
                    //            "Program Descriptor: {0} ({1})\t\t\t", stream?.ElementaryPid, stream?.StreamTypeString);

                    //    }
                    //}

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
                    pmt = null;
                }
            }
        }
    }
}
