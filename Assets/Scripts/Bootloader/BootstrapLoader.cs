using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BootstrapLoader : MonoBehaviour
{
    [Header("Nazwa Sceny Menu")]
    // Wpisz tutaj DOK£ADN¥ nazwê swojej g³ównej sceny (np. "ExperimentScene")
    public string mainSceneName = "MainMenu";
    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        Debug.Log("£adowanie sceny menu...");
        SceneManager.LoadScene(mainSceneName);
    }
}
