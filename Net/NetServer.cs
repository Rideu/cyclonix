global using System;
global using System.Net;
global using System.Net.Sockets;
global using System.Threading.Tasks;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Runtime.Serialization;
using System.Threading;

using Cyclonix.Utils;

namespace Cyclonix.Net
{

    public partial class NetServer
    {
        public string StoragePath { get; set; }

        string publicIP;
        public string PublicIP { get => publicIP; }

        private string localIP;
        public string LocalIP { get => localIP; }

        private string localSubnet;
        public string LocalSubnet { get => localSubnet; }

        public event Action OnStarted;

        public NetServer()
        {
        }

        class TcpPortListener
        {

            public TcpListener _tcplistener;
            public bool _secure;
            public Action<byte[]> _ondata;
            public Action _onconnection;
            public int _port;
            internal bool _listening;
        }

        class UdpPortListener
        {

            public IPEndPoint _remote = IPEndPoint.Parse("0.0.0.0");
            public UdpClient _udplistener;
            public bool _secure;
            public Action<byte[]> _ondata;
            public Action _onconnection;
            public int _port;
            internal bool _listening;
        }

        void startListen(TcpPortListener tcl)
        {
            tcl._listening = true;

            tcl._tcplistener.Server.NoDelay = true;
            tcl._tcplistener.Server.ReceiveBufferSize = 1024 * 8;
            tcl._tcplistener.Server.SendBufferSize = 1024 * 16;

            tcl._tcplistener.Start();
            LogWriter.LogMsgTag("[L]", $"Listen TCP: {tcl._tcplistener.LocalEndpoint}", ConsoleColor.Yellow);

            Task.Run(() =>
            {
                ListenOne(tcl);
            });

        }

        void stopListen(TcpPortListener tcl)
        {

            tcl._listening = false;
            tcl._tcplistener.Stop();
            LogWriter.LogMsgTag("[L]", $"Stop listen TCP: {tcl._tcplistener.LocalEndpoint}", ConsoleColor.Yellow);
        }

        void startListen(UdpPortListener ucl)
        {
            ucl._listening = true;

            //ucl._udplistener.Server.NoDelay = true;
            //ucl._udplistener.Server.ReceiveBufferSize = 1024 * 8;
            //ucl._udplistener.Server.SendBufferSize = 1024 * 16;

            LogWriter.LogMsgTag("[L]", $"Listen UDP: {ucl._port}", ConsoleColor.Yellow);

            Task.Run(() =>
            {
                ListenOne(ucl);
            });

        }

        void stopListen(UdpPortListener ucl)
        {

            ucl._listening = false;
            ucl._udplistener.Close();
            LogWriter.LogMsgTag("[L]", $"Stop listen TCP: {ucl._udplistener.Client.LocalEndPoint}", ConsoleColor.Yellow);
        }


        List<TcpPortListener> _tcpPortListeners = new();

        public void AddTcpListener(int port, Action<byte[]> ondata = null, bool secure = false)
        {
            if (!_tcpPortListeners.Any(n => n._port == port))
                _tcpPortListeners.Add(new()
                {
                    _port = port,
                    _tcplistener = TcpListener.Create(port),
                    _secure = secure,
                    _ondata = ondata,
                });

        }

        List<UdpPortListener> _udpPortListeners = new List<UdpPortListener>();

        public void AddUdpListener(int port, Action<byte[]> ondata = null, bool secure = false)
        {
            if (!_udpPortListeners.Any(n => n._port == port))
                _udpPortListeners.Add(new()
                {
                    _port = port,
                    _udplistener = new UdpClient(port),
                    _secure = secure,
                    _ondata = ondata,
                });

        }

