using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static string prevSceneName { get; private set; }
    public static void LoadScene(string nameScene, bool isSave = true)
    {
        if (isSave) prevSceneName = SceneManager.GetActiveScene().name;
        SceneManager.LoadSceneAsync(nameScene);
    }

    public static void ReloadScene()
    {
        SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().name);
    }

    public static void BackToPreviousScene()
    {
        if (!string.IsNullOrEmpty(prevSceneName))
        {
            SceneManager.LoadSceneAsync(prevSceneName);
            return;
        }

        SceneManager.LoadSceneAsync("StartScreen");
    }
}
