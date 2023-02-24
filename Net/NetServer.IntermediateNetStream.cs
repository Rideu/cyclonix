using System.IO;

namespace Cyclonix.Net
{

    public partial class NetServer
    {
        class IntermediateNetStream : Stream
        {


            NetworkStream UnderlyingStream;

            public IntermediateNetStream(NetworkStream ns)
            {
                UnderlyingStream = ns;
            }

            public override bool CanRead => UnderlyingStream.CanRead;

            public override bool CanSeek => UnderlyingStream.CanSeek;

            public override bool CanWrite => UnderlyingStream.CanWrite;

            public override long Length => UnderlyingStream.Length;

            public override long Position { get => UnderlyingStream.Position; set => UnderlyingStream.Position = value; }

            public override void Flush()
            {
                UnderlyingStream.Flush();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return UnderlyingStream.Read(buffer, offset, count);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return UnderlyingStream.Seek(offset, origin);
            }

            public override void SetLength(long value)
            {
                UnderlyingStream.SetLength(value);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                UnderlyingStream.Write(buffer, offset, count);
            }
        }

    }
}
