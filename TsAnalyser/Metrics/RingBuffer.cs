using System;
using System.IO;
using System.Threading;

namespace TsAnalyser.Metrics
{
    internal class RingBuffer
    {
        private byte[][] _buffer;
        private long[] _addTimestamp;
        private int[] _dataLength;

        private ushort _lastAddPos;
        private ushort _lastRemPos;

        private const int PacketSize = 1500;
        private readonly object _lockObj = new object();

        public RingBuffer()
        {
            ResetBuffers();
        }

        private void ResetBuffers()
        {
            lock (_lockObj)
            {
                //allocate buffer and zero
                _buffer = new byte[ushort.MaxValue + 1][];
                _addTimestamp = new long[ushort.MaxValue + 1];
                _dataLength = new int[ushort.MaxValue + 1];

                for (var n = 0; n <= ushort.MaxValue; ++n)
                {
                    _buffer[n] = new byte[PacketSize];
                }
            }
        }

        /// <summary>
        /// Add a packet into the ring buffer.
        /// </summary>
        /// <param name="data"></param>
        public void Add(ref byte[] data)
        {
            lock (_lockObj)
            {
                if (data.Length <= PacketSize)
                {
                    //good data size
                    Buffer.BlockCopy(data, 0, _buffer[_lastAddPos], 0, data.Length);
                    _dataLength[_lastAddPos] = data.Length;
                    _addTimestamp[_lastAddPos++] = DateTime.UtcNow.Ticks;
                }
                else
                {
                    throw new InvalidDataException("Jumbo packets not supported");
                }
            }
        }

        /// <summary>
        /// Get the oldest element from the ring buffer - blocks if no data is yet available
        /// </summary>
        /// <returns></returns>
        public int Remove(ref byte[] dataBuffer,out int dataLength, out long addedAtTimestamp)
        {
            while(true)
            {
                lock (_lockObj)
                {
                    if (_lastRemPos != _lastAddPos)
                    {
                        dataLength = _dataLength[_lastRemPos];
                        addedAtTimestamp = _addTimestamp[_lastRemPos];

                        if (dataBuffer.Length < dataLength)
                            return dataLength;
                        
                        Buffer.BlockCopy(_buffer[_lastRemPos++], 0, dataBuffer, 0, dataLength);
                        return 0;
                    }
                }
                Thread.Sleep(1);
            }
        }

    

    }
}