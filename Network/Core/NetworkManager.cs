using UnityEngine;
using Yang.Network.Steam;

namespace Yang.Network
{
    public class NetworkManager : MonoBehaviour
    {
        private static NetworkBase network;
        public static NetworkBase Instance => network;

        private readonly SteamNetwork steamNetwork = new();

        private void Awake()
        {
            DontDestroyOnLoad(this);

            if (steamNetwork.Init()) network = steamNetwork;
            else Application.Quit();
        }

        private void OnDestroy() => network.OnDestroy();
    }
}
