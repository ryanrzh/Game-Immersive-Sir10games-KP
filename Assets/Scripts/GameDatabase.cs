using Game.APD;
using UnityEngine;

namespace Game
{
    // Satu asset induk yang menyimpan semuanya: daftar plant, daftar level, dan aturan skor.
    // Cukup pasang asset ini ke GameBoostrap.
    [CreateAssetMenu(fileName = "GameDatabase", menuName = "Game APD/Game Database")]
    public class GameDatabase : ScriptableObject
    {
        [SerializeField] private Plant[] plants = new Plant[0];
        [Tooltip("Urutkan: Level 1, 2, 3, 4. Level berikutnya terbuka jika level sebelumnya lulus.")]
        [SerializeField] private Level[] levels = new Level[0];
        [SerializeField] private ScoreRule scoreRule;

        public Plant[] Plants => plants;
        public Level[] Levels => levels;
        public ScoreRule ScoreRule => scoreRule;

        public Plant GetPlant(string id)
        {
            foreach (var p in plants) if (p != null && p.Id == id) return p;
            return null;
        }

        public int IndexOfLevel(Level level)
        {
            for (int i = 0; i < levels.Length; i++) if (levels[i] == level) return i;
            return -1;
        }
    }
}
