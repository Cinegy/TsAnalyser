using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TsDecoder.Tables;

namespace TsDecoder.TransportStream
{
    public class TsDecoder
    {

        public ProgramAssociationTable ProgramAssociationTable => _patFactory.ProgramAssociationTable;
        public ServiceDescriptionTable ServiceDescriptionTable => _sdtFactory.ServiceDescriptionTable;

        public List<ProgramMapTable> ProgramMapTables { get; private set; }
        
        private ProgramAssociationTableFactory _patFactory;
        private ServiceDescriptionTableFactory _sdtFactory;
        private List<ProgramMapTableFactory> _pmtFactories;


        public delegate void TableChangeEventHandler(object sender, TableChangedEventArgs args);

        public TsDecoder()
        {
            SetupFactories();
        }
        
        public void AddPacket(TsPacket newPacket)
        {
            try
            {
                if (newPacket.TransportErrorIndicator) return;

                switch (newPacket.Pid)
                {
                    case (short)PidType.PatPid:
                        _patFactory.AddPacket(newPacket);
                        break;
                    case (short)PidType.SdtPid:
                        _sdtFactory.AddPacket(newPacket);
                        break;
                    default:
                        CheckPmt(newPacket);
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Exception generated within AddPacket method: " + ex.Message);
            }
        }

        public ServiceDescriptor GetServiceDescriptorForProgramNumber(int? programNumber)
        {
            if (programNumber == null) return null;

            var serviceDescItem = _sdtFactory?.ServiceDescriptionItems?.SingleOrDefault(
                                  i => i.ServiceId == programNumber);

            var serviceDesc =
                serviceDescItem?.Descriptors?.SingleOrDefault(sd => (sd as ServiceDescriptor) != null) as ServiceDescriptor;

            return serviceDesc;
        }

        public T GetDescriptorForProgramNumberByTag<T>( int? programNumber, int streamType, int descriptorTag)  where T : class
        {
            if (programNumber == null) return null;
            
            var selectedPmt = ProgramMapTables?.FirstOrDefault(t => t.ProgramNumber == programNumber);

            if (selectedPmt == null) return null;

            var selectedDesc = default(T);

            foreach (var esStream in selectedPmt.EsStreams)
            {
                if (esStream.StreamType != streamType) continue;

                selectedDesc = esStream.Descriptors.SingleOrDefault(d => d.DescriptorTag == descriptorTag) as T;

                if (selectedDesc != null) break;
            }
        
            return selectedDesc;
            
        }

        public EsInfo GetEsStreamForProgramNumberByTag(int? programNumber, int streamType, int descriptorTag) 
        {
            if (programNumber == null) return null;

            var selectedPmt = ProgramMapTables?.FirstOrDefault(t => t.ProgramNumber == programNumber);

            if (selectedPmt == null) return null;

            foreach (var esStream in selectedPmt.EsStreams)
            {
                if (esStream.StreamType != streamType) continue;

                var desc = esStream.Descriptors.SingleOrDefault(d => d.DescriptorTag == descriptorTag);

                if (desc != null) return esStream;
               
            }

            return null;
        }

        private void CheckPmt(TsPacket tsPacket)
        {
            if (ProgramAssociationTable == null) return;

            if (tsPacket.Pid == 0x0010)
            {
                //Pid 0x0010 is a NIT packet
                //TODO: Decode NIT, and store
                return;
            }

            var contains = false;
            foreach (var pid in ProgramAssociationTable.Pids)
            {
                if (!Equals(pid, tsPacket.Pid)) continue;
                contains = true;
                break;
            }

            if (!contains) return;

            ProgramMapTableFactory selectedPmt = null;
            foreach (var t in _pmtFactories)
            {
                if (t.TablePid != tsPacket.Pid) continue;
                selectedPmt = t;
                break;
            }

            if (selectedPmt == null)
            {
                selectedPmt = new ProgramMapTableFactory();
                selectedPmt.TableChangeDetected += _pmtFactory_TableChangeDetected;
                _pmtFactories?.Add(selectedPmt);
            }

            selectedPmt.AddPacket(tsPacket);
        }
        
        private void SetupFactories()
        {
            _patFactory = new ProgramAssociationTableFactory();
            _patFactory.TableChangeDetected += _patFactory_TableChangeDetected;
            _pmtFactories = new List<ProgramMapTableFactory>(16);
            ProgramMapTables = new List<ProgramMapTable>(16);

            _sdtFactory = new ServiceDescriptionTableFactory();
            _sdtFactory.TableChangeDetected += _sdtFactory_TableChangeDetected;
        }

        public ProgramMapTable GetSelectedPmt(int programNumber)
        {
            ProgramMapTable pmt;

            if (programNumber == 0)
            {
                if (ProgramMapTables?.Count == 0) return null;
                if (ProgramAssociationTable == null) return null;
                //without a passed program number, use the default program
                if (ProgramMapTables?.Count <
                    (ProgramAssociationTable?.Pids?.Length - 1)) return null;

                pmt = ProgramMapTables?.OrderBy(t => t.ProgramNumber).FirstOrDefault();
            }
            else
            {
                pmt = ProgramMapTables?.SingleOrDefault(t => t.ProgramNumber == programNumber);
            }

            return pmt;
        }

        private void _sdtFactory_TableChangeDetected(object sender, TransportStreamEventArgs e)
        {
            try
            {
                var name = GetServiceDescriptorForProgramNumber(ProgramMapTables.FirstOrDefault()?.ProgramNumber);
                var message =
                    $"SDT {e.TsPid} Refreshed: {name?.ServiceName} - {name?.ServiceProviderName} (Version {ServiceDescriptionTable?.VersionNumber}, Section {ServiceDescriptionTable?.SectionNumber})";

                OnTableChangeDetected(new TableChangedEventArgs() { Message = message, TablePid = e.TsPid });
            }
            catch(Exception ex)
            {
                Debug.WriteLine("Problem reading service name: " + ex.Message);   
            }
        }

        private void _pmtFactory_TableChangeDetected(object sender, TransportStreamEventArgs e)
        {
            string message;
            lock (this)
            {
                var fact = sender as ProgramMapTableFactory;

                if (fact == null) return;

                var selectedPmt = ProgramMapTables?.FirstOrDefault(t => t.Pid == e.TsPid);

                if (selectedPmt != null)
                {
                    ProgramMapTables?.Remove(selectedPmt);
                    message = $"PMT {e.TsPid} refreshed";
                }
                else
                {
                    message = $"PMT {e.TsPid} added";
                }

                ProgramMapTables?.Add(fact.ProgramMapTable);
            }

            OnTableChangeDetected(new TableChangedEventArgs() { Message = message, TablePid = e.TsPid });
        }

        private void _patFactory_TableChangeDetected(object sender, TransportStreamEventArgs e)
        {
            _pmtFactories = new List<ProgramMapTableFactory>(16);
            ProgramMapTables = new List<ProgramMapTable>(16);

            _sdtFactory = new ServiceDescriptionTableFactory();
            _sdtFactory.TableChangeDetected += _sdtFactory_TableChangeDetected;

            OnTableChangeDetected(new TableChangedEventArgs() {Message = "PAT refreshed - resetting all factories" , TablePid = e.TsPid});
        }

        //A decoded table change has been processed
        public event TableChangeEventHandler TableChangeDetected;

        private void OnTableChangeDetected(TableChangedEventArgs args)
        {   
            var handler = TableChangeDetected;
            handler?.Invoke(this, args);
        }
    }


}

