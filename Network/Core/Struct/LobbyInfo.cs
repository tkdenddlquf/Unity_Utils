using System.Collections.Generic;

namespace Yang.Network
{
    public struct LobbyInfo
    {
        public NetworkLobbyType type;

        public int maxMembers;

        public Dictionary<string, string> datas;

        public LobbyInfo(int maxMembers)
        {
            type = NetworkLobbyType.Public;
            datas = new();

            this.maxMembers = maxMembers;
        }
    }
}