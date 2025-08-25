namespace Yang.Network
{
    public struct FriendInfo
    {
        public ulong userID;
        public bool online;

        public FriendInfo(ulong userID, bool online)
        {
            this.userID = userID;
            this.online = online;
        }
    }
}