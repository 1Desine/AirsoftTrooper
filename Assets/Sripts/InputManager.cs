using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour {
    static public InputManager Instance { get; private set; }


    private InputActions inputActions;

    static public Action OnJump = () => { };
    static public Action OnFireDown = () => { };
    static public Action OnFireUp = () => { };
    static public Action OnReloadDown = () => { };
    static public Action OnReloadUp = () => { };

    private void Awake() {
        Instance = this;

        inputActions = new InputActions();
        inputActions.Enable();

        inputActions.Trooper.Jump.started += _ => OnJump();
        inputActions.Trooper.Fire.started += _ => OnFireDown();
        inputActions.Trooper.Fire.canceled += _ => OnFireUp();
        inputActions.Trooper.Reload.started += _ => OnReloadDown();
        inputActions.Trooper.Reload.canceled += _ => OnReloadUp();
    }

    static public Vector2 MoveV2N() => Instance.inputActions.Trooper.MoveV2N.ReadValue<Vector2>();
    static public Vector2 LookV2D() => Instance.inputActions.Trooper.LookV2D.ReadValue<Vector2>();
    static public bool GetFireButton() => Instance.inputActions.Trooper.Fire.inProgress;
    static public bool GetReloadButton() => Instance.inputActions.Trooper.Reload.inProgress;


}
