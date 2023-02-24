

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using static System.Math;

namespace Cyclonix.Utils
{

    public static class Helper
    {

        #region Math

        public static int Lerp(int firstFloat, int secondFloat, float by)
        {
            return (int)(firstFloat + (secondFloat - firstFloat) * by);
        }

        public static int PowXIn(int firstFloat, int secondFloat, float by, float x)
        {
            return (int)(firstFloat + (secondFloat - firstFloat) * -(Pow(by - 1, x) - 1));
        }

        public static float Lerp(float firstFloat, float secondFloat, float by)
        {
            return firstFloat * (1 - by) + secondFloat * by;
        }
        #endregion

        #region String

        static int timeroll => (int)(DateTime.Now.Millisecond * Math.Cos((DateTime.Now.Millisecond - 256) * 2));

        static Random stringDerand = new Random(timeroll);

        static readonly string keyspace = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

        public static string RandString(int len = 64)
        {
            var chars = new char[len];
            for (int i = len - 1; i >= 0; i--)
            {
                chars[i] = keyspace[stringDerand.Next(0, keyspace.Length)];
            }
            var buf = new string(chars);
            return buf;
        }

        public static string SubstringMax(this string s, int start, int length)
        {
            return s.Substring(start, Min(s.Length - start, length));
        }

        #endregion

        #region Extensions

        public static T Clamp<T>(this T v, T min, T max) where T : IComparable => v.CompareTo(max) == 1 ? max : v.CompareTo(min) == -1 ? min : v;

        public static string ToBinaryString(this int value)
            => BitConverter.GetBytes(value).ToBinaryString();

        public static string ToBinaryString(this uint value)
            => BitConverter.GetBytes(value).ToBinaryString();

        public static string ToBinaryString(this short value)
            => BitConverter.GetBytes(value).ToBinaryString();

        public static string ToBinaryString(this ushort value)
            => BitConverter.GetBytes(value).ToBinaryString();

        public static string ToBinaryString(this byte value)
            => (new byte[1] { value }).ToBinaryString();

        //public static string ToBinaryString(this sbyte value)
        //    => ToBinaryString(BitConverter.GetBytes(value));

        public static string ToBinaryString(this byte[] bytes)
        => (new BitField { bytes = bytes }).ToString();


        public static string ToByteString(this byte[] bytes)
        {
            return bytes.Aggregate("", (s, v) => s += v + " ");
        }

        public static string GetExceptionRecur(this Exception e)
        {
            var msg = e;
            string buf = msg.Message + " ";
            while (msg != null)
            {
                msg = msg.InnerException;
                if (msg != null)
                    buf += msg.Message + " ";
            }
            return buf;
        }

        static Encoding urf8encoder = Encoding.UTF8;
        static Encoding asciiencoder = Encoding.ASCII;

        public static byte[] UTF8Encode(this string s) => urf8encoder.GetBytes(s);
        public static string UTF8Decode(this byte[] s) => urf8encoder.GetString(s);
        public static string UTF8Decode(this ArraySegment<byte> s) => urf8encoder.GetString(s);

        public static byte[] ASCIIEncode(this string s) => asciiencoder.GetBytes(s);
        public static string ASCIIDecode(this byte[] s) => asciiencoder.GetString(s);
        public static string ASCIIDecode(this ArraySegment<byte> s) => asciiencoder.GetString(s);
        public static char[] ASCIIDecodeChars(this byte[] s) => asciiencoder.GetChars(s);

        public static string[] Split(this string s, string separator, StringSplitOptions options = StringSplitOptions.None)
        {
            return s.Split(new[] { separator }, options);
        }

        public static string WriteError(this Exception e)
        {
            var st = new StackTrace(e, true);

            // get the top stack frame
            var frame = st.GetFrame(0);

            // get the line number from the stack frame
            var line = frame.GetFileLineNumber();

            return e.Message + "\r\n" + e.StackTrace + "\r\n" + line;

        }

        public static bool PerCharCompare(this string s, string find, int from)
        {
            for (int i = from, j = 0; i < s.Length; i++)
            {
                if (s[i] == find[j])
                {
                    j++;
                    if (j == find.Length)
                        return true;
                }
                else
                {
                    return false;
                }
            }

            return false;
        }

        public static string Between(this string s, string border)
        {

            int startpos = s.IndexOf(border);
            if (startpos > -1)
            {

                int start = startpos + border.Length;
                int end = s.IndexOf(border, start + border.Length);
                if (end > -1)
                {

                    int length = end - start;
                    return s.Substring(start, length);
                }
            }
            return null;
        }

