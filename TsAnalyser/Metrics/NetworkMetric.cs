using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Threading;

namespace TsAnalyser.Metrics
{
    [DataContract]
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
        private int _samplingPeriod = 5000;

        private int _currentPeriodPackets;
        private long _periodLongestTimeBetweenPackets;
        private int _periodShortestTimeBetweenPackets;
        private float _periodMaxBufferUsage;
        private int _periodData;
       // private float _periodAverageTimeBetweenPackets;
        private int _periodMaxPacketQueue;

        private DateTime _startTime;
        private long _timerFreq;
        private Timer _periodTimer;

        public NetworkMetric()
        {
            _ticksPerSecond = new TimeSpan(0, 0, 0, 1).Ticks;

            _periodTimer = new Timer(ResetPeriodTimerCallback, null, 0, SamplingPeriod);
        }

        private void ResetPeriodTimerCallback(object o)
        {
            lock (this)
            {
                LastPeriodEndTime = SampleTime;

                PeriodPackets = _currentPeriodPackets;
                _currentPeriodPackets = 0;

                PeriodLongestTimeBetweenPackets = _periodLongestTimeBetweenPackets;
                _periodLongestTimeBetweenPackets = 0;

                PeriodShortestTimeBetweenPackets = _periodShortestTimeBetweenPackets;
                _periodShortestTimeBetweenPackets = 0;

                PeriodMaxNetworkBufferUsage = _periodMaxBufferUsage;
                _periodMaxBufferUsage = 0;

                PeriodData = _periodData;
                _periodData = 0;

                //PeriodAverageTimeBetweenPackets = _periodAverageTimeBetweenPackets;
                //_periodAverageTimeBetweenPackets = 0;

                PeriodMaxPacketQueue = _periodMaxPacketQueue;
                _periodMaxPacketQueue = 0;

                SampleCount++;

            }
        }

        [DataMember]
        public string SampleTime => DateTime.UtcNow.ToString("o");
        
        [DataMember]
        public string LastPeriodEndTime { get; private set; }

        [DataMember]
        public long SampleCount { get; private set; }

        [DataMember]
        public string MulticastAddress { get; set; }

        [DataMember]
        public int  MulticastGroup { get; set; }

        /// <summary>
        /// Defines the internal sampling period in milliseconds - each time the sampling period has rolled over during packet addition, the periodic values reset.
        /// The values returned by all 'Period' properties represent the values gathered within the last completed period.
        /// </summary>
        [DataMember]
        public int SamplingPeriod
        {
            get { return _samplingPeriod; }
            set
            {
                _samplingPeriod = value;
                ResetPeriodTimerCallback(null);
                _periodTimer = new Timer(ResetPeriodTimerCallback, null, 0, SamplingPeriod);
                
            }
        }

        /// <summary>
        /// All time total of packets received (unless explicitly reset)
        /// </summary>
        [DataMember]
        public long TotalPackets { get; private set; }

        /// <summary>
        /// Total of packets received within the last complete sampling period
        /// </summary>
        [DataMember]
        public long PeriodPackets { get; private set; }

        /// <summary>
        /// Total data volume sum of all packets received (unless explicitly reset)
        /// </summary>
        [DataMember]
        public long TotalData { get; private set; }

        /// <summary>
        /// Total data volume sum of all packets received within the last complete sampling period
        /// </summary>
        [DataMember]
        public long PeriodData { get; private set; }

        /// <summary>
        /// Instantaneous bitrate, sampled over the last complete second
        /// </summary>
        [DataMember]
        public long CurrentBitrate { get; private set; }

        /// <summary>
        /// Highest per-second bitrate measured since start (unless explicitly reset)
        /// </summary>
        [DataMember]
        public long HighestBitrate { get; private set; }

        //TODO: This
        /// <summary>
        /// Highest per-second bitrate measured within the last complete sampling period
        /// </summary>
        //[DataMember]
        //public long PeriodHighestBitrate { get; private set; }

        /// <summary>
        /// Lowest per-second bitrate measured since start (unless explicitly reset)
        /// </summary>
        [DataMember]
        public long LowestBitrate { get; private set; } = 999999999;
        
        ////TODO: This
        ///// <summary>
        ///// Lowest per-second bitrate measured within the last complete sampling period
        ///// </summary>
        //[DataMember]
        //public long PeriodLowestBitrate { get; private set; }

        /// <summary>
        /// All-time average bitrate measured since start (unless explicitly reset)
        /// </summary>
        [DataMember]
        public long AverageBitrate => (long) (TotalData/DateTime.UtcNow.Subtract(_startTime).TotalSeconds) * 8;
        
        /// <summary>
        /// Average bitrate measured within the last complete sampling period
        /// </summary>
        [DataMember]
        public long PeriodAverageBitrate => (PeriodData / (SamplingPeriod/1000)) * 8;

        /// <summary>
        /// Packets received within the last complete second
        /// </summary>
        [DataMember]
        public int PacketsPerSecond { get; private set; }

        /// <summary>
        /// Current level of network buffer usage.
        /// A high value indicates TS Analyser is unable to keep up with the inbound rate and may not account for all packets under overflow conditions
        /// </summary>
        public float NetworkBufferUsage
        {
            get
            {
                if (UdpClient == null) return -1;
                float avail = UdpClient.Available;
                return avail/UdpClient.Client.ReceiveBufferSize*100;
            }
        }

        /// <summary>
        /// The highest network buffer usage since since start (unless explicitly reset)
        /// </summary>
        [DataMember]
        public float MaxNetworkBufferUsage { get; private set; }

