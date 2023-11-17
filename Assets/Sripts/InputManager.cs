using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour {
    static public InputManager Instance { get; private set; }


    private InputActions inputActions;

    public Action OnJump = () => { };
    public Action OnFireDown = () => { };
    public Action OnFire = () => { };
    public Action OnFireUp = () => { };
    public Action OnReloadDown = () => { };
    public Action OnReload = () => { };


    private void Awake() {
        Instance = this;

        inputActions = new InputActions();
        inputActions.Enable();

        inputActions.Trooper.Jump.started += context => OnJump();
        inputActions.Trooper.Fire.started += context => OnFireDown();
        inputActions.Trooper.Fire.canceled += context => OnFireUp();
        inputActions.Trooper.Reload.started += context => OnReloadDown();

    }
    private void Update() {
        if (inputActions.Trooper.Fire.inProgress) OnFire();
        if (inputActions.Trooper.Reload.inProgress) OnReload();
    }


    public Vector2 MoveV2N() => inputActions.Trooper.MoveV2N.ReadValue<Vector2>();
    public Vector2 LookV2D() => inputActions.Trooper.LookV2D.ReadValue<Vector2>();






}
