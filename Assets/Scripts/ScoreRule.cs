using UnityEngine;

namespace Game
{
    // Semua angka poin dan bintang ada di sini, jadi mudah diubah dari Inspector.
    [CreateAssetMenu(fileName = "ScoreRule", menuName = "Game APD/Aturan Skor")]
    public class ScoreRule : ScriptableObject
    {
        [Header("Level 1")]
        public int identifyFacilityFound = 10;   // titik benar ditemukan
        public int identifyHazardCorrect = 10;   // tiap kategori bahaya benar
        public int identifyWrongTap = -5;        // salah ketuk
        public int identifyHazardWrong = -5;     // tiap kategori bahaya salah

        [Header("Level 2, 3, 4 (per ronde)")]
        public int roundFirstTry = 20;           // benar di percobaan 1
        public int roundSecondTry = 10;          // benar di percobaan 2
        public int roundFail = 0;                // salah semua
        public int maxAttempts = 2;

        [Header("Lulus dan bintang (persen)")]
        public float passPercent = 70f;
        public float star3Percent = 90f;
        public float star2Percent = 70f;
        public float star1Percent = 40f;

        public int RoundPoint(int attempt)
        {
            if (attempt <= 1) return roundFirstTry;
            if (attempt == 2) return roundSecondTry;
            return roundFail;
        }

        public int GetStar(float percent)
        {
            if (percent >= star3Percent) return 3;
            if (percent >= star2Percent) return 2;
            if (percent >= star1Percent) return 1;
            return 0;
        }

        public bool IsPassed(float percent) { return percent >= passPercent; }
    }
}
