namespace Yang.Network
{
    public struct MessageReadInfo
    {
        public ulong senderID;
        public byte type;
        public string msg;

        public MessageReadInfo(ulong senderID, byte type, string msg)
        {
            this.senderID = senderID;
            this.type = type;
            this.msg = msg;
        }
    }
}
