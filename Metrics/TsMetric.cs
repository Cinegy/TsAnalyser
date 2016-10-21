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
        public object ProgramMapTableLock { get; } = new object();
        public ServiceDescriptionTable ServiceDescriptionTable { get; private set; }
        public List<ServiceDescriptor> ServiceDescriptors { get; private set; }
        public object ServiceDescriptionTableLock { get; } = new object();
        public bool DecodeServiceDescriptions { get; set; } = false;
        
        //TODO:placeholder object to sort out later
        public object NetworkInformationTable { get; private set; }
        
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

                if (newPacket.Pid == 0x0011)
                {
                    CheckSdt(newPacket);
                }
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

            lock (ProgramMapTableLock)
            {
                if (!ProgAssociationTable.Pids.Contains(tsPacket.Pid)) return;

                if (ProgramMapTables == null)
                {
                    ProgramMapTables = new List<ProgramMapTable>(); 
                }

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
                    //TODO: Wiring up to API needed here
                    
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
            }
        }

        private void CheckSdt(TsPacket tsPacket)
        {
            if (tsPacket.Pid != 0x0011) return;

            if (!DecodeServiceDescriptions) return;

            lock (ServiceDescriptionTableLock)
            {
                if (tsPacket.PayloadUnitStartIndicator)
                {
                    if (ServiceDescriptionTable?.CheckVersionCurrent(tsPacket) != true)
                    {
                        ServiceDescriptionTable = new ServiceDescriptionTable(tsPacket);
                    }
                }
                else
                {
                    if (ServiceDescriptionTable?.HasAllBytes() == false)
                    {
                        ServiceDescriptionTable.Add(tsPacket);
                    }
                }

                if (ServiceDescriptionTable?.HasAllBytes() != true)
                {
                    return;
                }

                if (!ServiceDescriptionTable.ProcessTable())
                {
                    ServiceDescriptionTable = null;
                    return;
                }

                //TODO: Fix up API again

                //   if (_tsAnalyserApi != null && _tsAnalyserApi.ServiceMetrics == null) _tsAnalyserApi.ServiceMetrics = _serviceDescriptionTable;

                if (ServiceDescriptionTable?.Items == null || ServiceDescriptionTable.TableId != 0x42)
                {
                    return;
                }

                if(ServiceDescriptors==null)
                {
                    ServiceDescriptors = new List<ServiceDescriptor>(16);
                }

                foreach (var item in ServiceDescriptionTable.Items)
                {
                    foreach (var descriptor in item.Descriptors.Where(d => d.DescriptorTag == 0x48))
                    {
                        var sd = descriptor as ServiceDescriptor;

                        if (sd == null) continue;
                        
                        var service = ProgramMapTables?.SingleOrDefault(i => i?.ProgramNumber == item?.ServiceId);


                        var match = false;
                        
                        foreach (var serviceDescriptor in ServiceDescriptors)
                        {
                            if (serviceDescriptor.ServiceName.Value == sd.ServiceName.Value)
                            {
                                match = true;
                            }
                        }

                        if (match) continue;

                        ServiceDescriptors.Add(sd);
                        service?.Descriptors.Add(sd);
                    }
                }
            }

        }

    }


}

