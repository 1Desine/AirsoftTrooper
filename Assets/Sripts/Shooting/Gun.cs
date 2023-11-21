using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;


public class Gun : NetworkBehaviour {
    // reference https://youtu.be/-lGsuCEWkM0?si=gNsTcQw-mxeJTqac

    // Network variables should be value objects
    protected struct InputPayload : INetworkSerializable {
        public int tick;
        public DateTime timestamp;
        public ulong networkObjectId;

        public Vector3 barrelPosition;
        public Quaternion barrelRotation;
        public bool wannaShoot;
        public bool wannaReload;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
            serializer.SerializeValue(ref tick);
            serializer.SerializeValue(ref timestamp);
            serializer.SerializeValue(ref networkObjectId);

            serializer.SerializeValue(ref barrelPosition);
            serializer.SerializeValue(ref barrelRotation);
            serializer.SerializeValue(ref wannaShoot);
            serializer.SerializeValue(ref wannaReload);
        }
    }
    private struct StatePayload : INetworkSerializable {
        public int tick;
        public ulong networkObjectId;

        public int ammoLoaded;
        public bool isChambered;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
            serializer.SerializeValue(ref tick);
            serializer.SerializeValue(ref networkObjectId);

            serializer.SerializeValue(ref ammoLoaded);
            serializer.SerializeValue(ref isChambered);
        }
    }

    [SerializeField] Transform barrelEnd;

    [Header("Positioning")]
    [SerializeField] Transform holdPoint;
    [SerializeField] Transform scopePoint;

    [Header("Gun properties")]
    [SerializeField] int rpm;
    [SerializeField] int bulletsPerShot;
    private float lastTimeShot;
    [SerializeField] List<FireMode> fireModesAwailible;
    private FireMode fireMode = FireMode.None;
    private enum FireMode {
        Auto,
        Semi,
        Burst,
        Single,
        None,
    }
    [SerializeField]
    List<FireMode> preferedFireModeChoice = new List<FireMode>() {
        FireMode.Auto,
        FireMode.Semi,
        FireMode.Burst,
        FireMode.Single,
    };
    [SerializeField] int burstShotsAmount;

    [SerializeField] int clipSize;
    private int ammoLoaded;
    private bool isChambered;

    [SerializeField] float reloadTime;
    private int startReloadTime;
    [SerializeField] bool keepReloading;

    [Header("Bullet")]
    [SerializeField] float bulletSpeed;
    [SerializeField] int bulletDamage;

    [Header("Settings")]
    [SerializeField] AnimationCurve gravityResistance;






    // Netcode general
    const int bufferSize = 1024;

    // Network client specific
    CircularBuffer<StatePayload> clientStateBuffer;
    CircularBuffer<InputPayload> clientInputBuffer;
    StatePayload lastServerState;
    StatePayload lastProcessedState;

    // Netcode server specific
    [Header("Netcode")]





    GunHandler gunHandler;
    private void Awake() {
        // Network
        clientStateBuffer = new CircularBuffer<StatePayload>(bufferSize);
        clientInputBuffer = new CircularBuffer<InputPayload>(bufferSize);

        gunHandler = transform.parent.GetComponent<GunHandler>();


        // choose best option for this gun
        foreach (var fireModePrefered in preferedFireModeChoice) {
            bool foundBestOption = false;
            foreach (var fireModeAvailible in fireModesAwailible) {
                if (fireModeAvailible == fireModePrefered) {
                    foundBestOption = true;
                    fireMode = fireModePrefered;
                    break;
                }
            }
            if (foundBestOption) break;
        }
    }
    private void OnEnable() {
        gunHandler.OnFireButtonDown += GunHandler_OnShootButtonDown;
        gunHandler.OnFireButton += GunHandler_OnShootButton;
        gunHandler.OnFireButtonUp += GunHandler_OnShootButtonUp;
        gunHandler.OnReloadButtonDown += GunHandler_OnReloadButtonDown;
        gunHandler.OnReloadButton += GunHandler_OnReloadButton;
        gunHandler.OnReloadButtonUp += GunHandler_OnReloadButtonUp;
    }
    private void OnDisable() {
        gunHandler.OnFireButtonDown -= GunHandler_OnShootButtonDown;
        gunHandler.OnFireButton -= GunHandler_OnShootButton;
        gunHandler.OnFireButtonUp -= GunHandler_OnShootButtonUp;
        gunHandler.OnReloadButtonDown -= GunHandler_OnReloadButtonDown;
        gunHandler.OnReloadButton -= GunHandler_OnReloadButton;
        gunHandler.OnReloadButtonUp -= GunHandler_OnReloadButtonUp;
    }
    virtual protected void GunHandler_OnShootButtonDown() {
    }
    virtual protected void GunHandler_OnShootButton() {
        HandleClientInput(new InputPayload {
            tick = TickManager.CurrentTick,
            timestamp = DateTime.UtcNow,
            networkObjectId = NetworkObjectId,

            barrelPosition = barrelEnd.position,
            barrelRotation = barrelEnd.rotation,
            wannaShoot = true,
        });
    }
    virtual protected void GunHandler_OnShootButtonUp() { }
    virtual protected void GunHandler_OnReloadButtonDown() => Debug.LogError("me. Default GunHandler_OnReloadButtonDown", this);
    virtual protected void GunHandler_OnReloadButton() => Debug.LogError("me. Default GunHandler_OnReloadButton", this);
    virtual protected void GunHandler_OnReloadButtonUp() => Debug.LogError("me. Default GunHandler_OnReloadButtonUp", this);




    [ClientRpc]
    private void SendToClientRpc(StatePayload statePayload) {
        if (!IsOwner) return;
        lastServerState = statePayload;
    }
    private void HandleClientInput(InputPayload inputPayload) {
        if (!IsClient || !IsOwner) return;
        HandleServerReconciliation();

        var currentTick = inputPayload.tick;
        var bufferIndex = currentTick % bufferSize;

        StatePayload statePayload = ProcessInput(inputPayload);
        clientStateBuffer.Add(statePayload, bufferIndex);
        clientInputBuffer.Add(inputPayload, bufferIndex);
        SendToServerRpc(inputPayload);
    }
    [ServerRpc]
    private void SendToServerRpc(InputPayload input) {
        // Server computes what player said and sends the result
        SendToClientRpc(ProcessInput(input));
    }
    private bool ShouldReconcile() {
        bool isNewServerState = !lastServerState.Equals(default);
        bool isLastStateUndefinedOrDifferent = lastProcessedState.Equals(default)
                                           || !lastProcessedState.Equals(lastServerState);

        return isNewServerState && isLastStateUndefinedOrDifferent;
    }
    private void HandleServerReconciliation() {
        if (!ShouldReconcile()) return;

        int bufferIndex = lastServerState.tick % bufferSize;
        if (bufferIndex - 1 < 0) return; // Not enough informaion to reconcile


        StatePayload clientState = clientStateBuffer.Get(bufferIndex);
        bool needToReconcile =
               lastServerState.ammoLoaded != clientState.ammoLoaded
            || lastServerState.isChambered != clientState.isChambered;

        //Debug.Log("Server: " + lastServerState.position);
        //Debug.Log("Client: " + clientStateBuffer.Get(bufferIndex).position);
        //Debug.Log("The margin: " + positionError);

        if (needToReconcile) {
            Debug.Log("Gun Reconcile", this);
            ReconcileState();
        }

        lastProcessedState = lastServerState;
    }
    private void ReconcileState() {
        ammoLoaded = lastServerState.ammoLoaded;
        isChambered = lastServerState.isChambered;

        clientStateBuffer.Add(lastServerState, lastServerState.tick);
    }
    private StatePayload ProcessInput(InputPayload input) {
        // if it was more then .. seconds sinse this input - don't do it
        if ((DateTime.UtcNow - input.timestamp).TotalSeconds < 1) {
            if (input.wannaShoot) Shoot(new ShootArgs {
                timestamp = input.timestamp,
                ownerId = input.networkObjectId,
                position = input.barrelPosition,
                rotation = input.barrelRotation,
            });
            if (input.wannaReload) Reload();
            Debug.Log("input.timestamp: " + input.timestamp);
        }

        return new StatePayload {
            tick = TickManager.CurrentTick,
            networkObjectId = NetworkObjectId,

            ammoLoaded = ammoLoaded,
            isChambered = isChambered,
        };
    }


    protected struct ShootArgs {
        public DateTime timestamp;
        public ulong ownerId;
        public Vector3 position;
        public Quaternion rotation;
    }
    virtual protected bool CanShoot() {
        bool rpmCooldown = Time.time - lastTimeShot > 1f / (rpm / 60f);
        return rpmCooldown;
    }
    virtual protected void Shoot(ShootArgs args) {
        if (!CanShoot()) return;

        lastTimeShot = Time.time;

        SpawnManager.SpawnBullet(new Bullet.BulletSetupArgs {
            spawnTime = args.timestamp,
            ownerId = 0, // Need to be changed

            position = args.position,
            rotation = args.rotation,
            speed = bulletSpeed,
            damage = bulletDamage,
        });
    }

    virtual protected bool CanReload() {
        Debug.LogError("me. Delault CanReload", this);
        return true;
    }
    virtual protected void Reload() {
        if (!CanShoot()) return;


    }




}
