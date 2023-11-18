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
        InputManager.Instance.OnFireDown += InputManager_OnFireDown;
        InputManager.Instance.OnFireUp += InputManager_OnFireUp;
        InputManager.Instance.OnReloadDown += InputManager_OnReloadDown;
        InputManager.Instance.OnReloadUp += InputManager_OnReloadUp;
    }

    private void OnDisable() {
        InputManager.Instance.OnFireDown -= InputManager_OnFireDown;
        InputManager.Instance.OnFireUp -= InputManager_OnFireUp;
        InputManager.Instance.OnReloadDown -= InputManager_OnReloadDown;
        InputManager.Instance.OnReloadUp -= InputManager_OnReloadUp;
    }
    private void InputManager_OnFireDown() => OnFireButtonDown();
    private void InputManager_OnFireUp() => OnFireButtonUp();
    private void InputManager_OnReloadDown() => OnReloadButtonDown();
    private void InputManager_OnReloadUp() => OnReloadButtonUp();
    private void Update() {
        if (InputManager.Instance.GetFireButton()) OnFireButton();
        if (InputManager.Instance.GetReloadButton()) OnReloadButton(); // need logic for hold
    }


}
