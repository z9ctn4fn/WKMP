using BepInEx;
using BepInEx.Logging;
using Netcode.Transports.Facepunch;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WKMP;

[BepInPlugin(WKMPInfo.PLUGIN_GUID, WKMPInfo.PLUGIN_NAME, WKMPInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;

    private bool _alreadySpawned;

    private void Awake()
    {
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {WKMPInfo.PLUGIN_GUID} is loaded!");

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
    {
        if (scene.name == "Main-Menu")
        {
            if (!_alreadySpawned)
            {
                _alreadySpawned = true;
                SpawnNetworkManager();
            }

            WorldLoader.SetPresetSeed("4859");
        }
    }

    private void SpawnNetworkManager()
    {
        var netMan = Instantiate(new GameObject("Network Manager"));
        var netManComp = netMan.AddComponent<NetworkManager>();

        netManComp.NetworkConfig = new NetworkConfig()
        {
            NetworkTransport = netMan.AddComponent<FacepunchTransport>()
        };

        NetworkManager.Singleton.OnClientConnectedCallback += (id) =>
        {
            Logger.LogInfo("lol client connected " + id);
        };
        NetworkManager.Singleton.LogLevel = Unity.Netcode.LogLevel.Developer;

        DontDestroyOnLoad(netMan);
    }

    void OnGUI()
    {
        if (NetworkManager.Singleton == null) return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 300));
        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            StartButtons();
        }
        else
        {
            StatusLabels();
        }

        GUILayout.EndArea();
    }

    static void StartButtons()
    {
        if (GUILayout.Button("Host"))
        {
            NetworkManager.Singleton.StartHost();
        }
        if (GUILayout.Button("Client"))
        {
            (NetworkManager.Singleton.NetworkConfig.NetworkTransport as FacepunchTransport).targetSteamId = 76561198973313688;
            NetworkManager.Singleton.StartClient();
        }
        if (GUILayout.Button("Server")) NetworkManager.Singleton.StartServer();
    }

    static void StatusLabels()
    {
        var mode = NetworkManager.Singleton.IsHost ?
            "Host" : NetworkManager.Singleton.IsServer ? "Server" : "Client";

        GUILayout.Label("Transport: " +
            NetworkManager.Singleton.NetworkConfig.NetworkTransport.GetType().Name);
        GUILayout.Label("Mode: " + mode);
    }
}
