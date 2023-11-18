using Unity.Netcode;
using UnityEngine;

public class SpawnManager : NetworkBehaviour {
    static public SpawnManager instance { get; private set; }

    [SerializeField] Transform tickManager;


    private void Awake() {
        instance = this;

        DontDestroyOnLoad(gameObject);

        NetworkManager.Singleton.OnServerStarted += Singleton_OnServerStarted;
    }
    private void Singleton_OnServerStarted() {
        Transform newTickManager = Instantiate(tickManager);
        newTickManager.GetComponent<NetworkObject>().Spawn();
    }


}