        public void Start(string host = null)
        {
            SetupSSL(SslApplicationProtocol.Http11);

            if (ChillEnabled)
            {

                LogWriter.LogMsgTag("[L]", "Chill enabled.", ConsoleColor.Yellow);
                LoadChillRemotes();
            }

            var getIP = Task.Run(() =>
            {
                while (publicIP == null)
                {
                    try
                    {
                        Thread.Sleep(5000);
                        if (host != null) publicIP = Dns.GetHostAddresses(host).First().ToString();
                    }
                    catch (Exception)
                    {
                        LogWriter.LogMsgTag("[L]", "Public IP retrieval failed. Retrying...", ConsoleColor.Yellow);
                    }
                }
            });


            localIP = NetHelper.GetLocalIPAddress();
            localSubnet = localIP.Substring(0, localIP.IndexOf('.', localIP.IndexOf('.') + 1));

            var localIPaddr = IPAddress.Parse(localIP);

            timeoutTimerBreak = false;
            timeoutTimer = Task.Run(timeoutTick);

            LogWriter.LogMsgTag("[L]", "Running TCP listeners...", ConsoleColor.Yellow);

            foreach (var p in _tcpPortListeners)
            {
                startListen(p);
            }

            LogWriter.LogMsgTag("[L]", "Running UDP listeners...", ConsoleColor.Yellow);

            foreach (var p in _udpPortListeners)
            {
                startListen(p);
            }

            LogWriter.LogMsgTag("[L]", "Now listening", ConsoleColor.Yellow);

            getIP.Wait();

            OnStarted?.Invoke();
        }

        public void Stop()
        {

            chillFile?.Dispose();
            chillFile = null;

            timeoutTimerBreak = true;

            LogWriter.LogMsgTag("[L]", "Stopping TCP listeners...", ConsoleColor.Yellow);

            foreach (var p in _tcpPortListeners)
            {
                stopListen(p);
            }

            LogWriter.LogMsgTag("[L]", "Stopping UDP listeners...", ConsoleColor.Yellow);

            foreach (var p in _udpPortListeners)
            {
                stopListen(p);
            }
        }

        long bytesReceived;
        long bytesSent;


        Task ListenOne(UdpPortListener ucl)
        {

            if (!ucl._listening) return null;

            var t = Task.Run(() =>
            {
                try
                {
                    var accept = ucl._udplistener.Receive(ref ucl._remote);

                    processUdpClient(accept, ucl);
                }
                catch (Exception e)
                {

                    //throw;
                }
                ListenOne(ucl);
            });

            return t;
        }

        Task ListenOne(TcpPortListener tcl)
        {
            if (!tcl._listening) return null;

            var t = Task.Run(() =>
            {
                try
                {
                    var accept = tcl._tcplistener.AcceptTcpClient();

                    processTcpClient(accept, tcl);
                }
                catch (Exception e)
                {

                    //throw;
                }
                ListenOne(tcl);
            });

            return t;
        }

        void processUdpClient(byte[] rawdata, UdpPortListener upl)
        {

            var client = upl._udplistener;
            ManualResetEvent mre = new(false);

            NetNode udpnode = new(client, mre);

            List<Action<NetNode>> callbacklist = null;
            List<Action<RawContainer, ConnectionState>> msgcallbacklist = null;

            RawContainer data = new();
            data.Data = rawdata;
            data.Remote = udpnode;

            ConnectionState constate = new(upl._port);

            { // OnConnect

                if (_onconnectportmap.ContainsKey(upl._port))
                    callbacklist = _onconnectportmap[upl._port];

                if (callbacklist != null)
                    foreach (var cb in callbacklist)
                    {
                        cb.Invoke(udpnode);
                    }
            }



            { // OnMessage

                if (_onmessageportmap.ContainsKey(udpnode.Port))
                    msgcallbacklist = _onmessageportmap[udpnode.Port];

                if (msgcallbacklist != null)
                    foreach (var cb in msgcallbacklist)
                    {
                        cb.Invoke(data, constate);
                    }

                Transmission tm = default;

                while (udpnode.sendQueue.TryDequeue(out tm))
                {
                    client.Send(tm.Data);
                }
            }
        }

