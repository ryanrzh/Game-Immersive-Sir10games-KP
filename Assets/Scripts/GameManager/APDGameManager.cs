using UnityEngine;

namespace Game.APD
{
    public class APDGameManager : GameManager
    {
        public static APDGameManager Instance { get; private set; }

        // --- TAMBAHAN VARIABEL GLOBAL ---
        public Leaderboard LeaderboardData { get; private set; }
        public string CurrentUserId { get; set; } = "USER_SAAT_INI"; // Nanti diisi dari sistem Login

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                // Load data leaderboard satu kali saja saat game pertama kali jalan
                LeaderboardData = Leaderboard.Load();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public Plant PlantSelectedData { get; private set; }
        public Facility FacilitySelectedData { get; private set; }
        [SerializeField] private GameDatabase db;

        public string LevelSelectedData { get; set; } = "Level 1";

        public GameDatabase Database => db;

        public void SetPlantSelectedData(Plant plant)
        {
            PlantSelectedData = plant;
        }

        public Facility GenerateRandomPlant()
        {
            return PlantSelectedData.Facilities[Random.Range(0, PlantSelectedData.Facilities.Length)];
        }

        // --- TAMBAHAN FUNGSI SAVE SKOR ---
        // Panggil fungsi ini di akhir Modul (misal di dalam ShowResult)
        public void SavePlayerScore(string levelId, float score, int stars, float accuracy)
        {
            if (PlantSelectedData != null && LeaderboardData != null)
            {
                LeaderboardData.Add(PlantSelectedData.Id, levelId, CurrentUserId, score, stars, accuracy);
                Debug.Log($"Skor disimpan! User: {CurrentUserId}, Plant: {PlantSelectedData.Id}, Level: {levelId}, Skor: {score}");
            }
        }

        public void StartGame(int level)
        {
            switch (level)
            {
                case 1: StartGameLevel1(); break;
                case 2: StartGameLevel2(); break;
                case 3: StartGameLevel3(); break;
                case 4: StartGameLevel4(); break;
                default: Debug.LogWarning("Level tidak dikenali: " + level); break;
            }
        }

        private void StartGameLevel1() { FacilitySelectedData = GenerateRandomPlant(); LoadScene("Level 1 Start Menu"); }
        private void StartGameLevel2() { FacilitySelectedData = GenerateRandomPlant(); LoadScene("Level 2 Game Menu"); }
        private void StartGameLevel3() { FacilitySelectedData = GenerateRandomPlant(); LoadScene("Level 3 Game Menu"); }
        private void StartGameLevel4() { FacilitySelectedData = GenerateRandomPlant(); LoadScene("Level 4 Game Menu"); }
    }
}