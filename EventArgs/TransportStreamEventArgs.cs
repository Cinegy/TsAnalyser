using System;

namespace TsAnalyser.Metrics
{
    public class TransportStreamEventArgs : EventArgs
    {
        public int TsPid { get; set; }
    }
}