        void processTcpClient(TcpClient tc, TcpPortListener tpl)
        {
            Task.Run(() =>
            {

                ManualResetEvent
                    sendPending = new(false),
                    receivePending = new(false);

                NetNode tn = new NetNode(tc, sendPending);
                tn.RawTransport = tc.GetStream();
                tn.PrimaryTransport = tc.GetStream();

                tn.RequestID = $"{DateTime.Now.Ticks / 1000:X}";

                LogWriter.LogMsgTag($"[{tn.RequestID}:IN]", $"{tn.IPString}:{tn.Port}", ConsoleColor.Red, ConsoleColor.Gray);

                var subban = subnetBans.Any(n => tn.IPString.StartsWith(n));

                if (subban)
                    LogWriter.LogMsgTag($"[{tn.RequestID}:IN]", $"{tn.IPString}:{tn.Port} SB", ConsoleColor.Red, ConsoleColor.Gray);


                Chill blocked = default;

                if (ChillEnabled)
                {

                    blocked = ChilloutList.FirstOrDefault(n => n.rem == tn.ShortIP);


                    if (blocked.rem != 0)
                    {
                        if (blocked.ts < DateTime.Now)
                        {
                            ChilloutList.RemoveAll(n => n.rem == blocked.rem);
                            blocked.rem = 0;
                        }
                        else
                        {
                            LogWriter.LogMsgTag($"[{tn.RequestID}:B]", $"{tn.IPString} BT: {blocked.ts}", ConsoleColor.Red, ConsoleColor.Gray);
                        }
                    }
                }

                if (blocked.rem == 0 && !subban)
                {

                    if (tpl._secure)
                    {
                        IntermediateNetStream bs = new(tn.RawTransport);

                        //SslStream ssl = new SslStream(bs);
                        TLSStream ssl = new TLSStream(bs);

                        ssl.AuthenticateAsServer(serverAuthOptions);

                        tn.PrimaryTransport = ssl;
                    }


                    List<Action<NetNode>> callbacklist = null;

                    if (_onconnectportmap.ContainsKey(tn.Port))
                        callbacklist = _onconnectportmap[tn.Port];

                    if (callbacklist != null)
                        foreach (var cb in callbacklist)
                        {
                            cb.Invoke(tn);
                        }

                    ConnectionState constate = new(0);
                    tn.ConnectionState = constate;

                    receiverLoop(tn, receivePending, tpl);

                    senderLoop(tn, sendPending);
                }
                else
                {
                    LogWriter.LogMsgTag($"[{tn.RequestID}:IN]", $"DENIED", ConsoleColor.Red, ConsoleColor.Gray);
                    tn.Close();
                }
            });
        }

        Dictionary<int, List<Action<RawContainer, ConnectionState>>> _onmessageportmap = new();

        public void SubOnMessage(Action<RawContainer, ConnectionState> onmessage, int port, bool tcp = true, bool secure = false)
        {
            if (!_onmessageportmap.Any(n => n.Key == port))
                _onmessageportmap.Add(port, new());

            _onmessageportmap[port].Add(onmessage);

            if (tcp)
                AddTcpListener(port, secure: secure);
            else
                AddUdpListener(port, secure: secure);
        }

        public void SecurePort(int port, bool sec) => _tcpPortListeners.First(n => n._port == port)._secure = sec;

        Dictionary<int, List<Action<NetNode>>> _onconnectportmap = new();
        public void SubOnConnect(Action<NetNode> onconnect, int port, bool tcp = true)
        {
            if (!_onconnectportmap.Any(n => n.Key == port))
                _onconnectportmap.Add(port, new List<Action<NetNode>>());

            _onconnectportmap[port].Add(onconnect);

            if (tcp)
                AddTcpListener(port);
            else
                AddUdpListener(port);
        }

        public event Action OnDisconnect;

        void onCancel(object node)
        {
            NetNode tn = node as NetNode;

            LogWriter.LogMsgTag($"[{tn.RequestID}:IN]", $"Timeout", ConsoleColor.Red, ConsoleColor.Gray);
        }

        bool timeoutTimerBreak = false;
        Task timeoutTimer;

        void timeoutTick()
        {
            while (!timeoutTimerBreak)
            {
                var now = DateTime.Now;

                for (int i = 0; i < recvNetNodes.Count; i++)
                {
                    var rn = recvNetNodes[i];

                    if (rn != null)
                    {

                        if ((now - rn.LastRead).TotalSeconds > 60 && !rn.OnHold)
                        {
                            rn.Close();
                            onCancel(rn);
                            recvNetNodes.Remove(rn);
                        }
                    }
                }

                Thread.Sleep(1000);
            }
        }