        public static string Between(this string s, char borderleft, char borderright)
        {

            int startpos = s.SubstepIndex(borderleft) + 1 - 1;
            if (startpos > -1)
            {

                int endpos = s.SubstepIndex(borderright, startpos + 1);

                if (endpos > -1)
                {

                    int length = endpos - startpos - 1;
                    return s.Substring(startpos + 1, length);
                }

            }
            return null;
        }

        public static string Between(this string s, string borderleft, string borderright)
        {

            int startpos = s.SubstepIndex(borderleft) + borderleft.Length - 1;
            if (startpos > -1)
            {

                int endpos = s.SubstepIndex(borderright, startpos);

                if (endpos > -1)
                {

                    int length = endpos - startpos - 1;
                    return s.Substring(startpos + 1, length);
                }

            }
            return null;
        }

        /// <summary>
        /// Returns int, parsed from the specified string
        /// </summary>
        /// <param name="s">String, containing unsigned int</param>
        /// <returns>Parsed value on success, -1 otherwise</returns>
        public static int ParseInt(this string s)
        {
            return int.TryParse(s, out int res) ? res : -1;
        }



        // Bruteforce (https://stackoverflow.com/a/55150810)
        public static int NaiveIndex(this string haystack, string needle) // start <= end
        {
            for (var i = haystack.Length - 1; i >= needle.Length - 1; i--)
            {
                var found = true;

                for (var j = needle.Length - 1; j >= 0 && found; j--)
                {

                    found = haystack[i - (needle.Length - 1 - j)] == needle[j];
                }
                if (found)
                    return i - (needle.Length - 1);
            }
            return -1;
        }


        public static int SubstepIndex(this string stack, string find) // start => end
        {
            for (int i = 0; i < stack.Length; i += find.Length) // o(n)
            {
                for (int fold = 0; fold < find.Length; fold++) // o(n)
                {
                    int fidx = i - fold;

                    if (find[fold] == stack[i] && fidx >= 0 && fidx + find.Length <= stack.Length)
                    {

                        if (stack[fidx] == find[0])
                        {
                            int ifind = 0;
                            for (int istack = fidx; istack < fidx + find.Length; istack++) // o(n)
                            {

                                if (stack[istack] == find[ifind])
                                {
                                    if (ifind == find.Length - 1)
                                        return fidx;
                                }
                                else
                                {
                                    break;
                                }
                                ifind++;
                            }
                        }
                    }
                }
            }

            return -1;
        }

        public static int SubstepIndex(this ReadOnlySpan<char> stack, char find, int startIndex = 0) // start => end
        {
            return SubstepIndex(stack, new ReadOnlySpan<char>(find), startIndex);
        }

        public static int SubstepIndex(this string stack, char find, int startIndex = 0) // start => end
        {
            return SubstepIndex(stack, new ReadOnlySpan<char>(find), startIndex);
        }

        public static int SubstepIndex(this string stack, string find, int startIndex = 0) // start => end
        {
            return SubstepIndex<char>(stack, find, startIndex);
        }

        public static int SubstepIndex(this byte[] stack, byte[] find, int startIndex = 0) // start => end
        {
            return SubstepIndex<byte>(stack, find, startIndex);
        }

        public static int SubstepIndex<T>(this ReadOnlySpan<T> stack, ReadOnlySpan<T> find, int startIndex = 0) where T : IEquatable<T>
        {
            stack = stack.Slice(startIndex);

            for (int i = 0; i < stack.Length; i += find.Length)
            {
                for (int fold = 0; fold < find.Length; fold++)
                {
                    int fidx = i - fold;

                    if (find[fold].Equals(stack[i]) && fidx >= 0 && fidx + find.Length <= stack.Length)
                    {

                        if (stack[fidx].Equals(find[0]))
                        {
                            int ifind = 0;
                            for (int istack = fidx; istack < fidx + find.Length; istack++)
                            {

                                if (stack[istack].Equals(find[ifind]))
                                {
                                    if (ifind == find.Length - 1)
                                        return fidx + startIndex;
                                }
                                else
                                {
                                    break;
                                }
                                ifind++;
                            }
                        }
                    }
                }
            }

            return -1;
        }


        public static string URLEncode(this string s) => Uri.EscapeDataString(s);
        public static string URLDecode(this string s) => Uri.UnescapeDataString(s);

