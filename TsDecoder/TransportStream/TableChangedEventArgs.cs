namespace TsDecoder.TransportStream
{
    public class TableChangedEventArgs : System.EventArgs
    {
        public int TablePid { get; set; }
        public string Message { get; set; }
    }
}