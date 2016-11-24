using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization;
using Newtonsoft.Json.Linq;

namespace TsAnalyser.Logging
{
    [DataContract]
    internal class LogRecord
    {
        [DataMember]
        public string EventTime => DateTime.UtcNow.ToString("o");

        [DataMember]
        public string EventMessage { get; set; }

        [DataMember]
        public string EventCategory { get; set; }

        [DataMember]
        public string ProductName { get; set; }

        [DataMember]
        public string ProductVersion
            => FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion;

        [DataMember]
        public string EventKey { get; set; }

        [DataMember]
        public string EventTags { get; set; }
        
    }
}
