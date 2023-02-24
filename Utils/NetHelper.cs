using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using static System.Text.RegularExpressions.Regex;
using System.Web;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

using Cyclonix.Utils;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Cyclonix
{
    public static class NetHelper
    {
        public static string GetLocalIPAddress()
        {
            var hostname = Dns.GetHostName();
            var entry = Dns.GetHostEntry(hostname);
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("192.168.0.1", 65530);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                return endPoint.Address.ToString();
            }
        }

        public static string GetPublicIPAddress()
        {
            try
            {
                return new WebClient().DownloadString("http://checkip.dyndns.org/").Match(@"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}").Value;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void GetPublicIPAddress(ref string ip)
        {
            ip = GetPublicIPAddress();
        }

        public static bool IsNetworkAvailable() => System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();

        public static string GetIPv4(this EndPoint remoteip)
        {
            var ip = remoteip.ToString();
            var iof = ip.IndexOf(']', 8, 16);
            var sub = ip.Substring(8, iof - 8);
            return sub;
        }

        static ushort[] powtens = new ushort[] { 1, 10, 100, 1000, 10000 };

        public static ushort GetPortFromLoopback(this EndPoint endPoint)
        {
            var ip = endPoint.ToString();
            var iof = ip.IndexOf(']', 8);

            if (iof == -1)
            {
                iof = ip.LastIndexOf(':', 8) - 1;
            }

            if (iof == -2)
            {

                iof = ip.IndexOf(':', 8) - 1;
            }

            ushort port = 0;
            var portLen = ip.Length - (iof + 2);

            const ushort ucs = 48;

            for (int i = iof + 2, k = portLen - 1; i < iof + 2 + portLen; i++, k--)
            {
                ushort c = ip[i];
                ushort v = (ushort)((c - ucs) * powtens[k]);
                port += v;
            }
            return port;
        }

        public static byte[] GetIPtoBytes(this EndPoint remoteip)
        {
            return GetIPtoBytes(remoteip.ToString());
        }

        public static byte[] GetIPtoBytes(IEnumerable<char> remoteip)
        {
            var ip = remoteip;
            var iof = ip.IndexOf(']', 8);
            int i = 8;

            if (iof == -1)
            {
                i = 0;
                iof = ip.IndexOf(':', 8);
            }

            var bytes = new byte[4];
            var num = new byte[3];
            var ctr = 0;
            var length = ip.Count();
            for (; i < length; i++)
            {
                var c = ip.ElementAt(i);
                if (c == '.')
                {
                    bytes[3]++; // bytes[3] is used as an indexer
                    if (ctr == 3)
                        bytes[bytes[3] - 1] = (byte)((num[0] - 48) * 100 + (num[1] - 48) * 10 + (num[2] - 48));
                    else if (ctr == 2)
                        bytes[bytes[3] - 1] = (byte)((num[0] - 48) * 10 + (num[1] - 48));
                    else
                        bytes[bytes[3] - 1] = (byte)((num[0] - 48));
                    num[0] = num[1] = num[2] = 0;
                    ctr = 0;
                }
                else if (c == ']' || c == ':')
                {
                    break;
                }
                else
                {
                    num[ctr] = (byte)c;
                    ctr++;
                }
            }
            if (ctr == 3)
                bytes[3] = (byte)((num[0] - 48) * 100 + (num[1] - 48) * 10 + (num[2] - 48));
            else if (ctr == 2)
                bytes[3] = (byte)((num[0] - 48) * 10 + (num[1] - 48));
            else
                bytes[3] = (byte)((num[0] - 48));
            return bytes;
        }

        static byte[] powlut = { 100, 10, 1 };
        public static uint GetIPtoInt(string remoteip)
        { 
            return GetIPtoInt(remoteip as IEnumerable<char>);
        }

        public static uint GetIPtoInt(IEnumerable<char> remoteip)
        {

            var ip = remoteip;
            var iof = ip.IndexOf(']', 8) - 1;
            int i = 8;

            if (iof < 0)
            {
                i = 0;
                iof = ip.IndexOf(':', 8) - 1;
            }

            if (iof < 0)
            {
                i = 0;
                iof = ip.IndexOf('/', 8) - 1;
            }

            if (iof < 0) iof = remoteip.Count() - 1;

            uint bytes = 0;
            var ctr = 2;
            byte bufbyte = 0;
            byte byteidx = 0;


            var to = i;
            var from = iof;
            for (i = from; i >= to; i--)
            {

                var c = remoteip.ElementAt(i);
                if (c == '.')
                {
                    ctr = 2;
                    bytes |= (uint)bufbyte << (8 * byteidx);
                    byteidx++;
                    bufbyte = 0;
                }
                else if (c == ']' || c == ':')
                {
                    break;
                }
                else
                {

                    bufbyte += (byte)(((byte)c - 48) * powlut[ctr]);
                    ctr--;
                }
            }

            bytes |= (uint)bufbyte << (8 * byteidx);

            return bytes;
        }

        public static int BytesToIntBigEndian(this byte[] bytes)
        {
            return
                (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
        }

        // https://stackoverflow.com/a/31041121
        public static bool IsInSubnet(uint ipv4, uint netipv4, uint netmaskipv4)
        {
            uint netstart = (netipv4 & netmaskipv4); // first ip in subnet
            uint netend = (netstart | ~netmaskipv4); // last ip in subnet
            if ((ipv4 >= netstart) && (ipv4 <= netend))
                return true;
            else
                return false;
        }
         
        public static uint CreateIPv4Mask(byte cidrbits)
        {
            if (cidrbits > 32) throw new Exception("Bits count exceeds 32");

            return uint.MaxValue << (32 - cidrbits);
        }
    }

}
