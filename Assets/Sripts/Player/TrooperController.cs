using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using Unity.Multiplayer.Samples.Utilities.ClientAuthority;
using Unity.Netcode;
using UnityEngine;
using Utilities;


public class TrooperController : NetworkBehaviour {
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




    [SerializeField] Transform head;
    [SerializeField] float moveSpeed;
    Vector2 lookVector;

    Rigidbody body;
    new CapsuleCollider collider;

    // Netcode general
    const int bufferSize = 1024;


    // Network client specific
    CircularBuffer<StatePayload> clientStateBuffer;
    CircularBuffer<InputPayload> clientInputBuffer;
    StatePayload lastServerState;
    StatePayload lastProcessedState;
    CountdownTimer reconceliationTimer;

    // Netcode server specific
    CircularBuffer<StatePayload> serverStateBuffer;
    Queue<InputPayload> serverInputQueue;
    [Header("Netcode")]
    [SerializeField] float reconceliationThreshold = 0.1f;
    [SerializeField] float reconceliationCooldownTime = 1f;
    [SerializeField] Transform serverSphere;
    [SerializeField] Transform clientSphere;





    private void Awake() {
        Cursor.lockState = CursorLockMode.Locked;


        body = GetComponent<Rigidbody>();
        collider = GetComponent<CapsuleCollider>();


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
    }
    private void Update() {
        if (IsOwner) {
            if (Input.GetKeyDown(KeyCode.Q)) transform.position -= transform.forward * 10;
            if (Input.GetKeyDown(KeyCode.E)) transform.position += transform.forward * 10;
        }

        reconceliationTimer.Tick(Time.deltaTime);

        HandleLook();
    }
    private void OnEnable() {
        TickManager.OnTick += HandleTick;
    }
    private void OnDisable() {
        TickManager.OnTick -= HandleTick;
    }
    private void HandleTick() {
        HandleClientTick();
        HandleServerTick();
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
            StatePayload statePayload = ProcessInput(inputPayLoad);
            serverStateBuffer.Add(statePayload, bufferIndex);
        }

        if (bufferIndex == -1) return;
        SendToClientRpc(serverStateBuffer.Get(bufferIndex));

        serverSphere.position = serverStateBuffer.Get(bufferIndex).position; // DEBUG SPHERE
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

        var currentTick = TickManager.CurrentTick;
        var bufferIndex = currentTick % bufferSize;

        InputPayload inputPayload = new InputPayload() {
            tick = currentTick,
            timestamp = DateTime.UtcNow,
            networkObjectId = NetworkObjectId,
            position = transform.position,

            inputVector = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized,
        };

        SendToServerRpc(inputPayload);
        StatePayload statePayload = ProcessInput(inputPayload);
        clientStateBuffer.Add(statePayload, bufferIndex);
        clientInputBuffer.Add(inputPayload, bufferIndex);

        clientSphere.position = transform.position; // DEBUG SPHERE
    }
    private StatePayload ProcessInput(InputPayload input) {
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

        //Debug.Log("Server: " + lastServerState.position);
        //Debug.Log("Client: " + clientStateBuffer.Get(bufferIndex).position);
        //Debug.Log("The margin: " + positionError);

        if (positionError > reconceliationThreshold) {
            Debug.Log("TrooperController Reconcile, diviation is: " + positionError);
            ReconcileState();
            reconceliationTimer.Start();
        }

        lastProcessedState = lastServerState;
    }
    private void ReconcileState() {
        transform.position = lastServerState.position;
        body.velocity = lastServerState.velocity;

        clientStateBuffer.Add(lastServerState, lastServerState.tick);

        // Replay all inputs from the rewind state to the current state
        int tickToReplay = lastServerState.tick;

        while (tickToReplay < TickManager.CurrentTick) {
            int bufferIndex = tickToReplay % bufferSize;
            StatePayload recalculatedClientPayload = ProcessInput(clientInputBuffer.Get(bufferIndex));
            clientStateBuffer.Add(recalculatedClientPayload, bufferIndex);
            tickToReplay++;
        }
    }

    private void Move(Vector2 moveInput) {
        Vector3 moveDirection = transform.forward * moveInput.y + transform.right * moveInput.x;

        transform.position += moveDirection * moveSpeed * TickManager.deltaTick;
        //body.velocity += moveDirection * moveSpeed * TickManager.deltaTick;
    }
    private void HandleLook() {
        lookVector += InputManager.LookV2D() * 0.05f;
        lookVector.y = Mathf.Clamp(lookVector.y, -90, 90);
        transform.eulerAngles = Vector3.up * lookVector.x;
        head.localEulerAngles = Vector3.right * -lookVector.y;
    }


}
