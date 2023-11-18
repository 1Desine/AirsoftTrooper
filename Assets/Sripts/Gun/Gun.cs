using System.Collections.Generic;
using UnityEngine;

public class Gun : MonoBehaviour {


    [Header("Positioning")]
    [SerializeField] Transform holdPoint;
    [SerializeField] Transform scopePoint;

    [Header("Gun properties")]
    [SerializeField] int fireRate;
    [SerializeField] int burstFireRate;
    [SerializeField] int bulletsPerShot;
    float lastTimeShot;
    [SerializeField] List<FireMode> fireModesAwailible;
    FireMode fireMode = FireMode.None;
    private enum FireMode {
        Auto,
        Semi,
        Burst,
        Single,
        None,
    }
    List<FireMode> preferedFireModeChoice = new List<FireMode>() {
        FireMode.Auto,
        FireMode.Semi,
        FireMode.Burst,
        FireMode.Single,
    };
    [SerializeField] int burstShotsAmount;

    [SerializeField] int clipSize;
    private int ammoLoaded;
    private bool isChambered;

    [SerializeField] float reloadTime;
    [SerializeField] bool keepReloading;




    GunHandler gunHandler;
    private void Awake() {
        gunHandler = transform.parent.GetComponent<GunHandler>();


        // choose best option for this gun
        foreach (var fireModePrefered in preferedFireModeChoice) {
            bool foundBestOption = false;
            foreach (var fireModeAvailible in fireModesAwailible) {
                if (fireModeAvailible == fireModePrefered) {
                    foundBestOption = true;
                    fireMode = fireModePrefered;
                    break;
                }
            }
            if (foundBestOption) break;
        }
    }
    private void OnEnable() {
        gunHandler.OnFireButtonDown += GunHandler_OnShootButtonDown;
        gunHandler.OnFireButton += GunHandler_OnShootButton;
        gunHandler.OnFireButtonUp += GunHandler_OnShootButtonUp;
        gunHandler.OnReloadButtonDown += GunHandler_OnReloadButtonDown;
        gunHandler.OnReloadButton += GunHandler_OnReloadButton;
        gunHandler.OnReloadButtonUp += GunHandler_OnReloadButtonUp;
    }
    private void OnDisable() {
        gunHandler.OnFireButtonDown -= GunHandler_OnShootButtonDown;
        gunHandler.OnFireButton -= GunHandler_OnShootButton;
        gunHandler.OnFireButtonUp -= GunHandler_OnShootButtonUp;
        gunHandler.OnReloadButtonDown -= GunHandler_OnReloadButtonDown;
        gunHandler.OnReloadButton -= GunHandler_OnReloadButton;
        gunHandler.OnReloadButtonUp -= GunHandler_OnReloadButtonUp;
    }
    virtual protected void GunHandler_OnShootButtonDown() => Debug.LogError("me. Default GunHandler_OnShootButtonDown", this);
    virtual protected void GunHandler_OnShootButton() => Debug.LogError("me. Default GunHandler_OnShootButton", this);
    virtual protected void GunHandler_OnShootButtonUp() => Debug.LogError("me. Default GunHandler_OnShootButtonUp", this);
    virtual protected void GunHandler_OnReloadButtonDown() => Debug.LogError("me. Default GunHandler_OnReloadButtonDown", this);
    virtual protected void GunHandler_OnReloadButton() => Debug.LogError("me. Default GunHandler_OnReloadButton", this);
    virtual protected void GunHandler_OnReloadButtonUp() => Debug.LogError("me. Default GunHandler_OnReloadButtonUp", this);


}
