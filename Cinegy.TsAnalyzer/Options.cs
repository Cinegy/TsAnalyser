using CommandLine;

namespace Cinegy.TsAnalyzer
{
    internal class Options
    {
        [Option('q', "quiet", Required = false, Default = false,
        HelpText = "Don't print anything to the console")]
        public bool SuppressOutput { get; set; }
    
        [Option('s', "skipdecodetransportstream", Required = false, Default = false,
        HelpText = "Optional instruction to skip decoding further TS and DVB data and metadata")]
        public bool SkipDecodeTransportStream { get; set; }

        [Option('c', "teletextdecode", Required = false, Default = false,
        HelpText = "Optional instruction to decode DVB teletext subtitles / captions from default program")]
        public bool DecodeTeletext { get; set; }
        
        [Option("programnumber", Required = false,
        HelpText = "Pick a specific program / service to inspect (otherwise picks default).")]
        public ushort ProgramNumber { get; set; }

        [Option('d', "descriptortags", Required = false, Default = "",
        HelpText = "Comma separated tag values added to all log entries for instance and machine identification")]
        public string DescriptorTags { get; set; }
        
        [Option('v', "verboselogging", Required = false,
        HelpText = "Creates event logs for all discontinuities and skips.")]
        public bool VerboseLogging { get; set; }

        [Option('t', "telemetry", Required = false, Default = false,
        HelpText = "Enable telemetry to Cinegy Telemetry Server")]
        public bool TelemetryEnabled { get; set; }

        [Option('o', "organization", Required = false,
        HelpText = "Tag all telemetry with this organization (needed to indentify and access telemetry from Cinegy Analytics portal")]
        public string OrganizationId { get; set; }
    }
    
    // Define a class to receive parsed values using stream verb
    [Verb("stream", HelpText = "Stream from the network.")]
    internal class StreamOptions : Options
    {
        [Option('m', "multicastaddress", Required = false,
        HelpText = "Input multicast address to read from - if left blank, assumes unicast.")]
        public string MulticastAddress { get; set; }

        [Option('p', "port", Required = true,
        HelpText = "Input UDP network port to read from.")]
        public int UdpPort { get; set; }
        
        [Option('a', "adapter", Required = false,
        HelpText = "IP address of the adapter to listen for multicasts (if not set, tries first binding adapter).")]
        public string AdapterAddress { get; set; }
        
        [Option('n', "nortpheaders", Required = false, Default = false,
        HelpText = "Optional instruction to skip the expected 12 byte RTP headers (meaning plain MPEGTS inside UDP is expected")]
        public bool NoRtpHeaders { get; set; }
        
    }

    // Define a class to receive parsed values read verb
    [Verb("read", HelpText = "Read from a file.")]
    internal class ReadOptions : Options
    {
        [Option('f', "filename", Required = true,
        HelpText = "Allow a .TS file to be opened, instead of a Multicast (experimental)")]
        public string FileInput { get; set; }
        
    }
}
