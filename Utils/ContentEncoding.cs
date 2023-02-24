using System.IO;
using System.IO.Compression;

namespace Cyclonix.Utils
{
    public static class ContentEncoding
    {

        public static byte[] GZIPCompress(byte[] raw)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                using (GZipStream cstr = new GZipStream(memory, CompressionLevel.Fastest))
                {
                    cstr.Write(raw, 0, raw.Length);
                }
                return memory.ToArray();
            }
        }
    }
}
