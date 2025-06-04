using UnityEngine;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Collections;
using System.Threading.Tasks;

namespace Yang.Network.Relay
{
    public class HostRelay
    {
        private Allocation hostAllocation;

        private NetworkDriver hostDriver;
        private NativeList<NetworkConnection> serverConnections;

        public bool IsCreated { get; private set; }

        private readonly NetworkReaderBase reader;

        public HostRelay(NetworkReaderBase reader)
        {
            this.reader = reader;
        }

        private async void ScheduleUpdate()
        {
            Debug.Log("Host listening.");

            while (hostDriver.IsCreated && hostDriver.Bound)
            {
                hostDriver.ScheduleUpdate().Complete();

                for (int i = 0; i < serverConnections.Length; i++)
                {
                    if (!serverConnections[i].IsCreated)
                    {
                        serverConnections.RemoveAt(i);
                        i--;
                    }
                }

                NetworkConnection conn;

                while ((conn = hostDriver.Accept()) != default)
                {
                    serverConnections.Add(conn);
                    reader.Connect(conn);

                    Debug.Log("Accepted a connection.");
                }

                for (int i = 0; i < serverConnections.Length; i++)
                {
                    NetworkEvent.Type eventType;

                    while ((eventType = hostDriver.PopEventForConnection(serverConnections[i], out var stream)) != NetworkEvent.Type.Empty)
                    {
                        switch (eventType)
                        {
                            case NetworkEvent.Type.Data:
                                DataReader(serverConnections[i], stream);
                                break;

                            case NetworkEvent.Type.Disconnect:
                                Debug.Log("Player disconnected.");

                                reader.Disconnect(serverConnections[i]);
                                serverConnections[i] = default;
                                break;
                        }
                    }
                }

                await Task.Delay(200);
            }
        }

        public async Task<string> CreateAllocation(int maxConnections)
        {
            hostAllocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(hostAllocation.AllocationId);

            Debug.Log($"Join Code: {joinCode}");

            serverConnections = new NativeList<NetworkConnection>(maxConnections, Allocator.Persistent);

            return joinCode;
        }

        public void BindHost()
        {
            var relayServerData = AllocationUtils.ToRelayServerData(hostAllocation, "dtls");
            var settings = new NetworkSettings();

            settings.WithRelayParameters(ref relayServerData);

            hostDriver = NetworkDriver.Create(settings);

            if (hostDriver.Bind(NetworkEndpoint.AnyIpv4) != 0)
            {
                Debug.LogError("Host failed to bind.");
            }
            else
            {
                if (hostDriver.Listen() != 0) Debug.LogError("Host failed to listen.");
                else
                {
                    IsCreated = true;

                    ScheduleUpdate();
                }
            }
        }

        public void TossMessage(NetworkReadInfo info)
        {
            FixedString32Bytes msg = new(info.msg);

            foreach (NetworkConnection conn in serverConnections)
            {
                if (info.conn != null && conn == info.conn) continue;

                if (hostDriver.BeginSend(conn, out DataStreamWriter writer) == 0)
                {
                    writer.WriteByte(info.type);
                    writer.WriteFixedString32(msg);

                    hostDriver.EndSend(writer);
                }
            }
        }

        public void DisconnectAll()
        {
            for (int i = 0; i < serverConnections.Length; i++)
            {
                hostDriver.Disconnect(serverConnections[i]);
                serverConnections[i] = default;
            }

            hostDriver.Dispose();

            IsCreated = false;
        }

        private void DataReader(NetworkConnection conn, DataStreamReader stream)
        {
            NetworkReadInfo readInfo = new(conn, stream.ReadByte(), stream.ReadFixedString32().ToString());

            reader.ReadValue(ref readInfo);

            TossMessage(readInfo);
        }
    }
}