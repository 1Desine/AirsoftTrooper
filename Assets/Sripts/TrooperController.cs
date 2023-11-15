using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using Unity.Multiplayer.Samples.Utilities.ClientAuthority;
using Unity.Netcode;
using UnityEngine;
using Utilities;


// reference https://youtu.be/-lGsuCEWkM0?si=gNsTcQw-mxeJTqac

// Network variables should be value objects
public struct InputPayload : INetworkSerializable {
    public int tick;
    public DateTime timestamp;
    public ulong networkObjectId;
    public Vector3 position;

    public Vector3 inputVector;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
        serializer.SerializeValue(ref tick);
        serializer.SerializeValue(ref timestamp);
        serializer.SerializeValue(ref networkObjectId);
        serializer.SerializeValue(ref position);

        serializer.SerializeValue(ref inputVector);
    }
}

public struct StatePayload : INetworkSerializable {
    public int tick;
    public ulong networkObjectId;
    public Vector3 position;
    public Vector3 velocity;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
        serializer.SerializeValue(ref tick);
        serializer.SerializeValue(ref networkObjectId);
        serializer.SerializeValue(ref position);
        serializer.SerializeValue(ref velocity);
    }
}

public class TrooperController : NetworkBehaviour {
    Rigidbody body;
    new CapsuleCollider collider;
    ClientNetworkTransform clientNetworkTransform;

    // Netcode general
    NetworkTimer networkTimer;
    const float serverTickRate = 30f;
    const int bufferSize = 1024;

    DateTime awakeDateTime; // this will differ on the server and on the client, it's needed to sync tick

    // Network client specific
    CircularBuffer<StatePayload> clientStateBuffer;
    CircularBuffer<InputPayload> clientInputBuffer;
    StatePayload lastServerState;
    StatePayload lastProcessedState;

    // Netcode server specific
    CircularBuffer<StatePayload> serverStateBuffer;
    Queue<InputPayload> serverInputQueue;
    [Header("Netcode")]
    [SerializeField] float reconceliationThreshold = 0.1f;
    [SerializeField] float reconceliationCooldownTime = 1f;
    [SerializeField] Transform serverSphere;
    [SerializeField] Transform clientSphere;

    StatePayload intrapolationState;


    CountdownTimer reconceliationTimer;

