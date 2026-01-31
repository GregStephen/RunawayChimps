using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadSceneOnStart : MonoBehaviour
{
    [SerializeField] private string sceneToLoad = "Loading";

    private void Awake()
    {
        SceneManager.LoadScene(sceneToLoad, LoadSceneMode.Single);
    }
}