        // https://www.programmingalgorithms.com/algorithm/boyer%E2%80%93moore-string-search-algorithm/
        public static int BMHIndex(this string str, string pat)
        {
            int m = pat.Length;
            int n = str.Length;

            int[] badChar = new int[256];

            int i;

            for (i = 0; i < 256; i++)
                badChar[i] = -1;

            for (i = 0; i < m; i++)
                badChar[pat[i]] = i;

            int s = 0;
            while (s <= n - m)
            {
                int j = m - 1;

                while (j >= 0 && pat[j] == str[s + j])
                    --j;

                if (j < 0)
                {
                    return s;
                    s += s + m < n ? m - badChar[str[s + m]] : 1;
                }
                else
                {
                    s += Math.Max(1, j - badChar[str[s + j]]);
                }
            }

            return -1;
        }

        public static int BMHIndex(this byte[] str, byte[] pat)
        {
            int m = pat.Length;
            int n = str.Length;

            int[] badChar = new int[256];

            int i;

            for (i = 0; i < 256; i++)
                badChar[i] = -1;

            for (i = 0; i < m; i++)
                badChar[pat[i]] = i;

            int s = 0;
            while (s <= n - m)
            {
                int j = m - 1;

                while (j >= 0 && pat[j] == str[s + j])
                    --j;

                if (j < 0)
                {
                    return s;
                    s += s + m < n ? m - badChar[str[s + m]] : 1;
                }
                else
                {
                    s += Math.Max(1, j - badChar[str[s + j]]);
                }
            }

            return -1;
        }

        public static bool MatchBeginning(this string src, string with)
        {
            if (src.Length < with.Length)
                return false;

            for (int i = 0; i < with.Length; i++)
            {
                if (src[i] != with[i])
                    return false;
            }
            return true;
        }

        public static string Regplace(this string s, string pattern, string with) => Regex.Replace(s, pattern, with);
        public static Match Match(this string s, string pattern) => Regex.Match(s, pattern);
        public static MatchCollection Matches(this string s, string pattern) => Regex.Matches(s, pattern);

        public static void AddMany<T>(this ICollection<T> collection, IEnumerable<T> values)
        {
            foreach (var item in values)
            {
                collection.Add(item);
            }
        }

        public static void AddMany<T>(this ICollection<T> collection, params T[] values)
        {
            foreach (var item in values)
            {
                collection.Add(item);
            }
        }

        public static T[] Subset<T>(this ICollection<T> collection, int from, int length)
        {
            T[] buf = new T[length];
            for (int i = from, k = 0; i < from + length; i++)
            {
                buf[k++] = collection.ElementAt(i);
            }
            return buf;
        }

        public static string Print<T>(this ICollection<T> collection, Func<T, string> cast = null)
        {
            string buf = "";

            foreach (var el in collection)
            {
                if (cast != null)
                    buf += cast(el);
                else
                    buf += el;
            }

            return buf;
        }

        public static string Print<T>(this ICollection<T> collection, string div)
        {
            if (collection == null || collection.Count == 0)
                return string.Empty;

            string buf = collection.First().ToString();

            for (int i = 1; i < collection.Count; i++)
            {
                var el = collection.ElementAt(i);
                buf += div + el;
            }

            return buf;
        }

        public static int IndexOf<T>(this IEnumerable<T> enumerable, T find, int startIndex = 0)
        {
            var len = enumerable.Count();
            for (; startIndex < len; startIndex++)
            {
                if (enumerable.ElementAt(startIndex).Equals(find))
                    return startIndex;
            }
            return -1;
        }

        public static int Count<T>(this IEnumerable<T> enumerable, T find, int startIndex = 0)
        {
            var len = enumerable.Count();
            var count = 0;

            for (; startIndex < len; startIndex++)
            {
                if (enumerable.ElementAt(startIndex).Equals(find))
                    count++;
            }
            return count;
        }

        public static byte[] Rev(this byte[] src)
        {
            Array.Reverse(src);
            return src;
        }

        public static ImmutableArray<T>[] Split<T>(this ReadOnlySpan<T> span, T separator)
        {
            var arr = span.ToImmutableArray();

            var divisions = arr.Count(separator);

            var idx = 0;

            var from = 0;

            var result = new ImmutableArray<T>[divisions + 1];

            for (int i = 0; i < span.Length; i++)
            {

                if (span[i].Equals(separator))
                {
                    result[idx++] = span.Slice(from, i - from).ToImmutableArray();

                    from = i + 1;
                }
            }

            result[idx] = span.Slice(from, span.Length - from).ToImmutableArray();

            return result;
        }
        #endregion

        #region Additional

        static int xseed = (int)Pow(Sin(DateTime.Now.Millisecond) * 512, 3);

        static Random rng = new Random(xseed);
        public static int Rand(int min, int max) => rng.Next(min, max);
        public static byte RandByte(byte min, byte max) => (byte)rng.Next(min, max);
        public static float RandFloat(int min, int max) => (float)rng.NextDouble();

        #endregion

    }