        List<NetNode> recvNetNodes = new();

        void registerRecvNode(NetNode node)
        {
            recvNetNodes.Add(node);
        }

        void unregisterRecvNode(NetNode node)
        {


            recvNetNodes.Remove(node);
        }

        void receiverLoop(NetNode node, ManualResetEvent receivePending, TcpPortListener tpl)
        {
            Task.Run(() =>
            {

                bool loop = true;

                var constate = node.ConnectionState;

                List<Action<RawContainer, ConnectionState>> callbacklist = null;

                if (_onmessageportmap.ContainsKey(node.Port))
                    callbacklist = _onmessageportmap[node.Port];

                CancellationToken readCancel = new CancellationToken(false);
                readCancel.Register(onCancel, node);

                node.LastRead = DateTime.Now;

                registerRecvNode(node);

                while (loop && node.IsConnected)
                {
                    byte[] rbuffer = new byte[1024 * 8];
                    int dataRead = 0;
                    int totalRead = 0;


                    try
                    {

                        List<byte> dynBuffer = new List<byte> { };

                        while ((dataRead = node.PrimaryTransport.Read(rbuffer, 0, rbuffer.Length)) > 0)
                        {
                            node.LastRead = DateTime.Now;
                            node.TotalIns++;
                            bytesReceived += dataRead;
                            totalRead += dataRead;

                            var span = new Span<byte>(rbuffer, 0, dataRead);

                            RawContainer hc = new RawContainer();
                            hc.NegotiatedProtocols = (node.PrimaryTransport as TLSStream)?.NegotiatedProtocols;
                            hc.Secure = tpl._secure;
                            hc.Remote = node;
                            hc.Data = new ArraySegment<byte>(rbuffer, 0, dataRead);

                            constate.DataRead = totalRead;

                            if (callbacklist != null)
                                foreach (var cb in callbacklist)
                                {
                                    cb.Invoke(hc, constate);
                                }

                            loop = constate.KeepAlive;
                        }
                        if (constate.Protocol == WebProtocol.None)
                        {
                            break;
                        }
                        Thread.Sleep(10);
                    }
                    catch (IOException ex)
                    {
                        loop = false;
                        LogWriter.LogMsgTag($"[{node.RequestID}:RECV:IOEX]", $"{ex.GetExceptionRecur()}", ConsoleColor.Red, ConsoleColor.Gray);
                    }
                    catch (Exception ex)
                    {
                        loop = false;
                        LogWriter.LogMsgTag($"[{node.RequestID}:RECV:EX]", $"{ex.GetExceptionRecur()}", ConsoleColor.Red, ConsoleColor.Gray);
                    }
                    finally
                    {
                    }
                }

                unregisterRecvNode(node);
                node.InvokeOnDisconnect();

                node.PrimaryTransport.Dispose();
                receivePending.Dispose();
            });
        }