    private void Awake() {
        body = GetComponent<Rigidbody>();
        collider = GetComponent<CapsuleCollider>();
        clientNetworkTransform = GetComponent<ClientNetworkTransform>();

        // Network setting
        networkTimer = new NetworkTimer(serverTickRate);
        awakeDateTime = GetNetworkTime();

        clientStateBuffer = new CircularBuffer<StatePayload>(bufferSize);
        clientInputBuffer = new CircularBuffer<InputPayload>(bufferSize);
        serverStateBuffer = new CircularBuffer<StatePayload>(bufferSize);
        serverInputQueue = new Queue<InputPayload>();

        reconceliationTimer = new CountdownTimer(reconceliationCooldownTime);
    }
    private void Start() {
        serverSphere.parent = null;
        clientSphere.parent = null;
        body.isKinematic = false;

        if (IsServer) SyncNetworkTimerClientRpc(awakeDateTime);
        Debug.Log(GetNetworkTime().TimeOfDay.TotalSeconds);
        Debug.Log(NetworkManager.ServerTime.TimeAsFloat);
    }
    private void Update() {
        if (IsOwner) {
            if (Input.GetKeyDown(KeyCode.Q)) transform.position -= transform.forward * 10;
            if (Input.GetKeyDown(KeyCode.E)) transform.position += transform.forward * 10;
        }

        clientSphere.position = transform.position; // DEBUG SPHERE

        reconceliationTimer.Tick(Time.deltaTime);
    }
    private void FixedUpdate() {
        NetworkTimerTick();
    }
    private void NetworkTimerTick() {
        networkTimer.Update(Time.fixedDeltaTime);
        while (networkTimer.ShouldTick()) {
            HandleClientTick();
            HandleServerTick();
        }
        Debug.Log("time" + networkTimer.timer);
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
    private void SyncNetworkTimerClientRpc(DateTime serverGlobalTimeAtSpawn) {
        float delta = (float)(awakeDateTime - serverGlobalTimeAtSpawn).TotalSeconds;
        networkTimer.Update(delta);
    }
    private void HandleIntrapolation() {
        if (IsOwner || IsServer) return;

        // intrapolation for clients
    }
    private void HandleServerTick() {
        if (!IsServer) return;

        var bufferIndex = -1;
        while (serverInputQueue.Count > 0) {
            InputPayload inputPayLoad = serverInputQueue.Dequeue();

            bufferIndex = inputPayLoad.tick % bufferSize;
            StatePayload statePayload = ProcessMovement(inputPayLoad);
            serverStateBuffer.Add(statePayload, bufferIndex);
        }

        if (bufferIndex == -1) return;
        SendToClientRpc(serverStateBuffer.Get(bufferIndex));
        serverSphere.position = serverStateBuffer.Get(bufferIndex).position; // DEBUG SPHERE
    }
    private static float CalculateLatencyInMilliseconds(InputPayload inputPayload) {
        return (DateTime.Now - inputPayload.timestamp).Milliseconds / 1000f;
    }
    [ClientRpc]
    private void SendToClientRpc(StatePayload statePayload) {
        if (IsOwner) {
            lastServerState = statePayload;
        }
        else {
            transform.position = statePayload.position;
            body.velocity = statePayload.velocity;
        }
    }
    private void HandleClientTick() {
        if (!IsClient || !IsOwner) return;
        HandleServerReconciliation();

        var currentTick = networkTimer.currentTick;
        var bufferIndex = currentTick % bufferSize;

        InputPayload inputPayLoad = new InputPayload() {
            tick = currentTick,
            timestamp = DateTime.UtcNow,
            networkObjectId = NetworkObjectId,
            position = transform.position,

            inputVector = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized,
        };

        clientInputBuffer.Add(inputPayLoad, bufferIndex);
        SendToServerRpc(inputPayLoad);

        StatePayload statePayload = ProcessMovement(inputPayLoad);
        clientStateBuffer.Add(statePayload, bufferIndex);
    }
    private StatePayload ProcessMovement(InputPayload input) {
        Move(input.inputVector);

        return new StatePayload() {
            tick = input.tick,
            networkObjectId = input.networkObjectId,
            position = transform.position,
            velocity = body.velocity,
        };
    }

    [ServerRpc]
    private void SendToServerRpc(InputPayload input) {
        serverInputQueue.Enqueue(input);
    }
    private bool ShouldReconcile() {
        bool isNewServerState = !lastServerState.Equals(default);
        bool isLastStateUndefinedOrDifferent = lastProcessedState.Equals(default)
                                            || !lastProcessedState.Equals(lastServerState);

        return isNewServerState && isLastStateUndefinedOrDifferent
            && !reconceliationTimer.IsRunning;
    }
    private void HandleServerReconciliation() {
        if (!ShouldReconcile()) return;

        int bufferIndex = lastServerState.tick % bufferSize;
        if (bufferIndex - 1 < 0) return; // Not enough informaion to reconcile

        float positionError = Vector3.Distance(lastServerState.position, clientStateBuffer.Get(bufferIndex).position);

        Debug.Log("Server: " + lastServerState.position);
        Debug.Log("Client: " + clientStateBuffer.Get(bufferIndex).position);
        Debug.Log("The margin: " + positionError);

        if (positionError > reconceliationThreshold) {
            Debug.Log("Had to Reconcile, diviation is: " + positionError);
            ReconcileState();
            reconceliationTimer.Start();
        }

        //intrapolationState = lastProcessedState; // intrapolation for clients
        lastProcessedState = lastServerState;
    }
    private void ReconcileState() {
        transform.position = lastServerState.position;
        body.velocity = lastServerState.velocity;

        clientStateBuffer.Add(lastServerState, lastServerState.tick);

        // Replay all inputs from the rewind state to the current state
        int tickToReplay = lastServerState.tick;

        while (tickToReplay < networkTimer.currentTick) {
            int bufferIndex = tickToReplay % bufferSize;
            StatePayload recalculatedClientPayload = ProcessMovement(clientInputBuffer.Get(bufferIndex));
            clientStateBuffer.Add(recalculatedClientPayload, bufferIndex);
            tickToReplay++;
        }
    }

    private void Move(Vector2 moveInput) {
        Vector3 moveDirection = transform.forward * moveInput.y + transform.right * moveInput.x;

        float moveSpeed = 10f;
        //transform.position += moveDirection * moveSpeed * networkTimer.minTimeBetweenTicks;
        body.velocity += moveDirection * moveSpeed * networkTimer.minTimeBetweenTicks;
    }


}
