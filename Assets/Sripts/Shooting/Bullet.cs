using System;
using Unity.Netcode;
using UnityEngine;

public class Bullet : MonoBehaviour {

    private DateTime spawnTime;
    private float ownerId;
    private float speed;
    private float damage;
    private float maxLiveTime = 3f;

    private void Start() {
        TickManager.OnTick += TickManager_OnTick;


        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, speed * (float)(DateTime.UtcNow - spawnTime).TotalSeconds)) {
            Debug.Log("Bullet Hit some shit: " + hit.point);


            DestroySelf();
        }
        Debug.DrawRay(transform.position, transform.forward * speed * (float)(DateTime.UtcNow - spawnTime).TotalSeconds, Color.yellow, 1);
        transform.position += transform.forward * speed * (float)(DateTime.UtcNow - spawnTime).TotalSeconds;
    }

    private void Update() {
        if (NetworkManager.Singleton.IsClient) transform.position += transform.forward * speed * Time.deltaTime;
    }
    private void TickManager_OnTick() {
        if ((DateTime.UtcNow - spawnTime).TotalSeconds > maxLiveTime) DestroySelf();
        else if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, speed * TickManager.deltaTick)) {
            Debug.Log("Bullet Hit some shit: " + hit.point);

            if (NetworkManager.Singleton.IsServer) {
                if (hit.collider.TryGetComponent(out Trooper trooper)) {
                    Debug.Log("Server sais: it hit you");
                }
            }
            
            DestroySelf();
        }

        if (NetworkManager.Singleton.IsServer) transform.position += transform.forward * speed * TickManager.deltaTick;
    }
    private void DestroySelf() {
        TickManager.OnTick -= TickManager_OnTick;

        Destroy(gameObject);
    }

    public struct BulletSetupArgs {
        public DateTime spawnTime;
        public ulong ownerId;

        public Vector3 position;
        public Quaternion rotation;
        public float speed;
        public int damage;
    }
    public void Setup(BulletSetupArgs args) {
        spawnTime = args.spawnTime;
        ownerId = args.ownerId;

        transform.position = args.position;
        transform.rotation = args.rotation;
        speed = args.speed;
        damage = args.damage;
    }

}
