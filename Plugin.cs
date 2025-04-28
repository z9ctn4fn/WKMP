using BepInEx;
using BepInEx.Logging;
using Netcode.Transports.Facepunch;
using Steamworks;
using System;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.SceneManagement;
using static UnityEngine.UIElements.StylePropertyAnimationSystem;
using Unity.Mathematics;

namespace WKMP;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;
    private bool _alreadySpawned;

    public static GameObject netMan;
    public NetworkManager netManComp;
    private void Awake()
    {
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        SceneManager.sceneLoaded += OnSceneLoaded;

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

    public void SpawnNetworkManager()
    {
        netMan = Instantiate(new GameObject("Network Manager"));
        netManComp = netMan.AddComponent<NetworkManager>();
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
    void StartHost(string[] args)
    {

        NetworkManager.Singleton.StartHost();
        GameObject.Find("CL_Player").AddComponent<NetworkObject>();
        GameObject.Find("CL_Player").AddComponent<NetworkBehaviour>();
        GameObject.Find("CL_Player").AddComponent<NetworkTransform>();
        netManComp.AddNetworkPrefab(GameObject.Find("CL_Player"));
    }
    void StartClient(string[] StringArraySteamID)
    {
        string StringSteamID = string.Concat(StringArraySteamID);
        ulong SteamId = 0;
        if (long.TryParse(StringSteamID, out long parsed)) SteamId = (ulong)parsed;
        
        (NetworkManager.Singleton.NetworkConfig.NetworkTransport as FacepunchTransport).targetSteamId = SteamId;
        NetworkManager.Singleton.StartClient();
        GameObject.Find("CL_Player").AddComponent<NetworkObject>();
        GameObject.Find("CL_Player").AddComponent<NetworkBehaviour>();
        GameObject.Find("CL_Player").AddComponent<NetworkTransform>();
        netManComp.AddNetworkPrefab(GameObject.Find("CL_Player"));
    }
}
