using System.Collections.Generic;
using System.IO;

namespace Cyclonix.Utils
{
    public class StreamNode : Stream
    {
        Stream underlying;

        public Stream MainStream { get => underlying; set => underlying = value; }

        public override bool CanRead => underlying.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        long fileLength = -1;

        public override long Length => underlying?.Length ?? ByteBuffer?.Length ?? (Path != null ? fileLength : -1);

        public override long Position { get => underlying.Position; set => underlying.Position = value; }

        bool listed = false;

        public string Path { get; private set; } = null;

        public StreamNode(byte[] data, string path = null)
        {
            SetBuffer(data);
            openStreams.Add(this);
            listed = true;
            Path = path;
        }

        public StreamNode(Stream stream)
        {
            underlying = stream;
            Path = (stream as FileStream)?.Name;
            openStreams.Add(this);
            listed = true;
        }


        public StreamNode(string filepath)
        {
            if (File.Exists(filepath))
            {
                Path = filepath;
                var fi = new FileInfo(Path);
                fileLength = fi.Length;
                underlying = null;
                openStreams.Add(this);
                listed = true;
            }
        }

        ~StreamNode()
        {
            if (listed)
                openStreams.Remove(this);
        }

        public byte[] ByteBuffer => byteBuffer;

        byte[] byteBuffer;

        public void SetBuffer(byte[] buffer)
        {
            byteBuffer = buffer;
        }

        public override void Flush()
        {
            throw new NotSupportedException("Attempt to flush to a read-only stream");
        }
         
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (!string.IsNullOrEmpty(Path) && byteBuffer == null && underlying == null)
            {
                underlying = File.OpenRead(Path);
            }
            return underlying?.Read(buffer, offset, count) ?? -1;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return underlying?.Seek(offset, origin) ?? 0;
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException("Attempt to change a read-only stream");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("Attempt to write to a read-only stream");
        }

        public override void Close()
        {

            underlying?.Close();
            byteBuffer = null;
            openStreams.Remove(this);
            listed = false;
        }

        internal void Delete()
        {
            if (Path != null)
                File.Delete(Path);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            underlying?.Dispose();
        }

        static List<StreamNode> openStreams = new List<StreamNode>();

        public static IEnumerable<StreamNode> FindFirstByFilePath(string path)
        {
            List<StreamNode> sns = null;
            foreach (var s in openStreams)
            { 
                if (s.MainStream is FileStream fs)
                {
                    if (fs.Name == path)
                    {
                        sns = sns ?? new List<StreamNode>();
                        sns.Add(s);
                    }
                }
            }
            return sns;
        }
    }
}
