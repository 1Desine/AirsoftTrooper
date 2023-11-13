using UnityEngine;
using UnityEngine.UI;

public class LobbyUI : MonoBehaviour {

    [SerializeField] Button StartHostButton;
    [SerializeField] Button StartClientButton;


    private void Awake() {
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
