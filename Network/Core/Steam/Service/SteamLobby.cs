using Steamworks;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Yang.Network.Steam
{
    public class SteamLobby
    {
        private SteamNetworkingIdentity identity = new();

        private const int CHANNEL = 0;

        private readonly Callback<LobbyDataUpdate_t> lobbyDataUpdate;
        private readonly Callback<LobbyChatUpdate_t> lobbyChatUpdate;

        private readonly CallResult<LobbyEnter_t> lobbyEnter = new();
        private readonly CallResult<LobbyCreated_t> lobbyCreated = new();
        private readonly CallResult<LobbyMatchList_t> lobbyMatchList = new();

        private readonly Dictionary<string, string> lobbyDatas = new();

        public CSteamID LobbyID { get; private set; }

        private CSteamID userID;

        private int currentLobbyMemerLimit;

        public SteamLobby(ulong userID)
        {
            this.userID = (CSteamID)userID;

            lobbyDataUpdate = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);
            lobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
        }

        public void Dispose()
        {
            lobbyDataUpdate.Dispose();
            lobbyChatUpdate.Dispose();

            lobbyEnter.Dispose();
            lobbyCreated.Dispose();
            lobbyMatchList.Dispose();
        }

        #region Join
        public async Task<bool> Create(LobbyInfo info)
        {
            TaskCompletionSource<bool> tcs = new();

            void Complete(LobbyCreated_t result, bool failure)
            {
                if (failure || result.m_eResult != EResult.k_EResultOK) tcs.SetResult(false);
                else
                {
                    LobbyID = (CSteamID)result.m_ulSteamIDLobby;

                    foreach (var data in info.datas) SetLobbyData(data.Key, data.Value);

                    ReceiveUpdate();
                    NetworkPing();

                    UpdateLobbyData();

                    NetworkReader.Connect((ulong)userID);

                    tcs.SetResult(true);
                }
            }

            lobbyCreated.Set(SteamMatchmaking.CreateLobby(ConvertLobbyType(info.type), info.maxMembers), Complete);

            return await tcs.Task;
        }

        public async Task<bool> Join(ulong lobbyID)
        {
            if (GetLobbyMemberCount(lobbyID) == 0) return false;

            TaskCompletionSource<bool> tcs = new();

            void Complete(LobbyEnter_t result, bool failure)
            {
                EChatRoomEnterResponse response = (EChatRoomEnterResponse)result.m_EChatRoomEnterResponse;

                if (failure || result.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess) tcs.SetResult(false);
                else
                {
                    LobbyID = (CSteamID)result.m_ulSteamIDLobby;

                    ReceiveUpdate();
                    NetworkPing();

                    UpdateLobbyData();

                    NetworkReader.Connect((ulong)userID);

                    tcs.SetResult(true);
                }
            }

            lobbyEnter.Set(SteamMatchmaking.JoinLobby((CSteamID)lobbyID), Complete);

            return await tcs.Task;
        }

        public async Task<bool> Leave()
        {
            if (LobbyID == CSteamID.Nil) return false;

            SteamMatchmaking.LeaveLobby(LobbyID);

            LobbyID = CSteamID.Nil;

            lobbyDatas.Clear();

            await Task.Delay(1000);

            return true;
        }

        private bool UpdateLobbyData()
        {
            int dataLength = SteamMatchmaking.GetLobbyDataCount(LobbyID);

            bool isChanged = false;

            for (int i = 0; i < dataLength; i++)
            {
                if (SteamMatchmaking.GetLobbyDataByIndex(LobbyID, i, out string key, 256, out string value, 256))
                {
                    if (lobbyDatas.TryGetValue(key, out string oldValue))
                    {
                        if (oldValue != value)
                        {
                            if (value == "") lobbyDatas.Remove(key);
                            else lobbyDatas[key] = value;

                            isChanged = true;
                        }
                    }
                    else
                    {
                        lobbyDatas.Add(key, value);
                        isChanged = true;
                    }
                }
            }

            int limit = GetLobbyMemberLimit((ulong)LobbyID);

            if (currentLobbyMemerLimit != limit)
            {
                currentLobbyMemerLimit = limit;

                isChanged = true;
            }

            return isChanged;
        }
        #endregion

        #region Message
        public void TossMessage(byte type, string msg)
        {
            byte[] messageBytes = System.Text.Encoding.UTF8.GetBytes(msg);
            byte[] finalMessage = new byte[1 + messageBytes.Length];

            finalMessage[0] = type;

            Buffer.BlockCopy(messageBytes, 0, finalMessage, 1, messageBytes.Length);

            GCHandle handle = GCHandle.Alloc(finalMessage, GCHandleType.Pinned);

            try
            {
                IntPtr dataPtr = handle.AddrOfPinnedObject();

                foreach (ulong memberID in GetMembers())
                {
                    if (userID == (CSteamID)memberID) continue;

                    identity.SetSteamID((CSteamID)memberID);

                    SteamNetworkingMessages.SendMessageToUser(ref identity, dataPtr, (uint)finalMessage.Length, Constants.k_nSteamNetworkingSend_Reliable, CHANNEL);
                }
            }
            finally
            {
                handle.Free();
            }
        }

        private async void ReceiveUpdate()
        {
            int length;
            IntPtr[] receiveBuffers = new IntPtr[16];

            while (LobbyID != CSteamID.Nil)
            {
                length = SteamNetworkingMessages.ReceiveMessagesOnChannel(CHANNEL, receiveBuffers, receiveBuffers.Length);

                for (int i = 0; i < length; i++)
                {
                    SteamNetworkingMessage_t netMessage = SteamNetworkingMessage_t.FromIntPtr(receiveBuffers[i]);

                    int size = netMessage.m_cbSize;

                    if (size <= 0 || netMessage.m_pData == IntPtr.Zero) continue;

                    byte[] data = new byte[size];

                    Marshal.Copy(netMessage.m_pData, data, 0, size);

                    string text = System.Text.Encoding.UTF8.GetString(data, 1, size - 1);

                    NetworkReader.ReadMessage(new((ulong)netMessage.m_identityPeer.GetSteamID(), data[0], text));

                    SteamNetworkingMessage_t.Release(receiveBuffers[i]);
                }

                await Task.Delay(30);
            }
        }

        private async void NetworkPing()
        {
            while (LobbyID != CSteamID.Nil)
            {
                TossMessage(byte.MaxValue, "P");

                await Task.Delay(5000);
            }
        }
        #endregion

        #region System
        public ELobbyType ConvertLobbyType(NetworkLobbyType type)
        {
            switch (type)
            {
                case NetworkLobbyType.Public:
                    return ELobbyType.k_ELobbyTypePublic;

                case NetworkLobbyType.Private:
                    return ELobbyType.k_ELobbyTypeInvisible;

                default:
                    return ELobbyType.k_ELobbyTypePrivate;
            }
        }

        public void SetJoinable(bool joinable) => SteamMatchmaking.SetLobbyJoinable(LobbyID, joinable);

        public void InviteUser(ulong userID) => SteamMatchmaking.InviteUserToLobby(LobbyID, (CSteamID)userID);

        public void SetPlayedWith()
        {
            foreach (ulong memberID in GetMembers())
            {
                if (memberID == (ulong)userID) continue;

                SteamFriends.SetPlayedWith((CSteamID)memberID);
            }
        }

        public async Task<int> GetLobbyList(List<ulong> lobbyIDs, int start, int count, params LobbyFilter[] filters)
        {
            TaskCompletionSource<int> tcs = new();

            void Complete(LobbyMatchList_t result, bool failure) => tcs.SetResult((int)result.m_nLobbiesMatching);

            List<LobbyFilter> containFilters = new();

            if (filters.Length != 0)
            {
                foreach (LobbyFilter filter in filters)
                {
                    ELobbyComparison comparison;

                    switch (filter.filterType)
                    {
                        case LobbyFilterType.EqualToOrLessThan:
                            comparison = ELobbyComparison.k_ELobbyComparisonEqualToOrLessThan;
                            break;

                        case LobbyFilterType.LessThan:
                            comparison = ELobbyComparison.k_ELobbyComparisonLessThan;
                            break;

                        case LobbyFilterType.Equal:
                            comparison = ELobbyComparison.k_ELobbyComparisonEqual;
                            break;

                        case LobbyFilterType.GreaterThan:
                            comparison = ELobbyComparison.k_ELobbyComparisonGreaterThan;
                            break;

                        case LobbyFilterType.EqualToOrGreaterThan:
                            comparison = ELobbyComparison.k_ELobbyComparisonEqualToOrGreaterThan;
                            break;

                        case LobbyFilterType.NotEqual:
                            comparison = ELobbyComparison.k_ELobbyComparisonNotEqual;
                            break;

                        default:
                            containFilters.Add(filter);
                            continue;
                    }

                    switch (filter.valueType)
                    {
                        case LobbyValueType.String:
                            SteamMatchmaking.AddRequestLobbyListStringFilter(filter.key, filter.value, comparison);
                            break;

                        case LobbyValueType.Int:
                            SteamMatchmaking.AddRequestLobbyListNumericalFilter(filter.key, int.Parse(filter.value), comparison);
                            break;
                    }
                }
            }

            lobbyMatchList.Set(SteamMatchmaking.RequestLobbyList(), Complete);

            int lobbyCount = await tcs.Task;

            lobbyIDs.Clear();

            for (int i = start; i < start + count; i++)
            {
                if (lobbyCount < i) lobbyIDs.Add((ulong)CSteamID.Nil);
                else
                {
                    ulong lobbyID = (ulong)SteamMatchmaking.GetLobbyByIndex(i);

                    bool addLobbyID = true;

                    foreach (LobbyFilter filter in containFilters)
                    {
                        string value = GetLobbyData(lobbyID, filter.key);

                        if (!value.Contains(filter.value))
                        {
                            addLobbyID = false;

                            break;
                        }
                    }

                    if (addLobbyID) lobbyIDs.Add(lobbyID);
                    else count++;
                }
            }

            return lobbyCount;
        }
        #endregion

        #region Members
        public IEnumerable<ulong> GetMembers()
        {
            int length = SteamMatchmaking.GetNumLobbyMembers(LobbyID);

            for (int i = 0; i < length; i++) yield return GetMember(i);
        }

        public ulong GetMember(int index) => (ulong)SteamMatchmaking.GetLobbyMemberByIndex(LobbyID, index);

        public ulong GetHost() => (ulong)SteamMatchmaking.GetLobbyOwner(LobbyID);

        public void SetHost(ulong userID) => SteamMatchmaking.SetLobbyOwner(LobbyID, (CSteamID)userID);

        public int GetLobbyMemberCount(ulong lobbyID) => SteamMatchmaking.GetNumLobbyMembers((CSteamID)lobbyID);

        public int GetLobbyMemberLimit(ulong lobbyID) => SteamMatchmaking.GetLobbyMemberLimit((CSteamID)lobbyID);

        public void SetLobbyMemberLimit(int maxMembers)
        {
            if (maxMembers < GetLobbyMemberCount((ulong)LobbyID)) return;

            SteamMatchmaking.SetLobbyMemberLimit(LobbyID, maxMembers);
        }
        #endregion

        #region Datas
        public string GetLobbyData(ulong lobbyID, string key) => SteamMatchmaking.GetLobbyData((CSteamID)lobbyID, key);

        public void SetLobbyData(string key, string value) => SteamMatchmaking.SetLobbyData(LobbyID, key, value);

        public void SetLobbyType(ELobbyType type) => SteamMatchmaking.SetLobbyType(LobbyID, type);

        public bool CheckMemberDatas(string key, string checkValue, bool containUser)
        {
            bool compare = true;

            foreach (ulong cSteamID in GetMembers())
            {
                if (!containUser && cSteamID == (ulong)userID) continue;

                string value = GetMemberData(cSteamID, key);

                if (value != checkValue)
                {
                    compare = false;

                    break;
                }
            }

            return compare;
        }

        public string GetMemberData(ulong userID, string key) => SteamMatchmaking.GetLobbyMemberData(LobbyID, (CSteamID)userID, key);

        public void SetMemberData(string key, string value) => SteamMatchmaking.SetLobbyMemberData(LobbyID, key, value);
        #endregion

        #region Callback
        private void OnLobbyDataUpdate(LobbyDataUpdate_t callback)
        {
            if (callback.m_bSuccess == 0 || LobbyID == CSteamID.Nil) return;

            if (callback.m_ulSteamIDLobby == callback.m_ulSteamIDMember) // 로비 데이터 변경
            {
                if (UpdateLobbyData()) NetworkReader.ChangeLobbyData();
            }
            else // 유저 데이터 변경
            {
                NetworkReader.ChangeUserData(callback.m_ulSteamIDMember);
            }
        }

        private void OnLobbyChatUpdate(LobbyChatUpdate_t callback)
        {
            EChatMemberStateChange state = (EChatMemberStateChange)callback.m_rgfChatMemberStateChange;

            ulong userID = callback.m_ulSteamIDUserChanged;

            switch (state)
            {
                case EChatMemberStateChange.k_EChatMemberStateChangeEntered:
                    NetworkReader.Connect(userID);
                    break;

                default:
                    NetworkReader.Disconnect(userID);
                    break;
            }
        }
        #endregion
    }
}
