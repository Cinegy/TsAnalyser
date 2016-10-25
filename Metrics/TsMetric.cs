using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TsAnalyser.Tables;
using TsAnalyser.TsElements;

namespace TsAnalyser.Metrics
{
    public class TsMetric
    {

        public ProgramAssociationTable ProgramAssociationTable => _patFactory.ProgramAssociationTable;
        public ServiceDescriptionTable ServiceDescriptionTable => _sdtFactory.ServiceDescriptionTable;

        public List<ProgramMapTable> ProgramMapTables { get; private set; }

        public object ProgramMapTableLock { get; } = new object();
        public List<ServiceDescriptor> ServiceDescriptors { get; private set; }
        public object ServiceDescriptionTableLock { get; } = new object();
        public bool DecodeServiceDescriptions { get; set; }

        private ProgramAssociationTableFactory _patFactory;
        private ServiceDescriptionTableFactory _sdtFactory;
        private List<ProgramMapTableFactory> _pmtFactories;

        public TsMetric()
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
                        if(DecodeServiceDescriptions) _sdtFactory.AddPacket(newPacket);
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

        private void CheckPmt(TsPacket tsPacket)
        {
            if (ProgramAssociationTable == null) return;

            if (tsPacket.Pid == 0x0010)
            {
                //Pid 0x0010 is a NIT packet
                //TODO: Decode NIT, and store
                return;
            }

            if (!ProgramAssociationTable.Pids.Contains(tsPacket.Pid)) return;

            var selectedPmt = _pmtFactories?.FirstOrDefault(t => t.TablePid == tsPacket.Pid);
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

        private void _sdtFactory_TableChangeDetected(object sender, TransportStreamEventArgs args)
        {
            Debug.WriteLine($"SDT {args.TsPid} refreshed");

            if (ServiceDescriptionTable?.Items == null || ServiceDescriptionTable.TableId != 0x42)
            {
                return;
            }

            if (ServiceDescriptors == null)
            {
                ServiceDescriptors = new List<ServiceDescriptor>(16);
            }

            foreach (var item in ServiceDescriptionTable.Items)
            {
                foreach (var descriptor in item.Descriptors.Where(d => d.DescriptorTag == 0x48))
                {
                    var sd = descriptor as ServiceDescriptor;

                    if (sd == null) continue;

                    var service = ProgramMapTables?.SingleOrDefault(i => i?.ProgramNumber == item.ServiceId);


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

        private void _pmtFactory_TableChangeDetected(object sender, TransportStreamEventArgs e)
        {
            var fact = sender as ProgramMapTableFactory;

            if (fact == null) return;

            var selectedPmt = ProgramMapTables?.FirstOrDefault(t => t.Pid == e.TsPid);

            if (selectedPmt != null)
            {
                ProgramMapTables?.Remove(selectedPmt);
                Debug.WriteLine($"PMT {e.TsPid} refreshed");
            }
            else
            {
                Debug.WriteLine($"PMT {e.TsPid} added");
            }

            ProgramMapTables?.Add(fact.ProgramMapTable);

        }

        private static void _patFactory_TableChangeDetected(object sender, TransportStreamEventArgs e)
        {
            Debug.WriteLine("PAT refreshed");
        }
    }


}

