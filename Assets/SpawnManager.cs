using Unity.Netcode;
using UnityEngine;

public class SpawnManager : NetworkBehaviour {
    static public SpawnManager Instance { get; private set; }

    [SerializeField] TickManager tickManager;
    [SerializeField] Trooper trooper;
    [SerializeField] Bullet bullet;


    private void Awake() {
        Instance = this;

        DontDestroyOnLoad(gameObject);

        NetworkManager.Singleton.OnServerStarted += Singleton_OnServerStarted;
    }
    private void Singleton_OnServerStarted() {
       Instantiate(tickManager).GetComponent<NetworkObject>().Spawn();
    }

    static public void SpawnTrooper(ulong ownerId) {
        Instantiate(Instance.trooper);
    }

    static public void SpawnBullet(Bullet.BulletSetupArgs args) {
        Bullet newBullet = Instantiate(Instance.bullet);
        newBullet.Setup(args);
        Debug.Log("Bullet spawned at position: " + newBullet.transform.position);
    }



}
