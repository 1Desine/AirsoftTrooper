using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour {
    static public InputManager Instance { get; private set; }


    private InputActions inputActions;

    public Action OnJump = () => { };
    public Action OnFireDown = () => { };
    public Action OnFireUp = () => { };
    public Action OnReloadDown = () => { };
    public Action OnReloadUp = () => { };

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

    public Vector2 MoveV2N() => inputActions.Trooper.MoveV2N.ReadValue<Vector2>();
    public Vector2 LookV2D() => inputActions.Trooper.LookV2D.ReadValue<Vector2>();
    public bool GetFireButton() => inputActions.Trooper.Fire.inProgress;
    public bool GetReloadButton() => inputActions.Trooper.Reload.inProgress;


}
