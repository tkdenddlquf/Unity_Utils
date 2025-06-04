using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace Yang.Network.Relay
{
    public class NetworkManager : NetworkSingleton<NetworkManager>
    {
        [SerializeField] private NetworkReaderBase reader;

        #region Host
        private HostRelay host;

        /// <summary>
        /// GUID 목록이 변경된 경우 콜백
        /// </summary>
        public event Action<List<string>> ChangeConnectGUIDs;

        public bool IsHost => host.IsCreated;

        public int ConnectCount => connectGUIDs.Count;

        private List<string> connectGUIDs = new();
        #endregion

        #region Client
        private ClientRelay client;

        /// <summary>
        /// 연결 상태가 변경된 경우 콜백
        /// </summary>
        public event Action ChangeConnect;

        public bool IsJoined => client.IsConnect;
        #endregion

        public string MyGUID { get; private set; }

        public string JoinCode { get; private set; }

        public int MaxConnection { get; private set; }

        private async void Awake()
        {
            DontDestroyOnLoad(this);

            await UnityServices.InitializeAsync();
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

            MyGUID = Guid.NewGuid().ToString()[..6];

            connectGUIDs.Add(MyGUID);

            host = new(reader);
            client = new(reader);
        }

        /// <summary>
        /// 방 생성
        /// </summary>
        /// <param name="maxConnection">최대 인원 수 (호스트 포함)</param>
        public async Task<string> Create(int maxConnection)
        {
            MaxConnection = maxConnection;

            JoinCode = await host.CreateAllocation(maxConnection - 1);

            host.BindHost();

            return JoinCode;
        }

        /// <summary>
        /// 참여중인 방에서 퇴장
        /// </summary>
        public void Dispose()
        {
            if (IsHost) host.DisconnectAll();
            else if (IsJoined)
            {
                client.Disconnect();

                ChangeConnect?.Invoke();
            }
        }

        /// <summary>
        /// 방 참가
        /// </summary>
        /// <param name="code">참가하려는 방 코드</param>
        public async Task<bool> Join(string code)
        {
            if (string.IsNullOrEmpty(code)) return false;

            JoinCode = code;

            await client.JoinAllocation(code);

            if (!client.BindAndConnect()) return false;

            for (int i = 0; i < 20; i++)
            {
                if (client.IsConnect)
                {
                    ChangeConnect?.Invoke();

                    return true;
                }

                await Task.Delay(200);
            }

            Debug.LogError("Failed to Joined.");

            return false;
        }

        /// <summary>
        /// 모든 연결된 대상에 메시지 전달
        /// </summary>
        /// <param name="type">메시지 타입</param>
        /// <param name="message">내용</param>
        public void TossMessage(byte type, string message)
        {
            if (IsHost) host.TossMessage(new(type, message));
            else if (IsJoined) client.TossMessage(type, message);
        }

        public void AddGUID(string guid)
        {
            if (connectGUIDs.Contains(guid)) return;

            connectGUIDs.Add(guid);
            ChangeConnectGUIDs?.Invoke(connectGUIDs);
        }

        public void RemoveGUID(string guid)
        {
            if (!connectGUIDs.Contains(guid)) return;

            connectGUIDs.Remove(guid);
            ChangeConnectGUIDs?.Invoke(connectGUIDs);
        }

        public string GetConnectGUID(int index)
        {
            if (index >= connectGUIDs.Count) return string.Empty;

            return connectGUIDs[index];
        }

        /// <summary>
        /// 연결중인 모든 GUID 반환
        /// </summary>
        /// <returns>GUID 목록</returns>
        public string GetConnectGUIDs() => string.Join(",", connectGUIDs);

        /// <summary>
        /// GUID 인덱스 반환
        /// </summary>
        /// <param name="guid">찾으려는 GUID</param>
        /// <returns>인덱스</returns>
        public int GetConnectGUIDIndex(string guid) => connectGUIDs.IndexOf(guid);

        /// <summary>
        /// GUID 목록 재설정
        /// </summary>
        /// <param name="msg">GUID 목록 문자열</param>
        public void SetConnectGUIDs(string msg)
        {
            connectGUIDs.Clear();

            if (string.IsNullOrEmpty(msg)) return;

            foreach (string id in msg.Split(',')) connectGUIDs.Add(id);
        }

        /// <summary>
        /// 모든 연결된 정보 반환
        /// </summary>
        /// <typeparam name="T">정보 타입</typeparam>
        /// <returns>정보</returns>
        public IEnumerable<T> GetInfos<T>() where T : ConnectInfoBase => reader.GetConnectionInfos<T>();
    }
}
