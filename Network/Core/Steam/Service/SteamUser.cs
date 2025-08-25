using Steamworks;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Yang.Network.Steam
{
    public class SteamUser
    {
        private readonly Callback<LobbyInvite_t> lobbyInvite;
        private readonly Callback<GameLobbyJoinRequested_t> gameLobbyJoinRequested;

        private readonly Callback<AvatarImageLoaded_t> avatarImageLoaded;

        private readonly ConcurrentDictionary<ulong, Sprite> avatarLists = new();

        public SteamUser()
        {
            lobbyInvite = Callback<LobbyInvite_t>.Create(OnLobbyInvite);
            gameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);

            avatarImageLoaded = Callback<AvatarImageLoaded_t>.Create(OnAvatarImageLoaded);
        }

        public void Dispose()
        {
            lobbyInvite.Dispose();
            gameLobbyJoinRequested.Dispose();

            avatarImageLoaded.Dispose();
        }

        #region Friends
        public IEnumerable<FriendInfo> GetFriends()
        {
            EFriendFlags flags = EFriendFlags.k_EFriendFlagImmediate;
            int count = SteamFriends.GetFriendCount(flags);

            for (int i = 0; i < count; i++)
            {
                CSteamID userID = SteamFriends.GetFriendByIndex(i, flags);
                bool online = false;

                if (SteamFriends.GetFriendGamePlayed(userID, out FriendGameInfo_t info))
                {
                    online = info.m_gameID.AppID() == SteamUtils.GetAppID();
                }

                yield return new((ulong)userID, online);
            }
        }

        public IEnumerable<ulong> GetCoplayFriend()
        {
            int count = SteamFriends.GetCoplayFriendCount();

            for (int i = 0; i < count; i++)
            {
                CSteamID userID = SteamFriends.GetCoplayFriend(i);
                AppId_t appId = SteamFriends.GetFriendCoplayGame(userID);

                if (appId != SteamUtils.GetAppID() || SteamFriends.HasFriend(userID, EFriendFlags.k_EFriendFlagAll)) continue;

                yield return (ulong)userID;
            }
        }

        public string GetUserName(ulong userID) => SteamFriends.GetFriendPersonaName((CSteamID)userID);

        public void ActiveFriendPopup(ulong userID) => SteamFriends.ActivateGameOverlayToUser("friendadd", (CSteamID)userID);
        #endregion

        #region User Data
        public async Task<Sprite> GetAvatar(ulong userID)
        {
            int imageID = SteamFriends.GetMediumFriendAvatar((CSteamID)userID);

            if (imageID == -1)
            {
                while (!avatarLists.ContainsKey(userID)) await Task.Delay(100);

                avatarLists.TryRemove(userID, out Sprite sprite);

                return sprite;
            }
            else return ConvertAvatarData(imageID);
        }

        private void OnAvatarImageLoaded(AvatarImageLoaded_t callback) => avatarLists.TryAdd((ulong)callback.m_steamID, ConvertAvatarData(callback.m_iImage));

        private Sprite ConvertAvatarData(int imageID)
        {
            if (imageID < 0) return null;

            if (SteamUtils.GetImageSize(imageID, out uint width, out uint height))
            {
                byte[] buffer = new byte[4 * height * width];

                if (SteamUtils.GetImageRGBA(imageID, buffer, buffer.Length))
                {
                    byte[] flippedBuffer = new byte[buffer.Length];
                    int rowSize = (int)(width * 4);

                    for (int y = 0; y < height; y++)
                    {
                        int srcIndex = y * rowSize;
                        int destIndex = (int)((height - 1 - y) * rowSize);

                        System.Buffer.BlockCopy(buffer, srcIndex, flippedBuffer, destIndex, rowSize);
                    }

                    Texture2D texture = new((int)width, (int)height, TextureFormat.RGBA32, false, true);

                    texture.LoadRawTextureData(flippedBuffer);
                    texture.Apply();

                    return Sprite.Create(texture, new Rect(0, 0, width, height), Vector2.one * 0.5f);
                }
            }

            return null;
        }
        #endregion

        #region Callbacks
        private void OnLobbyInvite(LobbyInvite_t callback)
        {
            CGameID gameID = (CGameID)callback.m_ulGameID;

            if (gameID.AppID() != SteamUtils.GetAppID()) return;

            NetworkReader.Invited(callback.m_ulSteamIDLobby, callback.m_ulSteamIDUser);
        }

        private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback) => NetworkReader.Invited((ulong)callback.m_steamIDLobby, (ulong)callback.m_steamIDFriend);
        #endregion
    }
}
