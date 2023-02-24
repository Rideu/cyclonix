namespace Cyclonix.Net
{
    public class ConnectionState
    {
        public int Port { get; private set; }

        public int DataRead { get; set; } = 0;

        public WebProtocol Protocol { get; set; }

        public bool Finished = false;

        public bool KeepAlive { get; set; } = false;
          
        public object DataBind;

        public ConnectionState() { }

        public ConnectionState(int port) : this() => Port = port;
    }
}
