using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class DaClock : MonoBehaviour {

    float clientTime;

    float serverTime;



    void Start() {
        AskTime(DateTime.UtcNow);

    }
    async void AskTime(DateTime date) {
        await Task.Delay(1000);

        ServerCallbak(date, DateTime.UtcNow);
    }
    async void ServerCallbak(DateTime clientWas, DateTime serverWas) {
        await Task.Delay(1000);

        clientTime = (float)(serverWas - clientWas).TotalSeconds / 2;
    }


    void Update() {
        serverTime += Time.deltaTime;

        if (clientTime != 0) clientTime += Time.deltaTime;


        Debug.Log("serverTime: " + serverTime);
        Debug.Log("clientTime: " + clientTime);

    }
}
