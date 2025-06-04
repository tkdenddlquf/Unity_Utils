using System.Collections.Generic;
using Unity.Networking.Transport;
using UnityEngine;

namespace Yang.Network.Relay
{
    public abstract class NetworkReaderBase : MonoBehaviour
    {
        protected Dictionary<NetworkConnection, ConnectInfoBase> ConnectionInfos { get; set; } = new();

        public virtual void Connect(NetworkConnection conn) => ConnectionInfos.Add(conn, null);

        public virtual void Disconnect(NetworkConnection conn) => ConnectionInfos.Remove(conn);

        public IEnumerable<T> GetConnectionInfos<T>() where T : ConnectInfoBase
        {
            foreach (var info in ConnectionInfos.Values)
            {
                if (info is T result) yield return result;
            }
        }

        public T GetConnectionInfo<T>(NetworkConnection conn) where T : ConnectInfoBase
        {
            if (ConnectionInfos[conn] is T result) return result;

            return null;
        }

        /// <summary>
        /// 호스트에 들어오는 메시지 처리
        /// </summary>
        /// <param name="info">메시지 정보</param>
        public abstract void ReadValue(ref NetworkReadInfo info);

        /// <summary>
        /// 클라이언트에 들어오는 메시지 처리
        /// </summary>
        /// <param name="type">메시지 타입</param>
        /// <param name="msg">내용</param>
        public abstract void ReadValue(byte type, string msg);
    }
}