        void senderLoop(NetNode node, ManualResetEvent sendPending)
        {
            Task.Run(() =>
            {

                bool loop = true;

                while (node.IsConnected && loop)
                {
                    sendPending.Reset();

                    Transmission message = default;

                    try
                    {
                        while (node.sendQueue.TryDequeue(out message))
                        {

                            {


                                //if (message.Response != null && message.Compressed)
                                //    message.Compress();

                                byte[] header = message.Data;

                                if (header != null && header.Length > 0)
                                {
                                    LogWriter.LogMsgTag($"[{node.RequestID}:HEAD-{node.TotalOuts}]", $"{header.UTF8Decode()}", ConsoleColor.Yellow, ConsoleColor.Gray);
                                    node.TotalOuts++;
                                    node.PrimaryTransport.Write(header, 0, header.Length);
                                }


                                StreamNode stream = message.SendStream;

                                if (stream != null)
                                {
                                    byte[] streamBuffer = new byte[1024 * 16];
                                    int streamRead = 0;



                                    if (message.Ranges != null && message.Ranges.Any())
                                    {
                                        var range = message.Ranges.First();
                                        var end = range.End == 0 ? stream.Length : range.End;
                                        var start = range.Start;
                                        var rangeLength = end - start;

                                        //if(range.Start > 0)
                                        stream.Position = range.Start;
                                        LogWriter.LogMsgTag($"[{node.RequestID}:RNG]", $"Sending range:bytes {stream.Position}-{end}/{stream.Length} ({rangeLength})", ConsoleColor.Yellow, ConsoleColor.Gray);

                                        var sent = 0L;
                                        node.OnHold = true;
                                        while ((streamRead = stream.Read(streamBuffer, 0, streamBuffer.Length)) > 0)
                                        {
                                            sent += streamRead;
                                            bytesSent += streamRead;
                                            node.PrimaryTransport.Write(streamBuffer, 0, streamRead);
                                        }
                                        node.OnHold = false;
                                        stream.Close();
                                        LogWriter.LogMsgTag($"[{node.RequestID}:SCLS]", $"Payload stream closed", ConsoleColor.Yellow, ConsoleColor.Gray);

                                        LogWriter.LogMsgTag($"[{node.RequestID}:RNG]", $"Sent: {sent} of {rangeLength}/{stream.Length}", ConsoleColor.Yellow, ConsoleColor.Gray);

                                    }
                                    else
                                    {
                                        double millis = 0;
                                        var sent = 0L;
                                        if (stream.ByteBuffer != null)
                                        {

                                            node.OnHold = true;
                                            sent += stream.ByteBuffer.Length;
                                            bytesSent += stream.ByteBuffer.Length;
                                            node.PrimaryTransport.Write(stream.ByteBuffer, 0, stream.ByteBuffer.Length);
                                            node.OnHold = false;
                                        }
                                        else
                                        {
                                            node.OnHold = true;
                                            var dt = DateTime.Now;
                                            while ((streamRead = stream.Read(streamBuffer, 0, streamBuffer.Length)) > 0)
                                            {
                                                sent += streamRead;
                                                bytesSent += streamRead;
                                                node.PrimaryTransport.Write(streamBuffer, 0, streamRead);
                                            }
                                            millis = (DateTime.Now - dt).TotalMilliseconds;
                                            node.OnHold = false;
                                        }
                                        //stream.Close();
                                        //Log.LogMsgTag($"[{node.RequestID}:SCLS]", $"Payload stream closed", ConsoleColor.Yellow, ConsoleColor.Gray);
                                        LogWriter.LogMsgTag($"[{node.RequestID}:FUL]", $"Sent: {sent}/{stream.Length} in {millis:0.00} ms", ConsoleColor.Yellow, ConsoleColor.Gray);

                                    }

                                    LogWriter.LogMsgTag($"[{node.RequestID}:SCLS]", $"Payload stream closed", ConsoleColor.Yellow, ConsoleColor.Gray);
                                    stream.Close();
                                }
                            }

                            if (message.Close)
                                node.Close();
                        }
                    }
                    catch (IOException ex)
                    {
                        loop = false;
                        LogWriter.LogMsgTag($"[{node.RequestID}:SEND:IOEX]", $"{ex.GetExceptionRecur()}", ConsoleColor.Red, ConsoleColor.Gray);

                    }
                    catch (Exception ex)
                    {
                        loop = false;
                        LogWriter.LogMsgTag($"[{node.RequestID}:SEND:EX]", $"{ex.GetExceptionRecur()}", ConsoleColor.Red, ConsoleColor.Gray);
                    }
                    finally
                    {
                    }

                    //if(message.SendStream ?.)
                    //message.Response?.Dispose();
                    //message.Response = null;
                    message.SendStream?.Close();
                    message.SendStream?.Dispose();

                    if (node.IsConnected)
                        sendPending.WaitOne(60000);
                }

                node.InvokeOnDisconnect();
                node.PrimaryTransport.Close();
                node.PrimaryTransport.Dispose();
                sendPending.Dispose();
            });
        }

    }


}
