using UnityEngine;

namespace Game.APD
{
    public class APDGameManager : GameManager
    {
        public static APDGameManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public Plant PlantSelectedData { get; private set; }

        public void SetPlantSelectedData(Plant plant)
        {
            PlantSelectedData = plant;
        }
    }
}
