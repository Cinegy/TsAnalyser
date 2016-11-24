using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TsAnalyser.Metrics
{
[DataContract]
    public abstract class Metric
    {
        private int _samplingPeriod = 5000;
        private Timer _periodTimer;

        internal readonly long TicksPerSecond;
        internal DateTime StartTime;

        protected Metric()
        {
            TicksPerSecond = new TimeSpan(0, 0, 0, 1).Ticks;

            _periodTimer = new Timer(ResetPeriodTimerCallback, null, 0, SamplingPeriod);
        }
        
        public string SampleTime => DateTime.UtcNow.ToString("o");

        [DataMember]
        public string LastPeriodEndTime { get; private set; }

        [DataMember]
        public long SampleCount { get; private set; }

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

        internal virtual void ResetPeriodTimerCallback(object o)
        {
            lock (this)
            {
                LastPeriodEndTime = SampleTime;

                SampleCount++;

            }
        }
    }
}
