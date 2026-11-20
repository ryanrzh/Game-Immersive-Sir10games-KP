
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game
{
    [Serializable]
    public class LeaderboardItem
    {
        [SerializeField] private string plantId;
        [SerializeField] private string levelId;
        [SerializeField] private string userId;

        // Field lama dipertahankan untuk kompatibilitas data.
        [SerializeField] private float point;
        [SerializeField] private int star;

        // Statistik baru.
        [SerializeField] private int playCount;
        [SerializeField] private float totalScore;
        [SerializeField] private float bestScore;
        [SerializeField] private float totalAccuracy;
        [SerializeField] private int accuracyCount;

        public string PlantId => plantId;
        public string LevelId => levelId;
        public string UserId => userId;

        // Point sekarang berarti total skor.
        public float Point => TotalScore;
        public int Star => star;

        public int PlayCount => playCount;
        public float TotalScore => totalScore;
        public float BestScore => bestScore;

        public float AverageScore =>
            playCount > 0 ? totalScore / playCount : 0f;

        public float AverageAccuracy =>
            accuracyCount > 0
                ? totalAccuracy / accuracyCount
                : 0f;

        public bool HasAccuracy => accuracyCount > 0;

        public float TotalAccuracy => totalAccuracy;
        public int AccuracyCount => accuracyCount;

        public LeaderboardItem() { }

        public LeaderboardItem(
            string plantId,
            string levelId,
            string userId)
        {
            this.plantId = plantId;
            this.levelId = levelId;
            this.userId = userId;
        }

        // Konversi data dari leaderboard versi lama.
        public void MigrateLegacyData()
        {
            if (playCount > 0) return;

            // Data lama hanya menyimpan skor terbaik.
            playCount = 1;
            totalScore = point;
            bestScore = point;

            // Akurasi lama tidak diketahui.
            totalAccuracy = 0f;
            accuracyCount = 0;
        }

        // Dipanggil setiap pemain selesai bermain.
        public void AddPlay(
            float score,
            int newStar,
            float? accuracy)
        {
            score = Mathf.Max(0f, score);

            playCount++;
            totalScore += score;

            if (playCount == 1 || score > bestScore)
            {
                bestScore = score;
                star = newStar;
            }

            if (accuracy.HasValue)
            {
                totalAccuracy +=
                    Mathf.Clamp(accuracy.Value, 0f, 100f);

                accuracyCount++;
            }

            point = totalScore;
        }

        // Membuat hasil penggabungan beberapa plant/level.
        public static LeaderboardItem CreateSummary(
            string plantId,
            string levelId,
            string userId,
            IEnumerable<LeaderboardItem> records)
        {
            var result = new LeaderboardItem(
                plantId, levelId, userId);

            var data = records.ToList();

            result.playCount = data.Sum(x => x.PlayCount);
            result.totalScore = data.Sum(x => x.TotalScore);
            result.bestScore = data.Max(x => x.BestScore);

            result.totalAccuracy =
                data.Sum(x => x.TotalAccuracy);

            result.accuracyCount =
                data.Sum(x => x.AccuracyCount);

            result.star = data.Sum(x => x.Star);
            result.point = result.totalScore;

            return result;
        }
    }

    [Serializable]
    public class Leaderboard
    {
        public const string TotalCategory = "total";

        private const string Key = "game_apd_leaderboard";

        [SerializeField]
        private List<LeaderboardItem> items =
            new List<LeaderboardItem>();

        public List<LeaderboardItem> Items => items;

        public static Leaderboard Load(string key = Key)
        {
            string json = PlayerPrefs.GetString(key, "");

            Leaderboard lb = string.IsNullOrEmpty(json)
                ? null
                : JsonUtility.FromJson<Leaderboard>(json);

            if (lb == null)
                lb = new Leaderboard();

            if (lb.items == null)
                lb.items = new List<LeaderboardItem>();

            // Migrasi skor lama menjadi satu catatan main.
            foreach (var item in lb.items)
            {
                item.MigrateLegacyData();
            }

            return lb;
        }

        public void Save(string key = Key)
        {
            PlayerPrefs.SetString(
                key, JsonUtility.ToJson(this));

            PlayerPrefs.Save();
        }

        // Versi baru: dengan persentase akurasi (0-100).
        public void Add(
            string plantId,
            string levelId,
            string userId,
            float score,
            int star,
            float accuracy)
        {
            AddInternal(
                plantId, levelId, userId,
                score, star, accuracy);
        }

        // Kompatibilitas pemanggilan kode lama.
        // Akurasi tidak dicatat karena tidak tersedia.
        public void Add(
            string plantId,
            string levelId,
            string userId,
            float score,
            int star)
        {
            AddInternal(
                plantId, levelId, userId,
                score, star, null);
        }

        private void AddInternal(
            string plantId,
            string levelId,
            string userId,
            float score,
            int star,
            float? accuracy)
        {
            if (string.IsNullOrEmpty(plantId) ||
                string.IsNullOrEmpty(levelId) ||
                string.IsNullOrEmpty(userId))
            {
                Debug.LogWarning(
                    "Leaderboard: ID tidak boleh kosong.");
                return;
            }

            // "total" adalah kategori hasil agregasi,
            // bukan kategori untuk menyimpan skor.
            if (plantId == TotalCategory ||
                levelId == TotalCategory)
            {
                Debug.LogWarning(
                    "Leaderboard: Simpan skor pada " +
                    "plant dan level asli, bukan total.");
                return;
            }

            var item = items.FirstOrDefault(i =>
                i.PlantId == plantId &&
                i.LevelId == levelId &&
                i.UserId == userId);

            if (item == null)
            {
                item = new LeaderboardItem(
                    plantId, levelId, userId);

                items.Add(item);
            }

            item.AddPlay(score, star, accuracy);

            Save();
        }

        // Leaderboard per plant, level, atau keseluruhan.
        public List<LeaderboardItem> GetTop(
            string plantId,
            string levelId,
            int count)
        {
            if (count <= 0)
                return new List<LeaderboardItem>();

            var query = items.Where(i =>
                i.PlantId != TotalCategory &&
                i.LevelId != TotalCategory);

            if (plantId != TotalCategory)
            {
                query = query.Where(i =>
                    i.PlantId == plantId);
            }

            if (levelId != TotalCategory)
            {
                query = query.Where(i =>
                    i.LevelId == levelId);
            }

            return query
                .GroupBy(i => i.UserId)
                .Select(g => LeaderboardItem.CreateSummary(
                    plantId, levelId, g.Key, g))
                .OrderByDescending(i => i.TotalScore)
                .ThenByDescending(i => i.AverageAccuracy)
                .ThenByDescending(i => i.BestScore)
                .ThenBy(i => i.UserId)
                .Take(count)
                .ToList();
        }

        // Mengambil statistik seorang pemain.
        public LeaderboardItem GetPlayerStats(
            string plantId,
            string levelId,
            string userId)
        {
            return GetTop(plantId, levelId, int.MaxValue)
                .FirstOrDefault(i => i.UserId == userId);
        }
    }
}
