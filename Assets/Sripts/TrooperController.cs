using Newtonsoft.Json.Bson;
using System;
using System.Collections.Generic;
using Unity.Multiplayer.Samples.Utilities.ClientAuthority;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;
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
    }
}

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class TrooperController : NetworkBehaviour {

    Rigidbody body;
    new CapsuleCollider collider;
    ClientNetworkTransform clientNetworkTransform;

    // Netcode general
    NetworkTimer networkTimer;
    const float serverTickRate = 60f;
    const int bufferSize = 1024;

    // Network client specific
    CircularBuffer<StatePayload> clientStateBuffer;
    CircularBuffer<InputPayload> clientInputBuffer;
    StatePayload lastServerState;
    StatePayload lastProcessedState;

    // Netcode server specific
    CircularBuffer<StatePayload> serverStateBuffer;
    Queue<InputPayload> serverInputQueue;
    [Header("Netcode")]
    [SerializeField] float reconceliationThreshold = 10f;
    [SerializeField] float reconceliationCooldownTime = 1f;
    [SerializeField] float extrapolationLimit = 0.5f; // 0.5f = 500 milliseconds
    [SerializeField] float extrapolationMultiplayer = 1.2f;
    [SerializeField] Transform serverSphere;
    [SerializeField] Transform clientSphere;

    StatePayload extrapolationState;
    CountdownTimer extrapolationTimer;


    CountdownTimer reconceliationTimer;

    private void Awake() {
        body = GetComponent<Rigidbody>();
        collider = GetComponent<CapsuleCollider>();
        clientNetworkTransform = GetComponent<ClientNetworkTransform>();

        // Network setting
        networkTimer = new NetworkTimer(serverTickRate);
        clientStateBuffer = new CircularBuffer<StatePayload>(bufferSize);
        clientInputBuffer = new CircularBuffer<InputPayload>(bufferSize);
        serverStateBuffer = new CircularBuffer<StatePayload>(bufferSize);
        serverInputQueue = new Queue<InputPayload>();

        reconceliationTimer = new CountdownTimer(reconceliationCooldownTime);
        extrapolationTimer = new CountdownTimer(extrapolationLimit);
        extrapolationTimer.OnTimerStart += () => {
            reconceliationTimer.Stop();
            SwitchAuthorityMode(AuthorityMode.Server);
        };
        extrapolationTimer.OnTimerStop += () => {
            extrapolationState = default;
            SwitchAuthorityMode(AuthorityMode.Client);
        };
    }
    private void SwitchAuthorityMode(AuthorityMode mode) {
        clientNetworkTransform.authorityMode = mode;
        bool shouldSync = mode == AuthorityMode.Client;
        clientNetworkTransform.SyncPositionX = shouldSync;
        clientNetworkTransform.SyncPositionY = shouldSync;
        clientNetworkTransform.SyncPositionZ = shouldSync;
    }
    private void Start() {
        //body.isKinematic = true;
        serverSphere.parent = null;
    }
    private void Update() {
        //body.isKinematic = false;
        //Debug.Log("extrapolationTimer.IsRunning: "+ extrapolationTimer.IsRunning);

        networkTimer.Update(Time.deltaTime);
        reconceliationTimer.Tick(Time.deltaTime);
        extrapolationTimer.Tick(Time.deltaTime);


        if (IsOwner == false) return;
        if (Input.GetKeyDown(KeyCode.Q)) transform.position -= transform.forward * 10;
        if (Input.GetKeyDown(KeyCode.E)) transform.position += transform.forward * 10;


        // Run on Update of FixedUpdate, or both - depends on the game, consider exposing an option to the game
        Extrapolate();
    }
    private void FixedUpdate() {
        while (networkTimer.ShouldTick()) {
            HandleClientTick();
            HandleServerTick();
        }
        Extrapolate();
    }

    private void Extrapolate() {
        if (IsServer && extrapolationTimer.IsRunning) {
            //transform.position += extrapolationState.position.With(y: 0);
            body.velocity = extrapolationState.velocity.With(y: 0);
        }
    }
    private void HandleServerTick() {
        if (!IsServer) return;

        var bufferIndex = -1;
        InputPayload inputPayLoad = default;
        while (serverInputQueue.Count > 0) {
            inputPayLoad = serverInputQueue.Dequeue();

            bufferIndex = inputPayLoad.tick % bufferSize;

            StatePayload statePayload = ProcessMovement(inputPayLoad);
            serverStateBuffer.Add(statePayload, bufferIndex);
        }

        if (bufferIndex == -1) return;
        SendToClientRpc(serverStateBuffer.Get(bufferIndex));
        HandleExtrapolation(serverStateBuffer.Get(bufferIndex), CalculateLatencyInMilliseconds(inputPayLoad));
    }
    private static float CalculateLatencyInMilliseconds(InputPayload inputPayload) {
        return (DateTime.Now - inputPayload.timestamp).Milliseconds / 1000f;
    }
    private bool ShoutldExtrapolate(float latency) => latency < extrapolationLimit && latency > Time.fixedDeltaTime;
    private void HandleExtrapolation(StatePayload latest, float latency) {
        if (ShoutldExtrapolate(latency)) {
            if (extrapolationState.position != default) latest = extrapolationState;

            var positionAdjustent = latest.velocity * (1 + latency * extrapolationMultiplayer);
            extrapolationState.position = positionAdjustent;
            extrapolationState.velocity = latest.velocity;

            extrapolationTimer.Start();
        }
        else {
            extrapolationTimer.Stop();
            // Reconcile if defired
        }
    }

    [ClientRpc]
    private void SendToClientRpc(StatePayload statePayload) {
        if (IsServer || IsHost)
            serverSphere.position = lastServerState.position; // DEBUG SPHERE

        if (IsOwner == false) return;
        lastServerState = statePayload;
    }
    //private StatePayload SimulateMovement(InputPayLoad inputPayLoad) {
    //    Physics.simulationMode = SimulationMode.Script;
    //    Move(inputPayLoad.inputVector);
    //    Physics.Simulate(Time.fixedDeltaTime);
    //    Physics.simulationMode = SimulationMode.FixedUpdate;
    //
    //    return new StatePayload() {
    //        tick = inputPayLoad.tick,
    //        position = transform.position,
    //    };
    //}
    private void HandleClientTick() {
        if (!IsClient || !IsOwner) return;

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

        HandleServerReconciliation();
    }
    [ServerRpc]
    private void SendToServerRpc(InputPayload input) {
        clientSphere.position = input.position; // DEBUG SPHERE

        serverInputQueue.Enqueue(input);
    }
    private bool ShouldReconcile() {
        bool isNewServerState = !lastServerState.Equals(default);
        bool isLastStateUndefinedOrDifferent = lastProcessedState.Equals(default)
                                            || !lastProcessedState.Equals(lastServerState);

        return isNewServerState && isLastStateUndefinedOrDifferent
            && !reconceliationTimer.IsRunning
            && !extrapolationTimer.IsRunning;
    }
    private void HandleServerReconciliation() {
        if (!ShouldReconcile()) return;

        float positionErrof;
        int bufferIndex;

        bufferIndex = lastServerState.tick % bufferSize;
        if (bufferIndex - 1 < 0) return; // Not enough informaion to reconcile

        StatePayload rewindState = IsHost ? serverStateBuffer.Get(bufferIndex - 1) : lastServerState; // Host RPCs execute immedietelt so we can use the last server state
        StatePayload clientState = IsHost ? clientStateBuffer.Get(bufferIndex - 1) : clientStateBuffer.Get(bufferIndex);
        positionErrof = Vector3.Distance(rewindState.position, clientState.position);
        //positionErrof = Vector3.Distance(lastServerState.position, clientStateBuffer.Get(bufferIndex).position);

        if (positionErrof > reconceliationThreshold) {
            Debug.Log("Had to Reconcile, diviation is: " + positionErrof);
            //Debug.Break();
            ReconcileState(rewindState);
            reconceliationTimer.Start();
        }

        lastProcessedState = lastServerState;
    }
    private void ReconcileState(StatePayload rewindState) {
        transform.position = rewindState.position;
        body.velocity = Vector3.zero;//rewindState.velocity;

        if (!rewindState.Equals(lastServerState)) return;

        clientStateBuffer.Add(rewindState, rewindState.tick);

        // Replay all inputs front the rewind state to the current state
        int tickToReplay = lastServerState.tick;

        while (tickToReplay < networkTimer.currentTick) {
            int bufferIndex = tickToReplay % bufferSize;
            StatePayload statePayload = ProcessMovement(clientInputBuffer.Get(bufferIndex));
            clientStateBuffer.Add(statePayload, bufferIndex);
            tickToReplay++;
        }
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


    private void Move(Vector2 moveInput) {
        Vector3 moveDirection = transform.forward * moveInput.y + transform.right * moveInput.x;

        float moveSpeed = 200f;
        //transform.position += moveDirection * moveSpeed * networkTimer.minTimeBetweenTicks;
        body.velocity = moveDirection * moveSpeed * networkTimer.minTimeBetweenTicks;
    }


}
