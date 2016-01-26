using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CommandLine;
using CommandLine.Text;

namespace TsAnalyser
{
    // Define a class to receive parsed values
    internal class Options 
    {
        [Option('m', "multicastaddress", Required = true,
       HelpText = "Input multicast address to read from.")]
        public string MulticastAddress { get; set; }

        [Option('g', "mulicastgroup", Required = true,
        HelpText = "Input multicast group port to read from.")]
        public int MulticastGroup { get; set; }

        [Option('l', "logfile", Required = false,
        HelpText = "Optional file to record events to.")]
        public string LogFile { get; set; }

        [Option('a', "adapter", Required = false,
        HelpText = "IP address of the adapter to listen for multicasts (if not set, tries first binding adapter).")]
        public string AdapterAddress { get; set; }
        
        [ParserState]
        public IParserState LastParserState { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this,
                current => HelpText.DefaultParsingErrorsHandler(this, current));
        }

    }
}
