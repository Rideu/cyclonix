using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
 
using TLSHandler.Enums;
using TLSHandler.Handler;
using TLSHandler.Internal.TLS.Records;

using static System.Math;

using Cyclonix.Utils;

namespace Cyclonix.Net
{
    public class TLSStream : Stream
    {
        Stream innerStream;

        public string TargetHostName { get; private set; }

        public Context TLSContext { get; internal set; }

        public bool IsEncrypted { get; private set; }

        public TLSStream(Stream innerStream)
        {
            this.innerStream = innerStream;
        }

        public override bool CanRead => innerStream.CanRead;

        public override bool CanSeek => throw new NotSupportedException();

        public override bool CanWrite => innerStream.CanWrite;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => innerStream.Position; set => innerStream.Position = value; }

        public override void Flush()
        {
            throw new InvalidOperationException("Cannot flush the TLSStream");
        }

        public override void Close()
        {
            innerStream.Close();
        }

        int bufferpos;
        byte[] readbuffer = new byte[1024 * 16];

        int stalllength = 0;

        public override int Read(byte[] buffer, int offset, int count)
        {

            lock (readbuffer)
            {

                if (stalllength > 0)
                {

                    var slen = Min(count, stalllength);
                    Buffer.BlockCopy(readbuffer, bufferpos, buffer, offset, slen);
                    bufferpos += slen;
                    stalllength -= slen;
                    return slen;
                }

                bufferpos = 0;
                var req = TLSRecord.Extract(innerStream);

                var dataread = 0;

                if (req != null) 
                {

                    if (req.Type == RecordType.ChangeCipherSpec)
                    {

                        var rec = new ChangeCipherSpec(req.Payload);
                        Process(rec);
                    }
                    else
                    if (req.Type == RecordType.ApplicationData)
                    {

                        var rec = new ApplicationData(req.Payload);

                        var tlsPayload = ReceiveApplicationData(rec);
                        if (tlsPayload != null)
                        {
                            try
                            {

                                //var parse = tlsPayload.Print(n => "" + (char)n);

                                var minread = Min(tlsPayload.Length, buffer.Length);
                                stalllength = 0;

                                if (tlsPayload.Length > buffer.Length)
                                {
                                    stalllength = tlsPayload.Length - buffer.Length;
                                    Buffer.BlockCopy(tlsPayload, minread, readbuffer, 0, stalllength);
                                }

                                Buffer.BlockCopy(tlsPayload, 0, buffer, dataread, minread);
                                dataread += minread;
                            }
                            catch (Exception e)
                            {

                                throw e;
                            }
                        }
                    }
                }



                return dataread;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new InvalidOperationException("Cannot seek through the TLSStream");
        }

        public override void SetLength(long value)
        {
            throw new InvalidOperationException("Cannot set length of the TLSStream");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (offset != 0)
            {
                throw new InvalidOperationException("Offset is not supported");
            }

            if (count > 1024 * 16)
            {

                int max = count;
                var clustersize = 1024 * 16;

                var buf = new byte[1024 * 16];

                var lastAmount = max % clustersize;

                int total = (int)Ceiling(max / (float)clustersize);
                bool tail = lastAmount > 0 && max > clustersize;

                for (int from = 0, i = 0; i < (total) - (tail ? 1 : 0); i++)
                {
                    
                    from = clustersize * i;
                    Buffer.BlockCopy(buffer, from, buf, 0, clustersize);
                    SendApplicationData(buf);
                }

                if (tail)
                {

                    var segment = new ArraySegment<byte>(buffer, clustersize * (total - 1), lastAmount);
                    SendApplicationData(segment.ToArray());
                }

            }
            else
            {
                SendApplicationData(buffer);
            }
        }

        public SslApplicationProtocol[] NegotiatedProtocols { private set; get; }

        public void AuthenticateAsServer(SslServerAuthenticationOptions serverAuthOptions)
        {

            var buffer = new byte[1024 * 16];

            var read = innerStream.Read(buffer, 0, buffer.Length);
            var readbytes = new Span<byte>(buffer, 0, read);
            var tlsreqs = TLSRecord.Extract(readbytes.ToArray());

            if (tlsreqs != null && tlsreqs.Length > 0)
            {

                var record = new Handshake(tlsreqs[0].Payload);

                if (TLSContext == null)
                {

                    var hshkreq = tlsreqs[0];
                    var handshake = new Handshake(hshkreq.Payload);
                    var SNI = TargetHostName = handshake.SNI;
                    NegotiatedProtocols = handshake.AvailableProtocols;

                    X509Certificate2 cert = null;

                    if (SNI == null)
                    {
                        if (serverAuthOptions.ServerCertificate == null)
                        {
                            throw new NullReferenceException("Server Name Indication was not specified by the remote endpoint, cannot provide default certificate in serverAuthOptions.ServerCertificate (null) ");
                        }
                    }

                    if (SNI != null)
                        cert = serverAuthOptions.ServerCertificateSelectionCallback?.Invoke(this, SNI) as X509Certificate2;

                    if (cert == null)
                        cert = serverAuthOptions.ServerCertificate as X509Certificate2;

                    if (cert == null)
                    {
                        throw new NullReferenceException("Certificate not found nor specified in SslServerAuthenticationOptions.ServerCertificate");
                    }

                    TLSContext = new Context(cert, cert, serverAuthOptions.ApplicationProtocols, false, false, true)
                    {
                        ClientCertificatesCallback = (chain) => OnClientCertificateVerify(cert, chain)
                    };

                    var response = TLSContext.Initialize(record);
                    Send(response);
                }
                else
                {
                    Process(record);
                }


                read = innerStream.Read(readbuffer, 0, readbuffer.Length);
                var segbytes = new ArraySegment<byte>(readbuffer, 0, read);
                tlsreqs = TLSRecord.Extract(segbytes);

                foreach (var req in tlsreqs)
                {
                    if (req.Type == RecordType.Handshake)
                    {
                        Process(req);
                    }
                    else
                    if (req.Type == RecordType.ChangeCipherSpec)
                    {

                        var rec = new ChangeCipherSpec(req.Payload);
                        Process(rec);
                    }
                    else
                    if (req.Type == RecordType.ApplicationData)
                    {

                        var rec = new ApplicationData(req.Payload);

                        var tlsPayload = ReceiveApplicationData(rec);
                    }
                }

                IsEncrypted = true;
            }
            else
            {
                throw new ProtocolViolationException("Failed to extract TLS records from TLSRecord.Extract");
            }
        }

        object _lock = new object();

        void Process(TLSRecord record)
        {
            lock (_lock)
            {

                var response = TLSContext.Process_Record(record);
                Send(response);
            }

        }

        void Send(Result resp)
        {
            if (resp != null)
            {
                if (resp is PacketResult hr)
                {
                    Send(hr.Response);
                }
                else if (resp is AlertResult ar)
                {

                    LogWriter.LogMsgTag($"[TLSStream]", $"ERR {ar.DebugMessage} {ar.Description}", ConsoleColor.DarkGray, ConsoleColor.DarkGray);
                    //LogHelper.Error(this, ar);
                    //if (ar.ShouldTerminate)
                    //    this.Close(CloseReason.ApplicationError);
                    return;
                }
            }
        }

        void Send(IEnumerable<TLSRecord> tls)
        {
            foreach (var pkt in tls)
            {
                //Log.LogMsgTag($"[TLSStream]", $"SEND TLS {pkt.Type} {pkt.Data.Length}", ConsoleColor.DarkGray, ConsoleColor.DarkGray);
                innerStream.Write(pkt.Data, 0, pkt.Data.Length);
            }
        }

        void SendApplicationData(byte[] data)
        {

            var resp = TLSContext.GetEncryptedPacket(data);
            if (resp != null)
            {
                if (resp is PacketResult hr)
                {
                    Send(hr.Response);
                }
                else
                if (resp is AlertResult ar)
                {
                    LogWriter.LogMsgTag($"[TLSStream]", $"ERR {ar.DebugMessage} {ar.Description}", ConsoleColor.DarkGray, ConsoleColor.DarkGray);
                    if (ar.ShouldTerminate)
                        this.Close();
                    return;
                }
            }
        }

        byte[] ReceiveApplicationData(ApplicationData tls)
        {

            var resp = TLSContext.Process_Record(tls);
            if (resp != null)
            {

                if (resp is PacketResult hr)
                {
                    Send(hr.Response);
                }
                else if (resp is AlertResult ar)
                {
                    LogWriter.LogMsgTag($"[TLSStream]", $"ERR {ar.DebugMessage} {ar.Description}", ConsoleColor.DarkGray, ConsoleColor.DarkGray);
                    if (ar.ShouldTerminate)
                        innerStream.Close();
                }
                else if (resp is ApplicationResult app)
                {
                    //Log.LogMsgTag($"[TLSStream]", $"RECV TLS {app.Data.Length}", ConsoleColor.DarkGray, ConsoleColor.DarkGray);
                    return app.Data;
                }
                return null;
            }
            else
            {

                if (TLSContext.State != TLSSessionState.Client_Finished)
                {

                    LogWriter.LogMsgTag($"[TLSStream]", $"ERR unknown error when decrypt ApplicationData", ConsoleColor.DarkGray, ConsoleColor.DarkGray);
                    innerStream.Close();
                }
                return null;
            }
        }

        bool OnClientCertificateVerify(X509Certificate2 pfxFilePath, X509Certificate2[] client_certs)
        {
            return true;
        }
    }
}
