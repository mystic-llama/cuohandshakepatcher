using System;
using System.Linq;
using System.Text;

namespace CuoHandshakeFix
{
    internal static class ServerCheck
    {
        private static readonly ulong C1 = 2394712817uL;
        private static readonly ulong C2 = 3875765969uL;
        private static readonly ulong C3 = 495865383uL;
        private static readonly ulong C4 = 1486448077uL;
        private static readonly ulong C5 = 2692095333uL;
        private static readonly ulong C6 = 3947146443uL;

        public static byte[] ComputeResponse(byte[] receivedPacket, string clientVersion)
        {
            uint token = GetToken(receivedPacket);
            long value = new Random().Next(0, int.MaxValue);
            string s = $"{value}client.exe_{clientVersion}";
            uint num = Waterhash(Encoding.ASCII.GetBytes(s));
            string s2 = $"{num}{token}";
            return BuildPacket(Waterhash(Encoding.ASCII.GetBytes(s2)), num);
        }

        private static uint GetToken(byte[] packet)
        {
            if (packet.Length < 5)
                throw new ArgumentException("Packet too short");
            return (uint)((packet[1] << 24) | (packet[2] << 16) | (packet[3] << 8) | packet[4]);
        }

        private static byte[] BuildPacket(uint hash2, uint hash1)
        {
            byte[] obj = new byte[9] { 213, 0, 0, 0, 0, 0, 0, 0, 0 };
            byte[] bytes = BitConverter.GetBytes(hash2);
            byte[] bytes2 = BitConverter.GetBytes(hash1);
            obj[1] = bytes[3]; obj[2] = bytes[2]; obj[3] = bytes[1]; obj[4] = bytes[0];
            obj[5] = bytes2[3]; obj[6] = bytes2[2]; obj[7] = bytes2[1]; obj[8] = bytes2[0];
            return obj;
        }

        private static uint Waterhash(byte[] data, ulong param3 = 14097894508562428199uL)
        {
            int num = data.Length;
            int i = 0;
            ulong num2 = param3;
            for (; num - i >= 16; i += 16)
            {
                ulong num3 = BitConverter.ToUInt32(data, i);
                long num4 = BitConverter.ToUInt32(data, i + 4);
                ulong num5 = BitConverter.ToUInt32(data, i + 8);
                ulong num6 = (BitConverter.ToUInt32(data, i + 12) ^ C3) * (num5 ^ C4);
                ulong num7 = ((ulong)num4 ^ C1) * (num3 ^ C2);
                num7 = num2 + (num7 - (num7 >> 32));
                num7 *= num6 - (num6 >> 32);
                num2 = num7 - (num7 >> 32);
            }
            ulong num8 = num2 + C6;
            int num9 = num - i;
            ulong num10 = 0uL;
            ulong num11 = 0uL;
            byte[] tail = data.Skip(i).ToArray();
            switch (num9)
            {
                case 1:
                    num10 = rd8() ^ C2;
                    num8 ^= C1;
                    num8 = num10 * num8 - (num10 * num8 >> 32);
                    break;
                case 2:
                    num10 = rd() ^ C3;
                    num8 ^= C4;
                    num8 = num10 * num8 - (num10 * num8 >> 32);
                    break;
                case 3:
                {
                    num10 = rd8(2) ^ C1;
                    ulong num13 = rd32() & 0xFFFF;
                    num8 ^= num13;
                    num8 = num10 * num8 - (num10 * num8 >> 32);
                    break;
                }
                case 4:
                {
                    num10 = rd(2) ^ C4;
                    ulong num12 = rd32() & 0xFFFF;
                    num8 ^= num12;
                    num8 = num10 * num8 - (num10 * num8 >> 32);
                    break;
                }
                case 5:
                    num8 = (num8 ^ rd32()) * (rd8(4) ^ C2);
                    num8 -= num8 >> 32;
                    break;
                case 6:
                    num8 = (num8 ^ rd32()) * (rd() ^ C2);
                    num8 -= num8 >> 32;
                    break;
                case 7:
                    num10 = ((rd(4) << 8) | rd8(6)) ^ C2;
                    num8 ^= rd32();
                    num8 = num10 * num8 - (num10 * num8 >> 32);
                    break;
                case 8:
                    num10 = rd32(4) ^ C5;
                    num8 ^= rd32();
                    num8 = num10 * num8 - (num10 * num8 >> 32);
                    break;
                case 9:
                    num11 = rd8(8) ^ C4;
                    num10 = num8 ^ C3;
                    num11 *= num10;
                    num11 -= num11 >> 32;
                    num8 = (rd32(4) ^ C1) * (rd32() ^ num8);
                    num8 = (num8 - (num8 >> 32)) ^ num11;
                    break;
                case 10:
                    num11 = (rd(8) ^ C4) * num8;
                    num11 -= num11 >> 32;
                    num8 = (rd32(4) ^ C1) * (rd32() ^ num8);
                    num8 = (num8 - (num8 >> 32)) ^ num11;
                    break;
                case 11:
                    num11 = ((rd(8) << 8) | rd8(10)) ^ C4;
                    num11 *= num8;
                    num11 -= num11 >> 32;
                    num8 = (rd32(4) ^ C1) * (rd32() ^ num8);
                    num8 = (num8 - (num8 >> 32)) ^ num11;
                    break;
                case 12:
                    num11 = (rd32(8) ^ num8) * C3;
                    num11 -= num11 >> 32;
                    num8 = (rd32(4) ^ C1) * (rd32() ^ num8);
                    num8 = (num8 - (num8 >> 32)) ^ num11;
                    break;
                case 13:
                    num11 = rd32(8) ^ num8;
                    num10 = rd8(12) ^ C3;
                    num11 *= num10;
                    num11 -= num11 >> 32;
                    num8 = (rd32(4) ^ C1) * (rd32() ^ num8);
                    num8 = (num8 - (num8 >> 32)) ^ num11;
                    break;
                case 14:
                    num11 = rd32(8) ^ num8;
                    num10 = rd(12) ^ C3;
                    num11 *= num10;
                    num11 -= num11 >> 32;
                    num8 = (rd32(4) ^ C1) * (rd32() ^ num8);
                    num8 = (num8 - (num8 >> 32)) ^ num11;
                    break;
                case 15:
                    num11 = ((rd(12) << 8) | rd8(14)) ^ C3;
                    num10 = rd32(8) ^ num8;
                    num11 *= num10;
                    num11 -= num11 >> 32;
                    num8 = (rd32(4) ^ C1) * (rd32() ^ num8);
                    num8 = (num8 - (num8 >> 32)) ^ num11;
                    break;
            }
            ulong num14 = ((num8 << 16) ^ num8) * ((ulong)num ^ C5);
            return (uint)((num14 - (num14 >> 32)) & 0xFFFFFFFFu);

            ulong rd(int offset = 0)
            {
                byte[] array = new byte[2];
                for (int j = 0; j < 2 && offset + j < tail.Length; j++)
                    array[j] = tail[offset + j];
                return BitConverter.ToUInt16(array, 0);
            }
            ulong rd32(int offset = 0)
            {
                byte[] array = new byte[4];
                for (int j = 0; j < 4 && offset + j < tail.Length; j++)
                    array[j] = tail[offset + j];
                return BitConverter.ToUInt32(array, 0);
            }
            ulong rd8(int offset = 0)
            {
                if (offset >= tail.Length) return 0uL;
                return tail[offset];
            }
        }
    }
}
