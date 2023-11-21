using System;
using UnityEngine;

public class GunHandler : MonoBehaviour {

    public Action OnFireButtonDown = () => { };
    public Action OnFireButton = () => { };
    public Action OnFireButtonUp = () => { };
    public Action OnReloadButtonDown = () => { };
    public Action OnReloadButton = () => { };
    public Action OnReloadButtonUp = () => { };


    private void OnEnable() {
        InputManager.OnFireDown += InputManager_OnFireDown;
        InputManager.OnFireUp += InputManager_OnFireUp;
        InputManager.OnReloadDown += InputManager_OnReloadDown;
        InputManager.OnReloadUp += InputManager_OnReloadUp;
    }

    private void OnDisable() {
        InputManager.OnFireDown -= InputManager_OnFireDown;
        InputManager.OnFireUp -= InputManager_OnFireUp;
        InputManager.OnReloadDown -= InputManager_OnReloadDown;
        InputManager.OnReloadUp -= InputManager_OnReloadUp;
    }
    private void InputManager_OnFireDown() => OnFireButtonDown();
    private void InputManager_OnFireUp() => OnFireButtonUp();
    private void InputManager_OnReloadDown() => OnReloadButtonDown();
    private void InputManager_OnReloadUp() => OnReloadButtonUp();
    private void Update() {
        if (InputManager.GetFireButton()) OnFireButton();
        if (InputManager.GetReloadButton()) OnReloadButton(); // need logic for hold
    }


}
