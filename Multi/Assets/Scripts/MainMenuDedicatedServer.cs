using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public class MainMenuDedicatedServer : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
#if DEDICATED_SERVER
    Debug.Log("DEDICATED_SERVER 5.2");
    NetworkManager.Singleton.SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
#endif
    }
}
