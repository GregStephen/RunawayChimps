using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadSceneOnStart : MonoBehaviour
{
    [SerializeField] private string sceneToLoad = "Loading";

    private void Start()
    {
        SceneManager.LoadScene(sceneToLoad, LoadSceneMode.Single);
    }
}
