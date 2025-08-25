using Steamworks;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Yang.Network.Steam
{
    public class SteamNetwork : NetworkBase
    {
        private SteamUser steamUser;
        private SteamCloud steamCloud;
        private SteamLobby steamLobby;

        public override ulong LobbyID => (ulong)steamLobby.LobbyID;

        public override bool IsJoined => LobbyID != (ulong)CSteamID.Nil;

        public override bool IsHost => GetHost() == UserID;

        public override bool Init()
        { 
#if !UNITY_EDITOR
            if (!SteamAPI.Init()) return false;
#endif
            if (!SteamManager.Initialized) return false;

            UserID = (ulong)Steamworks.SteamUser.GetSteamID();

            steamUser = new();
            steamCloud = new();
            steamLobby = new(UserID);

            return true;
        }

        public override void OnDestroy()
        {
            Leave();

            steamLobby.Dispose();
            steamUser.Dispose();
        }

        #region System
        private void SetRandomSeed() => random = new((int)(LobbyID ^ (LobbyID >> 32)));

        public override bool IsNoneID(ulong id) => id == GetNoneID();

        public override ulong GetNoneID() => (ulong)CSteamID.Nil;
        #endregion

        #region User
        public override IEnumerable<FriendInfo> GetFriends() => steamUser.GetFriends();

        public override IEnumerable<ulong> GetCoplayFriend() => steamUser.GetCoplayFriend();

        public override async Task<Sprite> GetAvatar(ulong userID) => await steamUser.GetAvatar(userID);

        public override string GetUserName(ulong userID) => steamUser.GetUserName(userID);

        public override void ActiveUserOverlay(ulong userID) => steamUser.ActiveFriendPopup(userID);
        #endregion

        #region Cloud
        public override async Task<T> JsonRead<T>(string fileName) => await steamCloud.JsonRead<T>(fileName);

        public override async Task<bool> JsonWrite<T>(string fileName, T data) => await steamCloud.JsonWrite(fileName, data);
        #endregion

        #region Lobby
        public override void TossMessage(byte type, string message) => steamLobby.TossMessage(type, message);

        public override void SetJoinable(bool joinable) => steamLobby.SetJoinable(joinable);

        public override async Task<bool> Create(LobbyInfo info)
        {
            bool success = await steamLobby.Create(info);

            if (success) SetRandomSeed();

            return success;
        }

        public override async Task<bool> Join(ulong lobbyID)
        {
            bool success = await steamLobby.Join(lobbyID);

            if (success) SetRandomSeed();

            return success;
        }

        public override void Leave() => steamLobby.Leave();

        #region Info
        public override async Task<int> GetLobbyList(List<ulong> lobbyIDs, int start, int count) => await steamLobby.GetLobbyList(lobbyIDs, start, count);

        public override string GetLobbyData(ulong lobbyID, string key) => steamLobby.GetLobbyData(lobbyID, key);

        public override void SetLobbyData(string key, string value) => steamLobby.SetLobbyData(key, value);

        public override void SetLobbyType(NetworkLobbyType type) => steamLobby.SetLobbyType(steamLobby.ConvertLobbyType(type));

        public override int GetLobbyMemberCount(ulong lobbyID) => steamLobby.GetLobbyMemberCount(lobbyID);

        public override int GetLobbyMemberLimit(ulong lobbyID) => steamLobby.GetLobbyMemberLimit(lobbyID);

        public override void SetLobbyMemberLimit(int maxMembers) => steamLobby.SetLobbyMemberLimit(maxMembers);
        #endregion

        #region Members
        public override IEnumerable<ulong> GetMembers() => steamLobby.GetMembers();

        public override ulong GetMember(int index) => steamLobby.GetMember(index);

        public override ulong GetHost() => steamLobby.GetHost();

        public override void SetHost(ulong userID) => steamLobby.SetHost(userID);

        public override void InviteUser(ulong userID) => steamLobby.InviteUser(userID);

        public override bool CheckMemberDatas(string key, string checkValue, bool containUser = true) => steamLobby.CheckMemberDatas(key, checkValue, containUser);

        public override string GetMemberData(ulong userID, string key) => steamLobby.GetMemberData(userID, key);

        public override void SetMemberData(string key, string value) => steamLobby.SetMemberData(key, value);

        public override void SetPlayedWith() => steamLobby.SetPlayedWith();
        #endregion
        #endregion
    }
}