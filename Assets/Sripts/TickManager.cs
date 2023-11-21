using System.Net.Sockets;
using System.Net;
using System;
using Unity.Netcode;
using UnityEngine;
using System.Globalization;

public class TickManager : NetworkBehaviour {
    static public TickManager Instance { get; private set; }


    static public Action OnTick = () => { };

    private NetworkTimer networkTimer;
    private int tickRate;
    static public int TickRate { get => Instance.tickRate;  }
    static public int CurrentTick { get => Instance.networkTimer.currentTick; }
    static public float deltaTick { get => Instance.networkTimer.minTimeBetweenTicks; }

    private void Awake() {
        Instance = this;

        DontDestroyOnLoad(gameObject);
    }
    private void Start() {
        tickRate = (int)NetworkManager.Singleton.NetworkConfig.TickRate;
        networkTimer = new NetworkTimer(tickRate);

        if (IsClient) {
            SyncTickRequestServerRpc(NetworkManager.Singleton.LocalClientId);

            Debug.LogError("I'm: \"" + NetworkManager.Singleton.LocalClientId + "\" SyncTickRequestServerRpc");
        }
    }
    private void FixedUpdate() {
        NetworkTimerTick();
    }
    private void NetworkTimerTick() {
        networkTimer.Update(Time.fixedDeltaTime);
        while (networkTimer.ShouldTick()) OnTick();
        //Debug.Log("tick" + networkTimer.currentTick);
    }
    [ClientRpc]
    private void SyncTickClientRpc(DateTime serverGlobalTime, float serverTime, ClientRpcParams clientRpcParams) {
        float delta = (float)(DateTime.UtcNow - serverGlobalTime).TotalSeconds;
        networkTimer = new NetworkTimer(tickRate);
        networkTimer.Update(serverTime + delta);
    }
    [ServerRpc(RequireOwnership = false)]
    private void SyncTickRequestServerRpc(ulong clientId) {
        ClientRpcParams clientRpcParams = new ClientRpcParams {
            Send = new ClientRpcSendParams() {
                TargetClientIds = new[] { clientId },
            }
        };

        SyncTickClientRpc(DateTime.UtcNow, (float)NetworkManager.NetworkTimeSystem.ServerTime, clientRpcParams);
    }



}