        /// <summary>
        /// The highest network buffer usage within the last sampling period
        /// </summary>
        [DataMember]
        public float PeriodMaxNetworkBufferUsage { get; private set; }
        
        /// <summary>
        /// Instantaneous time between last two received packets
        /// </summary>
        public long TimeBetweenLastPacket { get; private set; }

        /// <summary>
        /// All-time longest time between two received packets (unless explicitly reset)
        /// </summary>
        [DataMember]
        public long LongestTimeBetweenPackets { get; private set; }

        /// <summary>
        /// Longest time between two received packets within the last sampling period
        /// </summary>
        [DataMember]
        public long PeriodLongestTimeBetweenPackets { get; private set; }

        /// <summary>
        /// All-time shortest time between two received packets (unless explicitly reset)
        /// </summary>
        [DataMember]
        public long ShortestTimeBetweenPackets { get; private set; }

        /// <summary>
        /// Shortest time between two received packets within the last sampling period
        /// </summary>
        [DataMember]
        public long PeriodShortestTimeBetweenPackets { get; private set; }

        ////TODO: This
        ///// <summary>
        ///// All-time average time between two received packets (unless explicitly reset)
        ///// </summary>
        //[DataMember]
        //public float AverageTimeBetweenPackets { get; private set; }

        ////TODO: This
        ///// <summary>
        ///// Average time between two received packets within the last sampling period
        ///// </summary>
        //[DataMember]
        //public float PeriodAverageTimeBetweenPackets { get; private set; }
        
        /// <summary>
        /// Current count of packets waiting in queue for processing
        /// </summary>
        [DataMember]
        public float CurrentPacketQueue { get; private set; }

        /// <summary>
        /// All-time maximum value for count of packets waiting in queue (unless explicitly reset)
        /// </summary>
        [DataMember]
        public float MaxPacketQueue { get; private set; }

        /// <summary>
        /// Maximum value for count of packets waiting in queue within the last sampling period
        /// </summary>
        [DataMember]
        public float PeriodMaxPacketQueue { get; private set; }


        /// <summary>
        /// Any time the value for time between packets exceeds this value, an event will be raised.
        /// </summary>
        public int MaxIat { get; set; } 

        public UdpClient UdpClient { get; set; }

        public static long AccurateCurrentTime()
        {
            long time;
            QueryPerformanceCounter(out time);
            return time;
        }

        [DllImport(Lib)]
        private static extern int QueryPerformanceCounter(out long count);

        [DllImport(Lib)]
        private static extern bool QueryPerformanceFrequency(out long lpFrequency);

        public void AddPacket(byte[] data, long recvTime, int currentQueueSize)
        {
            lock (this)
            {
                if (TotalPackets == 0)
                {
                    RegisterFirstPacket();
                }

                _currentPacketTime = recvTime;
                CurrentPacketQueue = currentQueueSize;

                if (MaxPacketQueue < currentQueueSize) MaxPacketQueue = currentQueueSize;

                if (_periodMaxPacketQueue < currentQueueSize) _periodMaxPacketQueue = currentQueueSize;

                var timeBetweenLastPacket = (_currentPacketTime - _lastPacketTime)*1000;

                timeBetweenLastPacket = timeBetweenLastPacket/_timerFreq;

                TimeBetweenLastPacket = timeBetweenLastPacket;

                if ((MaxIat > 0) && (TimeBetweenLastPacket > MaxIat))
                {
                    OnExcessiveIat(timeBetweenLastPacket);
                }

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

                    if (TimeBetweenLastPacket > _periodLongestTimeBetweenPackets)
                        _periodLongestTimeBetweenPackets = TimeBetweenLastPacket;

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
                _currentPeriodPackets++;
                TotalData += data.Length;
                _periodData += data.Length;

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
                        CurrentBitrate = _dataThisSecond * 8;
                        if (CurrentBitrate > HighestBitrate) HighestBitrate = CurrentBitrate;
                        if (CurrentBitrate < LowestBitrate) LowestBitrate = CurrentBitrate;

                        _dataThisSecond = 0;
                        _currentSampleTime = DateTime.Now.Ticks;
                    }
                }

                var buffVal = NetworkBufferUsage;

                if (buffVal > _periodMaxBufferUsage)
                {
                    _periodMaxBufferUsage = buffVal;
                }

                if (buffVal > MaxNetworkBufferUsage)
                {
                    MaxNetworkBufferUsage = buffVal;
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
        }

        private void RegisterFirstPacket()
        {
            _startTime = DateTime.UtcNow;
            _currentSampleTime = _startTime.Ticks;
            QueryPerformanceFrequency(out _timerFreq);
            QueryPerformanceCounter(out _lastPacketTime);
        }
    
        public delegate void IatEventHandler(object sender, IatEventArgs args);

        public event IatEventHandler ExcessiveIat;

        protected virtual void OnExcessiveIat(long measuredIat)
        {
            var handler = ExcessiveIat;

            var args = new IatEventArgs()
            {
                MaxIat = MaxIat,
                MeasuredIat = measuredIat
            };

            handler?.Invoke(this, args);

        }
        
        public event  EventHandler BufferOverflow;

        protected virtual void OnBufferOverflow()
        {
            var handler = BufferOverflow;
            if (_bufferOverflow) return;
            handler?.Invoke(this, EventArgs.Empty);
            _bufferOverflow = true;
        }
    }

    public class IatEventArgs : EventArgs
    {
        public int MaxIat { get; set; }
        public long MeasuredIat { get; set; }
    }
}