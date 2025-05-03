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
using Steamworks.Data;

namespace WKMP;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;
    private bool _alreadySpawned;
    public GameObject prefabClone;
    public static GameObject netMan;
    public NetworkManager netManComp;
    public static Plugin Instance;
    public FacepunchTransport transport;
    private void Awake()
    {
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        SceneManager.sceneLoaded += OnSceneLoaded;
        Instance = this;
        SteamClient.Init(3195790, true);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
    {
        var commandsLoaded = false;
        if (scene.name == "Main-Menu")
        {
            if (!_alreadySpawned)
            {
                _alreadySpawned = true;
                SpawnNetworkManager();  // this now starts the coroutine too
            }
        }

        if (!commandsLoaded)
        {
            CommandConsole.AddCommand("host", StartHost, false);
            CommandConsole.AddCommand("join", StartClient, false);
            commandsLoaded = true;
        }
    }


    public void SpawnNetworkManager()
    {
        netMan = new GameObject("NetworkManager (Mod)");
        netManComp = netMan.AddComponent<NetworkManager>();
        transport = netMan.AddComponent<FacepunchTransport>();
        netManComp.NetworkConfig = new NetworkConfig();
        netManComp.NetworkConfig.NetworkTransport = transport;
        netManComp.NetworkConfig.ConnectionApproval = true;
        DontDestroyOnLoad(netMan);

        netManComp.OnClientConnectedCallback += OnClientConnected;
        netManComp.StartCoroutine(SetupPlayerPrefabWhenReady());

        Debug.Log("[MOD] NetworkManager spawned and coroutine started.");
        CommandConsole.AddCommand("host", StartHost, false);
        CommandConsole.AddCommand("join", StartClient, false);
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
        Instance = Plugin.Instance;
        if (Instance == null)
        {
            Debug.LogError("[MOD] This should not be possible. What the fuck?");
            StartCoroutine(RetryCommandLater(() => StartClient(args)));
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

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }
    private IEnumerator SetupPlayerPrefabWhenReady()
    {
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
        yield return new WaitUntil(() => prefabClone != null); 

        if (transport == null)
        {
            Debug.LogError("[MOD] FacepunchTransport not found.");
            yield break;
        }
        netManComp.NetworkConfig.PlayerPrefab = prefabClone;
        netManComp.ConnectionApprovalCallback = (request, response) =>
        {
            response.Approved = true;
            response.CreatePlayerObject = true;
            response.Pending = false;
        };

        Debug.Log("[MOD] Starting host...");
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        netManComp.StartHost();
    }

    private IEnumerator WaitForSteamAndStartClient(ulong targetSteamId)
    {
        Debug.Log("[MOD] Waiting for Steam to initialize...");

        yield return new WaitUntil(() => SteamClient.IsValid);
        yield return new WaitUntil(() => prefabClone != null);

        if (transport == null)
        {
            Debug.LogError("[MOD] FacepunchTransport not found.");
            yield break;
        }

        transport.targetSteamId = targetSteamId;
        netManComp.NetworkConfig.PlayerPrefab = prefabClone;
        netManComp.ConnectionApprovalCallback = (request, response) =>
        {
            response.Approved = true;
            response.CreatePlayerObject = true;
            response.Pending = false;
        };

        Debug.Log("[MOD] Starting client...");
        netManComp.OnClientConnectedCallback += OnClientConnected;
        netManComp.OnClientDisconnectCallback += OnClientDisconnected;

        netManComp.StartClient();
    }
    private bool wasConnected = false;

    void Update()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient) return;

        if (NetworkManager.Singleton.IsConnectedClient && !wasConnected)
        {
            wasConnected = true;
        }
        else if (!NetworkManager.Singleton.IsConnectedClient && wasConnected)
        {
            wasConnected = false;
            Debug.LogWarning("[MOD] Detected client disconnection manually.");
            OnClientDisconnected(NetworkManager.Singleton.LocalClientId);
        }
    }
    private void OnClientConnected(ulong clientId)
    {

        Debug.Log($"[MOD] Client connected: {clientId}");

        if (NetworkManager.Singleton.IsServer && clientId != 0)
        {
            var prefab = NetworkManager.Singleton.NetworkConfig.PlayerPrefab;
            if (prefab != null)
            {
                var instance = Instantiate(prefab);
                instance.SetActive(true);
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
        Debug.LogError($"[MOD] Client disconnected. Possible connection failure to host. ID: {clientId}");
    }
    private IEnumerator RetryCommandLater(Action retryAction)
    {
        yield return new WaitForSeconds(0.5f);
        retryAction?.Invoke();
    }
}


