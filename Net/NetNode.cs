using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.IO;
using System.Net.Security;
using System.Threading;
using System.Xml.Linq;

using Cyclonix.Utils;

namespace Cyclonix.Net
{
    public class NetNode : IDisposable
    {

        //public HttpRequestData LatestRequest;

        public string RequestID { get; internal set; }

        public object DataBind;

        internal int TotalIns;

        internal int TotalOuts;

        /// <summary> Indicates that the incoming request is not final (file being receieved, etc.) </summary>
        public bool Partial;

        private TcpClient tcpclient;

        private UdpClient udpclient;

        public bool IsConnected => (tcpclient?.Connected ?? udpclient?.Client?.Connected) ?? false;

        public EndPoint RemoteIP => (tcpclient?.Client ?? udpclient?.Client).RemoteEndPoint;

        public EndPoint LocalIP => (tcpclient?.Client ?? udpclient?.Client).LocalEndPoint;

        public int Port { get; private set; }

        /// <summary> String representaion of the IP Address of the remote <see cref="NetNode"/> </summary>
        public string IPString { get; private set; }

        /// <summary> Queue of the <see cref="Transmission"/>s to be send to the remote <see cref="NetNode"/> </summary>
        internal ConcurrentQueue<Transmission> sendQueue = new ConcurrentQueue<Transmission>();

        /// <summary> Big-endian representaion of the IP Address of the remote <see cref="NetNode"/> </summary>
        public int ShortIP { get; private set; }

        /// <summary> Raw transport stream </summary>
        public NetworkStream RawTransport { get; internal set; }

        /// <summary> A transport stream (either overlying or underlying) </summary>
        public Stream PrimaryTransport { get; internal set; }

        /// <summary> Timestamp of the last stream read call </summary>
        public DateTime LastRead { get; internal set; }

        public NetNode(UdpClient c, ManualResetEvent sendReset)
        {
            udpclient = c;

            var ipbytes = LocalIP.GetIPtoBytes();
            IPString = $"{ipbytes[0]}.{ipbytes[1]}.{ipbytes[2]}.{ipbytes[3]}";
            ShortIP = ipbytes.BytesToIntBigEndian();
            Port = udpclient.Client.LocalEndPoint.GetPortFromLoopback();
            manualReset = sendReset;
        }

        public NetNode(TcpClient c, ManualResetEvent sendReset)
        {
            tcpclient = c;

            var ipbytes = RemoteIP.GetIPtoBytes();
            IPString = $"{ipbytes[0]}.{ipbytes[1]}.{ipbytes[2]}.{ipbytes[3]}";
            ShortIP = ipbytes.BytesToIntBigEndian();
            Port = tcpclient.Client.LocalEndPoint.GetPortFromLoopback();
            manualReset = sendReset;
        }

        ManualResetEvent manualReset;

        public ConnectionState ConnectionState;

        public bool IsSecure =>
            ((PrimaryTransport as SslStream)?.IsEncrypted ?? false) ||
            ((PrimaryTransport as TLSStream)?.IsEncrypted ?? false);

        public bool OnHold { get; internal set; }

        public bool UpgradeToTLS(SslServerAuthenticationOptions ssloptions)
        {
            var ssl = new TLSStream(PrimaryTransport);

            ssl.AuthenticateAsServer(ssloptions);

            PrimaryTransport = ssl;

            return IsSecure;
        }

        public int ReadByte()
        {
            var read = PrimaryTransport.ReadByte();
            LastRead = DateTime.Now;
            return read;
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            var read = PrimaryTransport.Read(buffer, offset, count);
            LastRead = DateTime.Now;
            return read;
        }

        public int Read(Span<byte> buffer)
        {
            var read = PrimaryTransport.Read(buffer);
            LastRead = DateTime.Now;
            return read;
        }


        //public void PushSend(HttpMessageContainer hmc, bool close = true)
        //{
        //    if (hmc != null)
        //    {
        //        hmc.Headers.Add("x-rail", RequestID);

        //        if (hmc.FileName != null)
        //        {

        //            if (hmc.FileName.Length > 4 && hmc.FileName.PerCharCompare(htmExt, hmc.FileName.Length - 4))
        //                hmc.Headers.AppendConditionalPageHeaders(this);
        //        }
        //    }
        //    sendQueue.Enqueue(new Transmission { Data = hmc.Message.ASCIIEncode(), Close = close });
        //    manualReset.Set();
        //}

        /// <summary>
        /// Put the <see cref="string"/> header to the send queue along with <see cref="StreamNode"/> payload
        /// </summary>
        /// <param name="header">Header of the transmission</param>
        /// <param name="payload">Payload of the transmission</param>
        /// <param name="close">Indicates whether to close the connection upon the transmission is sent</param> 
        public void PushPayload(string header, StreamNode payload, bool close = true)
        {
            PushPayload(header.ASCIIEncode(), payload, close);
        }

        /// <summary>
        /// Put the <see cref="byte"/> header array to the send queue along with <see cref="StreamNode"/> payload
        /// </summary>
        /// <param name="header">Header of the transmission</param>
        /// <param name="payload">Payload of the transmission</param>
        /// <param name="close">Indicates whether to close the connection upon the transmission is sent</param> 
        public void PushPayload(byte[] header, StreamNode payload, bool close = true)
        {
            sendQueue.Enqueue(new Transmission(payload) { Data = header, Close = close });
            manualReset.Set();
        }

        /// <summary>
        /// Put the <see cref="string"/> object to the send queue
        /// </summary>
        /// <param name="data">Raw <see cref="string"/> array</param>
        /// <param name="close">Indicates whether to close the connection upon the data is sent</param> 
        public void PushStr(string data, bool close = true)
        {
            PushRaw(data.ASCIIEncode(), close);
        }

        /// <summary>
        /// Put the raw <see cref="byte"/> array to the send queue
        /// </summary>
        /// <param name="data">Raw <see cref="byte"/> array</param>
        /// <param name="close">Indicates whether to close the connection upon the data is sent</param> 
        public void PushRaw(byte[] data, bool close = true)
        {
            sendQueue.Enqueue(new Transmission { Data = data, Close = close });
            manualReset.Set();
        }

        public void Close() => tcpclient.Close();

        public void Dispose()
        {
            tcpclient.Dispose();
            manualReset.Dispose();
        }

        public event Action OnDisconnect;
        bool discPlayed;
        public void InvokeOnDisconnect()
        {
            if (!discPlayed)
            {
                discPlayed = true;
                OnDisconnect?.Invoke();
                Dispose();
            }
        }
    }
}
