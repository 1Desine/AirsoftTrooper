using Unity.Netcode;
using UnityEngine;

public class Trooper : NetworkBehaviour {


    private int health = 100;



    public void Damage(int damage) {
        health -= damage;
    }




}