    public struct BitField
    {
        public byte[] bytes;

        public uint Length { get => (uint)bytes.Length; set => bytes = new byte[value >= 0 ? value : 0]; }
        public uint Size { get => (uint)bytes.Length * 8; set => bytes = new byte[value / 8 >= 0 ? value / 8 : 0]; }


        public bool this[int index]
        {
            get
            {
                return get(index);
            }

            set
            {
                set(value, index);
            }
        }

        public void set(bool value, int at)
        {
            var bi = 7 - at % 8; // moved from dynbitfield, http2 affect unknown
            //var bi = at % 8;
            at /= 8;

            byte mask = (byte)(1 << bi);

            if (value)
                bytes[at] |= mask;
            else
                bytes[at] &= (byte)~mask;
        }

        public bool get(int at)
        {
            var bi = 7 - at % 8;
            at /= 8;

            byte mask = (byte)(1 << bi);
            return (bytes[at] & mask) == mask;
        }

        /// <summary>
        /// Write an unsigned short into specified bits range
        /// </summary>
        /// <param name="val">Value to write</param>
        /// <param name="dstfrom">First bit index to begin writing from</param>
        /// <param name="dstto">Last bit index to end writing to</param>
        /// <param name="srcfrom">Starting index of the val bits region</param>
        public void WriteUInt16(ushort val, int dstfrom, int dstto, int srcfrom)
        {
            int vsh = 0;
            int at = 0;
            int bi = 0;
            int valmask = srcfrom;
            bool vbit;

            for (int i = dstfrom; i <= dstto; i++)
            {
                at = i;
                bi = 7 - at % 8;
                at /= 8;

                byte mask = (byte)(1 << bi);

                vbit = (val & 1 << valmask) != 0;

                if (vbit)
                    bytes[at] |= mask;
                else
                    bytes[at] &= (byte)~mask;

                valmask--;
                //byte r = (byte)(bytes[at] & mask);
                //if (r == mask)
                //val |= 1;
            }
        }

        public ushort ReadUInt16(int from, int to)
        {
            ushort val = 0;
            int vsh = 0;
            int at = 0;
            for (int i = from; i <= to; i++)
            {
                val <<= 1;
                at = i;
                int bi = 7 - at % 8;
                at /= 8;

                byte mask = (byte)(1 << bi);
                byte r = (byte)(bytes[at] & mask);
                if (r == mask)
                    val |= 1;
            }

            return val;
        }

        public byte[] ReversedArray()
        {
            byte[] rev = new byte[Length];

            for (uint i = Length; i > 0; i--)
            {
                rev[Length - i] = bytes[i - 1];
            }

            return rev;
        }

        public override string ToString()
        {
            string s = "";

            //for (int i = (int)Size - 1; i >= 0; i--)
            //{
            //}
            for (int i = 0; i < Size; i++)
            {
                s += get(i) ? "1" : "0";

                if ((i + 1) % 8 == 0)
                    s += " ";
                if ((i + 1) % 64 == 0)
                    s += "\r\n";
            }
            return s;
        }

    }


    public struct DynBitField
    {
        public List<byte> bytes;

        public uint Length { get => (uint)bytes.Count; /*set => bytes = new byte[value >= 0 ? value : 0];*/ }
        public uint Size { get => Length * 8; /*set => bytes = new byte[value / 8 >= 0 ? value / 8 : 0];*/ }


        public bool this[int index]
        {
            get
            {
                return get(index);
            }

            set
            {
                set(value, index);
            }
        }

        public void set(bool value, int at)
        {
            var bi = 7 - at % 8;
            at /= 8;

            if (at >= Length) // misaligned
            {
                bytes.Add(0);
            }

            byte mask = (byte)(1 << bi);

            if (value)
                bytes[at] |= mask;
            else
                bytes[at] &= (byte)~mask;
        }

        public bool get(int at)
        {
            var bi = 7 - at % 8;
            at /= 8;

            byte mask = (byte)(1 << bi);
            return (bytes[at] & mask) == mask;
        }



        public byte[] ReversedArray()
        {
            byte[] rev = new byte[Length];

            for (var i = (int)Length; i > 0; i--)
            {
                rev[Length - i] = bytes.ElementAt(i - 1);
            }

            return rev;
        }

        public override string ToString()
        {
            string s = "";

            //for (int i = (int)Size - 1; i >= 0; i--)
            //{
            //}
            for (int i = 0; i < Size; i++)
            {
                s += get(i) ? "1" : "0";

                if ((i + 1) % 8 == 0)
                    s += " ";
                if ((i + 1) % 64 == 0)
                    s += "\r\n";
            }
            return s;
        }
    }

}
