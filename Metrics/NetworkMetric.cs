using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace TsAnalyser.Metrics
{
    public class NetworkMetric
    {
        private const string Lib = "kernel32.dll";
        private readonly long _ticksPerSecond;

        private bool _averagesReady;
        private bool _bufferOverflow;
        private long _currentPacketTime;
        private long _currentSampleTime;
        private int _currentSecond;
        private long _dataThisSecond;
        private long _lastPacketTime;
        private int _packetsThisSecond;

        private DateTime _startTime;
        private long _timerFreq;

        public NetworkMetric()
        {
            _ticksPerSecond = new TimeSpan(0, 0, 0, 1).Ticks;
        }

        public long TotalPackets { get; private set; }

        public long TotalData { get; private set; }

        public long CurrentBitrate { get; private set; }

        public long HighestBitrate { get; private set; }

        public long LowestBitrate { get; private set; } = 999999999;

        public long AverageBitrate => (long) (TotalData/DateTime.UtcNow.Subtract(_startTime).TotalSeconds);

        public int PacketsPerSecond { get; private set; }

        public float NetworkBufferUsage
        {
            get
            {
                if (UdpClient == null) return -1;
                float avail = UdpClient.Available;
                return avail/UdpClient.Client.ReceiveBufferSize*100;
            }
        }

        public long TimeBetweenLastPacket { get; set; }

        public long LongestTimeBetweenPackets { get; set; }

        public long ShortestTimeBetweenPackets { get; set; }

        public UdpClient UdpClient { get; set; }

        [DllImport(Lib)]
        private static extern int QueryPerformanceCounter(out long count);

        [DllImport(Lib)]
        private static extern bool QueryPerformanceFrequency(out long lpFrequency);

        public void AddPacket(byte[] data)
        {
            if (TotalPackets == 0)
            {
                RegisterFirstPacket();
            }

            QueryPerformanceCounter(out _currentPacketTime);

            var timeBetweenLastPacket = (_currentPacketTime - _lastPacketTime)*1000;

            timeBetweenLastPacket = timeBetweenLastPacket/_timerFreq;

            TimeBetweenLastPacket = timeBetweenLastPacket;

            _lastPacketTime = _currentPacketTime;

            if (TotalPackets == 1)
            {
                ShortestTimeBetweenPackets = TimeBetweenLastPacket;
                _currentSecond = DateTime.UtcNow.Second;
            }

            if (TotalPackets > 10)
            {
                if (TimeBetweenLastPacket > LongestTimeBetweenPackets)
                    LongestTimeBetweenPackets = TimeBetweenLastPacket;

                if (TimeBetweenLastPacket < ShortestTimeBetweenPackets)
                    ShortestTimeBetweenPackets = TimeBetweenLastPacket;

                if (DateTime.UtcNow.Second == _currentSecond)
                {
                    _packetsThisSecond++;
                }
                else
                {
                    PacketsPerSecond = _packetsThisSecond;
                    _packetsThisSecond = 1;
                    _currentSecond = DateTime.UtcNow.Second;
                }
            }

            TotalPackets++;
            TotalData += data.Length;

            if (DateTime.Now.Ticks - _currentSampleTime < _ticksPerSecond)
            {
                _dataThisSecond += data.Length;
            }
            else
            {
                if (!_averagesReady & (DateTime.UtcNow.Subtract(_startTime).TotalMilliseconds > 1000))
                    _averagesReady = true;

                if (_averagesReady)
                {
                    CurrentBitrate = _dataThisSecond;
                    if (CurrentBitrate > HighestBitrate) HighestBitrate = CurrentBitrate;
                    if (CurrentBitrate < LowestBitrate) LowestBitrate = CurrentBitrate;

                    _dataThisSecond = 0;
                    _currentSampleTime = DateTime.Now.Ticks;
                }
            }

            if (NetworkBufferUsage > 99)
            {
                OnBufferOverflow();
            }
            else
            {
                _bufferOverflow = false;
            }
        }

        public void RegisterFirstPacket()
        {
            _startTime = DateTime.UtcNow;
            _currentSampleTime = _startTime.Ticks;
            QueryPerformanceFrequency(out _timerFreq);
            QueryPerformanceCounter(out _lastPacketTime);
        }

        public event EventHandler BufferOverflow;

        protected virtual void OnBufferOverflow()
        {
            var handler = BufferOverflow;
            if (handler == null) return;
            if (_bufferOverflow) return;
            handler(this, System.EventArgs.Empty);
            _bufferOverflow = true;
        }
    }
}