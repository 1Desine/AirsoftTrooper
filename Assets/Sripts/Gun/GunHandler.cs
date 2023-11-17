using System;
using UnityEngine;

public class GunHandler : MonoBehaviour {


    public Action OnFireButtonDown = () => { };
    public Action OnFireButton = () => { };
    public Action OnFireButtonUp = () => { };
    public Action OnReloadButtonDown = () => { };
    public Action OnReloadButton = () => { };


    private void OnEnable() {
        InputManager.Instance.OnFireDown += InputManager_OnFireDown;
        InputManager.Instance.OnFire += InputManager_OnFire;
        InputManager.Instance.OnFireUp += InputManager_OnFireUp;
        InputManager.Instance.OnReloadDown += InputManager_OnReloadDown;
        InputManager.Instance.OnReload += InputManager_OnReload;
    }

    private void OnDisable() {
        InputManager.Instance.OnFireDown -= InputManager_OnFireDown;
        InputManager.Instance.OnFire -= InputManager_OnFire;
        InputManager.Instance.OnFireUp -= InputManager_OnFireUp;
        InputManager.Instance.OnReloadDown -= InputManager_OnReloadDown;
        InputManager.Instance.OnReload -= InputManager_OnReload;
    }
    private void InputManager_OnFireDown() => OnFireButtonDown();
    private void InputManager_OnFire() => OnFireButton();
    private void InputManager_OnFireUp() => OnFireButtonUp();
    private void InputManager_OnReloadDown() => OnReloadButtonDown();
    private void InputManager_OnReload() => OnReloadButton();

}
