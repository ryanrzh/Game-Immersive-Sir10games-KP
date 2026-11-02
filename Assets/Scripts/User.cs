using UnityEngine;

namespace Game
{
    [System.Serializable]
    public class User
    {
        [SerializeField] private string id;
        [SerializeField] private string name;
        [SerializeField] private Sprite avatar;

        public string Id => id;
        public string Name => name;
        public Sprite Avatar => avatar;

        public User() { }

        public User(string id, string name)
        {
            this.id = id;
            this.name = name;
        }
    }
}
