using CommandLine;
using CommandLine.Text;

namespace TsAnalyser
{
    internal class Options
    {
        [Option('q', "quiet", Required = false, Default = false,
        HelpText = "Don't print anything to the console")]
        public bool SuppressOutput { get; set; }

        [Option('l', "logfile", Required = false,
        HelpText = "Optional file to record events to.")]
        public string LogFile { get; set; }
        
        [Option('w', "webservices", Required = false, Default = false,
        HelpText = "Enable Web Services (control page available on http://localhost:8124/index.html by default).")]
        public bool EnableWebServices { get; set; }

        [Option('u', "serviceurl", Required = false, Default = "http://localhost:8124/",
        HelpText = "Optional service URL for REST web services (must change if running multiple instances with web services enabled).")]
        public string ServiceUrl { get; set; }

        [Option('s', "skipdecodetransportstream", Required = false, Default = false,
        HelpText = "Optional instruction to skip decoding further TS and DVB data and metadata")]
        public bool SkipDecodeTransportStream { get; set; }

        [Option('t', "teletextdecode", Required = false, Default = false,
        HelpText = "Optional instruction to decode DVB teletext subtitles from default program (experimental)")]
        public bool DecodeTeletext { get; set; }
        
        [Option('p', "programnumber", Required = false,
        HelpText = "Pick a specific program / service to inspect (otherwise picks default).")]
        public ushort ProgramNumber { get; set; }

        [Option('d', "descriptortags", Required = false, Default = "",
        HelpText = "Comma separated tag values added to all log entries for instance and machine identification")]
        public string DescriptorTags { get; set; }

    }
    
    // Define a class to receive parsed values
    [Verb("stream", HelpText = "Stream from the network.")]
    internal class StreamOptions : Options
    {
        [Option('m', "multicastaddress", Required = true,
        HelpText = "Input multicast address to read from.")]
        public string MulticastAddress { get; set; }

        [Option('g', "mulicastgroup", Required = true,
        HelpText = "Input multicast group port to read from.")]
        public int MulticastGroup { get; set; }
        
        [Option('e', "timeserieslogfile", Required = false,
        HelpText = "Optional file to record time slice metric data to.")]
        public string TimeSeriesLogFile { get; set; }
        
        [Option('a', "adapter", Required = false,
        HelpText = "IP address of the adapter to listen for multicasts (if not set, tries first binding adapter).")]
        public string AdapterAddress { get; set; }
        
        [Option('n', "nortpheaders", Required = false, Default = false,
        HelpText = "Optional instruction to skip the expected 12 byte RTP headers (meaning plain MPEGTS inside UDP is expected")]
        public bool NoRtpHeaders { get; set; }
        
        [Option('i', "interarrivaltime", Required = false, Default = 40,
        HelpText = "Maximum permitted time between UDP packets before alarming.")]
        public int InterArrivalTimeMax { get; set; }
        
        [Option('h', "savehistoricaldata", Required = false, Default = false,
        HelpText = "Optional instruction to save and then flush to disk recent TS data on stream problems.")]
        public bool SaveHistoricalData { get; set; }

    }

    // Define a class to receive parsed values
    [Verb("read", HelpText = "Read from a file.")]
    class ReadOptions : Options
    {
        [Option('f', "filename", Required = false, Default = "",
        HelpText = "Allow a .TS file to be opened, instead of a Multicast (experimental)")]
        public string FileInput { get; set; }
        
    }
}
