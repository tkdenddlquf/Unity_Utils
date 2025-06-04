using Unity.Networking.Transport;

namespace Yang.Network.Relay
{
    public struct NetworkReadInfo
    {
        public NetworkConnection? conn;
        public byte type;
        public string msg;

        public NetworkReadInfo(byte type, string msg)
        {
            conn = null;
            this.type = type;
            this.msg = msg;
        }

        public NetworkReadInfo(NetworkConnection? conn, byte type, string msg)
        {
            this.conn = conn;
            this.type = type;
            this.msg = msg;
        }
    }
}
