using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Yang.Network
{
    public abstract class NetworkBase
    {
        public ulong UserID { get; protected set; }

        public abstract ulong LobbyID { get; }

        public abstract bool IsJoined { get; }

        public abstract bool IsHost { get; }

        public bool IsFull => GetLobbyMemberCount(LobbyID) == GetLobbyMemberLimit(LobbyID);

        protected System.Random random = new();

        public abstract bool Init();

        public abstract void OnDestroy();

        #region System
        public int GetRandomValue(int minValue, int maxValue) => random.Next(minValue, maxValue);

        public abstract bool IsNoneID(ulong id);

        public abstract ulong GetNoneID();
        #endregion

        #region User
        public abstract IEnumerable<FriendInfo> GetFriends();

        public abstract IEnumerable<ulong> GetCoplayFriend();

        public abstract Task<Sprite> GetAvatar(ulong userID);

        public abstract string GetUserName(ulong userID);

        public abstract void ActiveUserOverlay(ulong userID);
        #endregion

        #region Cloud
        public abstract Task<T> JsonRead<T>(string fileName);

        public abstract Task<bool> JsonWrite<T>(string fileName, T data);
        #endregion

        #region Lobby
        public abstract void TossMessage(byte type, string message);

        public abstract void SetJoinable(bool joinable);

        public abstract Task<bool> Create(LobbyInfo info);

        public abstract Task<bool> Join(ulong lobbyID);

        public abstract void Leave();

        #region Info
        public abstract Task<int> GetLobbyList(List<ulong> lobbyIDs, int start, int count);

        public abstract string GetLobbyData(ulong lobbyID, string key);

        public abstract void SetLobbyData(string key, string value);

        public abstract void SetLobbyType(NetworkLobbyType type);

        public abstract int GetLobbyMemberCount(ulong lobbyID);

        public abstract int GetLobbyMemberLimit(ulong lobbyID);

        public abstract void SetLobbyMemberLimit(int maxMembers);
        #endregion

        #region Members
        public abstract IEnumerable<ulong> GetMembers();

        public abstract ulong GetMember(int index);

        public abstract ulong GetHost();

        public abstract void SetHost(ulong userID);

        public abstract void InviteUser(ulong userID);

        public abstract bool CheckMemberDatas(string key, string checkValue, bool containUser = true);

        public abstract string GetMemberData(ulong userID, string key);

        public abstract void SetMemberData(string key, string value);

        public abstract void SetPlayedWith();
        #endregion
        #endregion
    }
}