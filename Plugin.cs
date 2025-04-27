using BepInEx;
using BepInEx.Logging;
using Netcode.Transports.Facepunch;
using Steamworks;
using System;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using static UnityEngine.UIElements.StylePropertyAnimationSystem;

namespace WKMP;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;
    private bool _alreadySpawned;
    public static SteamId Id => SteamClient.SteamId;
    public GameObject netMan = Instantiate(new GameObject("Network Manager"));
    public NetworkManager netManComp = null;
    public static IntPtr adress = IntPtr.Zero;


    private void Awake()
    {
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");


        SceneManager.sceneLoaded += OnSceneLoaded;
        netManComp = netMan.AddComponent<NetworkManager>();

    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
    {
        bool commandsLoaded = false;
        if (scene.name == "Main-Menu")
        {

            if (!_alreadySpawned)
            {
                _alreadySpawned = true;
                SpawnNetworkManager();
            }
        }
        if (!commandsLoaded)
        {
            CommandConsole.AddCommand("host", StartHost, false);
            CommandConsole.AddCommand("join", StartClient, false);
        }
    }

    private void SpawnNetworkManager()
    {

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
    }
    static void StartHost(string[] yarg) 
    {
        NetworkManager.Singleton.StartHost();

    }
    static void StartClient(string[] StringArraySteamID)
    {
        string StringSteamID = string.Concat(StringArraySteamID);
        ulong SteamId = 0;
        if (long.TryParse(StringSteamID, out long parsed)) SteamId = (ulong)parsed;
        
        (NetworkManager.Singleton.NetworkConfig.NetworkTransport as FacepunchTransport).targetSteamId = SteamId;
        NetworkManager.Singleton.StartClient();
    }
    public void SendChat(string[] message)
    {

    }
}
