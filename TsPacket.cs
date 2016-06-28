using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace TsAnalyser
{
    public struct PesHdr
    {
        public long Pts;
        public long Dts;
        public int StartCode;
        public byte[] Payload;
    }

    public struct TsPacket
    {
        public byte SyncByte; //should always be 0x47 - indicates start of a TS packet
        public bool TransportErrorIndicator; //Set when a demodulator can't correct errors from FEC data - this would inform a stream processor to ignore the packet
        public bool PayloadUnitStartIndicator; //true = the start of PES data or PSI otherwise zero only. 
        public bool TransportPriority; //true = the current packet has a higher priority than other packets with the same PID.
        public short Pid; //Packet identifier flag, used to associate one packet with a set
        public short ScramblingControl; // '00' = Not scrambled, For DVB-CSA only:'01' = Reserved for future use, '10' = Scrambled with even key, '11' = Scrambled with odd key
        public bool AdaptationFieldExists;
        public bool ContainsPayload;
        public short ContinuityCounter;
        public PesHdr PesHeader;
        public byte[] Payload;
    }

    public class TsPacketFactory
    {
        private const byte SyncByte = 0x47;
        private const int TsPacketSize = 188;

        public static TsPacket[] GetTsPacketsFromData(byte[] data)
        {
            try
            {
                var maxPackets = (data.Length) / TsPacketSize;
                var tsPackets = new TsPacket[maxPackets];

                var start = FindSync(data, 0);
                var packetCounter = 0;

                while (start >= 0)
                {
                    var tsPacket = new TsPacket
                    {
                        SyncByte = data[start],
                        Pid = (short)(((data[start + 1] & 0x1F) << 8) + (data[start + 2])),
                        TransportErrorIndicator = (data[start + 1] & 0x80) != 0,
                        PayloadUnitStartIndicator = (data[start + 1] & 0x40) != 0,
                        TransportPriority = (data[start + 1] & 0x20) != 0,
                        ScramblingControl = (short)(data[start + 3] >> 6),
                        AdaptationFieldExists = (data[start + 3] & 0x20) != 0,
                        ContainsPayload = (data[start + 3] & 0x10) != 0,
                        ContinuityCounter = (short)(data[start + 3] & 0xF)
                    };

                    if (tsPacket.ContainsPayload & !tsPacket.TransportErrorIndicator)
                    {
                        var payloadOffs = start + 4;
                        var payloadSize = TsPacketSize - 4;

                        if (tsPacket.AdaptationFieldExists)
                        {
                            var adaptationFieldSize = 1 + data[payloadOffs];
                            payloadSize -= adaptationFieldSize;
                            payloadOffs += adaptationFieldSize;
                        }

                        if (tsPacket.PayloadUnitStartIndicator)
                        {
                            if (payloadOffs > (data.Length - 2) || data[payloadOffs] != 0 || data[payloadOffs + 1] != 0 || data[payloadOffs + 2] != 1)
                            {
#if DEBUG
                                Debug.WriteLine("PES syntax error: no PES startcode found, or payload offset exceeds boundary of data");
#endif
                            }
                            else
                            {
                                tsPacket.PesHeader = new PesHdr
                                {
                                    StartCode = 0x100 + data[payloadOffs + 3],
                                    Pts = -1,
                                    Dts = -1
                                };

                                var ptsDtsFlag = data[payloadOffs + 7] >> 6;

                                switch (ptsDtsFlag)
                                {
                                    case 2:
                                        tsPacket.PesHeader.Pts = Get_TimeStamp(2, data, payloadOffs + 9);
                                        break;
                                    case 3:
                                        tsPacket.PesHeader.Pts = Get_TimeStamp(3, data, payloadOffs + 9);
                                        tsPacket.PesHeader.Dts = Get_TimeStamp(1, data, payloadOffs + 14);
                                        break;
                                    case 1:
                                        throw new Exception("PES Syntax error: pts_dts_flag = 1");
                                }

                                var pesLength = 9 + data[payloadOffs + 8];
                                tsPacket.PesHeader.Payload = new byte[pesLength];
                                Buffer.BlockCopy(data, payloadOffs, tsPacket.PesHeader.Payload, 0, pesLength);

                                payloadOffs += pesLength;
                                payloadSize -= pesLength;
                            }
                        }

                        if (payloadSize < 1)
                        {
                            tsPacket.TransportErrorIndicator = true;
                        }
                        else
                        {
                            tsPacket.Payload = new byte[payloadSize];
                            Buffer.BlockCopy(data, payloadOffs, tsPacket.Payload, 0, payloadSize);
                        }
                    }

                    tsPackets[packetCounter++] = tsPacket;

                    start += TsPacketSize;

                    if (start >= data.Length)
                        break;
                    if (data[start] != SyncByte)
                        break;  // but this is strange!
                }

                return tsPackets;
            }

            catch (Exception ex)
            {
                Debug.WriteLine("Exception within GetTsPacketsFromData method: " + ex.Message);
            }

            return null;
        }

        private static long Get_TimeStamp(int code, IList<byte> data, int offs)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            if (code == 0)
            {
                Debug.WriteLine("Method has been called with incorrect code to match against - check for fault in calling method.");
                throw new Exception("PES Syntax error: 0 value timestamp code check passed in");
            }

            if ((data[offs + 0] >> 4) != code)
                throw new Exception("PES Syntax error: Wrong timestamp code");

            if ((data[offs + 0] & 1) != 1)
                throw new Exception("PES Syntax error: Invalid timestamp marker bit");

            if ((data[offs + 2] & 1) != 1)
                throw new Exception("PES Syntax error: Invalid timestamp marker bit");

            if ((data[offs + 4] & 1) != 1)
                throw new Exception("PES Syntax error: Invalid timestamp marker bit");

            long a = (data[offs + 0] >> 1) & 7;
            long b = (data[offs + 1] << 7) | (data[offs + 2] >> 1);
            long c = (data[offs + 3] << 7) | (data[offs + 4] >> 1);

            return (a << 30) | (b << 15) | c;
        }

        private static int FindSync(IList<byte> tsData, int offset)
        {
            if (tsData == null) throw new ArgumentNullException(nameof(tsData));
            
            //not big enough to be any kind of single TS packet
            if (tsData.Count < 188)
            {
                return -1;
            }

            try
            {
                for (var i = offset; i < tsData.Count; i++)
                {
                    //check to see if we found a sync byte
                    if (tsData[i] != SyncByte) continue;
                    if (i + 1 * TsPacketSize < tsData.Count && tsData[i + 1 * TsPacketSize] != SyncByte) continue;
                    if (i + 2 * TsPacketSize < tsData.Count && tsData[i + 2 * TsPacketSize] != SyncByte) continue;
                    if (i + 3 * TsPacketSize < tsData.Count && tsData[i + 3 * TsPacketSize] != SyncByte) continue;
                    if (i + 4 * TsPacketSize < tsData.Count && tsData[i + 4 * TsPacketSize] != SyncByte) continue;
                    // seems to be ok
                    return i;
                }
                return -1;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Problem in FindSync algorithm... : ", ex.Message);
                throw;
            }
        }
    }
}
