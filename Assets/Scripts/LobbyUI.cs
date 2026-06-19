using Mirror;
using UnityEngine;


public class LobbyUI : MonoBehaviour
{
    public void Host()
    {
        if (NetworkManager.singleton != null)
            NetworkManager.singleton.StartHost();
    }

    public void Join()
    {
        if (NetworkManager.singleton != null)
            NetworkManager.singleton.StartClient();
    }
}
