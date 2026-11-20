
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game
{
    public class GameManager : MonoBehaviour
    {
        public List<User> UserList { private set; get; }

        public User GetUser(string id)
        {
            return UserList.Find(u => u.Id == id);
        }

        public void AddUser(User user)
        {
            UserList.Add(user);
        }

        public Leaderboard Leaderboard { private set; get; }

        public static void LoadScene(string nameScene, LoadSceneMode mode = LoadSceneMode.Single)
        {
            SceneManager.LoadSceneAsync(nameScene, mode);
        }

        public static void ReloadScene()
        {
            SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().name);
        }

        public static void UnloadScene(string nameScene)
        {
            SceneManager.UnloadSceneAsync(nameScene);
        }
    }
}
