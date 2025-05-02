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
using System.Collections;

namespace WKMP;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;
    private bool _alreadySpawned;
    public GameObject prefabClone;
    public static GameObject netMan;
    public NetworkManager netManComp;
    public Plugin Instance;
    public FacepunchTransport transport;
    private void Awake()
    {
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        SceneManager.sceneLoaded += OnSceneLoaded;
        Instance = this;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
    {
        if (scene.name != "Main-Menu") return;

        if (!_alreadySpawned)
        {
            _alreadySpawned = true;
            SpawnNetworkManager();  // starts coroutine setup
        }

        // Register commands each scene load
        CommandConsole.AddCommand("host", args =>
        {
            if (this == null)
            {
                Debug.LogError("[MOD] Plugin.Instance is null in host command.");
                return;
            }

            StartCoroutine(WaitForSteamAndStartHost());
        }, false);

        CommandConsole.AddCommand("join", args =>
        {
            if (this == null)
            {
                Debug.LogError("[MOD] Plugin.Instance is null in join command.");
                return;
            }

            if (args.Length == 0 || !ulong.TryParse(string.Join("", args), out ulong id))
            {
                Debug.LogError("[MOD] Missing or invalid Steam ID.");
                return;
            }

            StartCoroutine(WaitForSteamAndStartClient(id));
        }, false);
    }


    public void SpawnNetworkManager()
    {
        netMan = new GameObject("NetworkManager (Mod)");
        netManComp = netMan.AddComponent<NetworkManager>();
        transport = netMan.AddComponent<FacepunchTransport>();
        netManComp.NetworkConfig = new NetworkConfig()
        {
            NetworkTransport = transport
        };

        DontDestroyOnLoad(netMan);

        // ✅ Hook connection event BEFORE networking starts
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

        // ✅ Kick off prefab setup now (netManComp is valid)
        netManComp.StartCoroutine(SetupPlayerPrefabWhenReady());

        Debug.Log("[MOD] NetworkManager spawned and coroutine started.");
    }

    void OnGUI()
    {
        if (NetworkManager.Singleton == null) return;
    }
    public void StartHost(string[] args)
    {
        StartCoroutine(WaitForSteamAndStartHost());
    }

    public void StartClient(string[] args)
    {
        if (this == null)
        {
            Debug.LogError("[MOD] Plugin.Instance is null — cannot start coroutine.");
            return;
        }
        if (args.Length == 0)
        {
            Debug.LogError("[MOD] You must pass a Steam ID to connect to.");
            return;
        }

        if (!ulong.TryParse(string.Join("", args), out ulong steamId))
        {
            Debug.LogError("[MOD] Invalid Steam ID: " + string.Join("", args));
            return;
        }

        StartCoroutine(WaitForSteamAndStartClient(steamId));
    }
    void Update()
    {
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }
    private IEnumerator SetupPlayerPrefabWhenReady()
    {
        // 🛑 Check if we already created a prefab clone
        if (prefabClone != null)
        {
            Debug.LogWarning("Prefab clone already created. Skipping setup.");
            yield break;
        }

        GameObject sceneObject = null;

        while (sceneObject == null)
        {
            sceneObject = GameObject.Find("CL_Player");
            yield return null;
        }

        Debug.Log("Found CL_Player!");

        // Clone and patch
        prefabClone = Instantiate(sceneObject);
        prefabClone.name = "CL_Player_PrefabClone";
        prefabClone.SetActive(false);

        if (prefabClone.GetComponent<NetworkObject>() == null)
            prefabClone.AddComponent<NetworkObject>();

        if (prefabClone.GetComponent<NetworkTransform>() == null)
            prefabClone.AddComponent<NetworkTransform>();

        NetworkManager.Singleton.NetworkConfig.PlayerPrefab = prefabClone;

        Debug.Log("Assigned PlayerPrefab successfully.");
    }
    private IEnumerator WaitForSteamAndStartHost()
    {
        Debug.Log("[MOD] Waiting for Steam to initialize...");

        yield return new WaitUntil(() => SteamClient.IsValid);
        yield return new WaitForSeconds(1f); // extra buffer

        if (transport == null)
        {
            Debug.LogError("[MOD] FacepunchTransport not found on manually spawned NetworkManager.");
            yield break;
        }
        Debug.Log("[MOD] Starting host...");
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        netManComp.StartHost();
    }

    private IEnumerator WaitForSteamAndStartClient(ulong targetSteamId)
    {
        Debug.Log("[MOD] Waiting for Steam to initialize...");

        yield return new WaitUntil(() => SteamClient.IsValid);
        yield return new WaitForSeconds(1f); // buffer to ensure relay setup

        if (transport == null)
        {
            Debug.LogError("[MOD] FacepunchTransport not found on manually spawned NetworkManager.");
            yield break;
        }

        transport.targetSteamId = targetSteamId;

        Debug.Log("[MOD] Starting client connection to " + targetSteamId);
        netManComp.OnClientConnectedCallback += OnClientConnected;
        netManComp.OnClientDisconnectCallback += OnClientDisconnected;

        netManComp.StartClient();
    }
    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[MOD] Client connected: {clientId}");

        if (NetworkManager.Singleton.IsServer && clientId != NetworkManager.Singleton.LocalClientId)
        {
            var prefab = NetworkManager.Singleton.NetworkConfig.PlayerPrefab;
            if (prefab != null)
            {
                var instance = Instantiate(prefab);
                instance.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
                Debug.Log($"[MOD] Spawned remote player for client {clientId}");
            }
            else
            {
                Debug.LogError("[MOD] No PlayerPrefab set.");
            }
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.LogWarning($"[MOD] Client disconnected or failed to connect: {clientId}");
    }
}


