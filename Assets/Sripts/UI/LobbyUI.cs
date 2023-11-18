using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class LobbyUI : MonoBehaviour {

    [SerializeField] Button StartServerButton;
    [SerializeField] Button StartHostButton;
    [SerializeField] Button StartClientButton;


    private void Awake() {
        StartServerButton.onClick.AddListener(() => {
            NetworkManager.Singleton.StartServer();
            Hide();
        });
        StartHostButton.onClick.AddListener(() => {
            NetworkManager.Singleton.StartHost();
            Hide();
        });
        StartClientButton.onClick.AddListener(() => {
            NetworkManager.Singleton.StartClient();
            Hide();
        });
    }



    private void Hide() {
        gameObject.SetActive(false);
    }

}
