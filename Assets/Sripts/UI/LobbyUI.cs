using UnityEngine;
using UnityEngine.UI;

public class LobbyUI : MonoBehaviour {

    [SerializeField] Button StartServerButton;
    [SerializeField] Button StartHostButton;
    [SerializeField] Button StartClientButton;


    private void Awake() {
        StartServerButton.onClick.AddListener(() => {
            MyNetworkManager.Singleton.StartServer();
            Hide();
        });
        StartHostButton.onClick.AddListener(() => {
            MyNetworkManager.Singleton.StartHost();
            Hide();
        });
        StartClientButton.onClick.AddListener(() => {
            MyNetworkManager.Singleton.StartClient();
            Hide();
        });
    }



    private void Hide() {
        gameObject.SetActive(false);
    }

}
