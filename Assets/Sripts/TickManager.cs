using System.Net.Sockets;
using System.Net;
using System;
using Unity.Netcode;
using UnityEngine;
using System.Globalization;

public class TickManager : NetworkBehaviour {
    static public TickManager Instance { get; private set; }


    public Action OnTick = () => { };

    private NetworkTimer networkTimer;
    private int tickRate;
    public int CurrentTick { get { return networkTimer.currentTick; } }

    private void Awake() {
        Instance = this;

        DontDestroyOnLoad(gameObject);
    }
    private void Start() {
        tickRate = (int)Unity.Netcode.NetworkManager.Singleton.NetworkConfig.TickRate;
        networkTimer = new NetworkTimer(tickRate);

        Debug.LogError("tick - IsOwnedByServer: " + IsOwnedByServer);
        Debug.LogError("tick - OwnerClientId: " + OwnerClientId);
        Debug.LogError("tick - IsOwner: " + IsOwner);


        if (IsClient) {
            SyncTickRequestServerRpc(NetworkManager.Singleton.LocalClientId);

            Debug.LogError("I'm: " + NetworkManager.Singleton.LocalClientId + " tried to SyncTickRequestServerRpc");
        }
    }
    private void FixedUpdate() {
        NetworkTimerTick();
    }
    private void NetworkTimerTick() {
        networkTimer.Update(Time.fixedDeltaTime);
        while (networkTimer.ShouldTick()) OnTick();
        Debug.Log("tick" + networkTimer.currentTick);
    }
    public DateTime GetNetworkTime() {
        var ntpData = new byte[48];
        ntpData[0] = 0x1B;

        var ntpServer = "pool.ntp.org";
        var addresses = Dns.GetHostEntry(ntpServer).AddressList;
        var ipEndPoint = new IPEndPoint(addresses[0], 123);

        using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)) {
            socket.Connect(ipEndPoint);
            socket.Send(ntpData);
            socket.Receive(ntpData);
            socket.Close();
        }

        var intPart = (ulong)ntpData[40] << 24 | (ulong)ntpData[41] << 16 | (ulong)ntpData[42] << 8 | ntpData[43];
        var fractPart = (ulong)ntpData[44] << 24 | (ulong)ntpData[45] << 16 | (ulong)ntpData[46] << 8 | ntpData[47];

        var milliseconds = (intPart * 1000) + (fractPart * 1000 / 0x100000000L);
        var networkDateTime = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds((long)milliseconds);

        return networkDateTime.ToLocalTime();
    }
    [ClientRpc]
    private void SyncTickClientRpc(DateTime serverGlobalTime, float serverTime, ClientRpcParams clientRpcParams) {
        float delta = (float)(GetNetworkTime() - serverGlobalTime).TotalSeconds;
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

        SyncTickClientRpc(GetNetworkTime(), (float)NetworkManager.NetworkTimeSystem.ServerTime, clientRpcParams);
    }



}
