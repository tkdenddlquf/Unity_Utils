using System;

namespace Yang.Network
{
    public static class NetworkReader
    {
        public static event Action<ulong> OnConnect;
        public static event Action<ulong> OnDisonnect;
        public static event Action<ulong> OnChangeUserData;
        public static event Action OnChangeLobbyData;

        public static event Action<ulong, ulong> OnInvite;

        public static event Action<MessageReadInfo> OnMessage;

        /// <summary>
        /// 유저가 연결된 경우 호출
        /// </summary>
        /// <param name="userID">연결된 유저 ID</param>
        public static void Connect(ulong userID) => OnConnect?.Invoke(userID);

        /// <summary>
        /// 유저의 연결이 해제된 경우 호출
        /// </summary>
        /// <param name="userID">연결이 해제된 유저 ID</param>
        public static void Disconnect(ulong userID) => OnDisonnect?.Invoke(userID);

        /// <summary>
        /// 유저의 정보가 변경된 경우 호출
        /// </summary>
        /// <param name="userID">정보가 변경된 유저 ID</param>
        public static void ChangeUserData(ulong userID) => OnChangeUserData?.Invoke(userID);

        /// <summary>
        /// 방의 정보가 변경된 경우 호출
        /// </summary>
        public static void ChangeLobbyData() => OnChangeLobbyData?.Invoke();

        /// <summary>
        /// 자신이 초대된 경우 호출
        /// </summary>
        /// <param name="lobbyID">초대된 로비 ID</param>
        /// <param name="userID">호스트 ID</param>
        public static void Invited(ulong lobbyID, ulong userID) => OnInvite?.Invoke(lobbyID, userID);

        /// <summary>
        /// 메시지를 수신한 경우 호출
        /// </summary>
        /// <param name="info">수신한 메시지 정보</param>
        public static void ReadMessage(MessageReadInfo info) => OnMessage?.Invoke(info);
    }
}
