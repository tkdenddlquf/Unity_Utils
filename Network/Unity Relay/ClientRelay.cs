using UnityEngine;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using System.Threading.Tasks;
using Unity.Collections;

namespace Yang.Network.Relay
{
    public class ClientRelay
    {
        private JoinAllocation clientAllocation;

        private NetworkDriver playerDriver;
        private NetworkConnection clientConnection;

        private readonly NetworkReaderBase reader;

        public bool IsConnect { get; private set; }

        public ClientRelay(NetworkReaderBase reader)
        {
            this.reader = reader;
        }

        private async void ScheduleUpdate()
        {
            Debug.Log("Player sent connection request to host.");

            while (playerDriver.IsCreated && playerDriver.Bound)
            {
                playerDriver.ScheduleUpdate().Complete();

                NetworkEvent.Type eventType;

                while ((eventType = clientConnection.PopEvent(playerDriver, out var stream)) != NetworkEvent.Type.Empty)
                {
                    switch (eventType)
                    {
                        case NetworkEvent.Type.Data:
                            DataReader(stream);
                            break;

                        case NetworkEvent.Type.Connect:
                            Debug.Log("Connected to host.");

                            IsConnect = true;
                            break;

                        case NetworkEvent.Type.Disconnect:
                            Debug.Log("Disconnected from host.");

                            IsConnect = false;
                            clientConnection = default;
                            break;

                        default:
                            Debug.Log(eventType);
                            break;
                    }
                }

                await Task.Delay(200);
            }
        }

        public async Task JoinAllocation(string joinCode)
        {
            clientAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
        }

        public bool BindAndConnect()
        {
            var relayServerData = AllocationUtils.ToRelayServerData(clientAllocation, "dtls");
            var settings = new NetworkSettings();

            settings.WithRelayParameters(ref relayServerData);

            playerDriver = NetworkDriver.Create(settings);

            if (playerDriver.Bind(NetworkEndpoint.AnyIpv4) != 0)
            {
                Debug.LogError("Player failed to bind.");

                return false;
            }

            clientConnection = playerDriver.Connect(relayServerData.Endpoint);

            if (clientConnection.IsCreated)
            {
                ScheduleUpdate();

                return true;
            }

            return false;
        }

        public void TossMessage(byte type, string message)
        {
            if (!clientConnection.IsCreated)
            {
                Debug.LogError("Client is not connected.");

                return;
            }

            if (playerDriver.BeginSend(clientConnection, out var writer) == 0)
            {
                writer.WriteByte(type);
                writer.WriteFixedString32(message);
                playerDriver.EndSend(writer);
            }
        }

        public void Disconnect()
        {
            playerDriver.Disconnect(clientConnection);
            clientConnection = default;

            playerDriver.Dispose();
        }

        private void DataReader(DataStreamReader stream)
        {
            byte type = stream.ReadByte();
            string msg = stream.ReadFixedString32().ToString();

            reader.ReadValue(type, msg);
        }
    }
}
