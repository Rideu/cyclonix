using System.Net.Security;

namespace Cyclonix.Net
{
    public class RawContainer
    {

        public ArraySegment<byte> Data { get; internal set; }

        public NetNode Remote { get; internal set; }

        public ConnectionState ConState { get; internal set; }

        public SslApplicationProtocol[] NegotiatedProtocols { get; internal set; }

        public bool Secure { get; internal set; }
    }
}
