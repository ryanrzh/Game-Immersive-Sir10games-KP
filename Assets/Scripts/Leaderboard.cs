using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game
{
    // Satu baris di leaderboard.
    // Yang disimpan adalah ID (bukan objek Plant/Facility/User), karena JsonUtility tidak bisa
    // menyimpan referensi ke asset ScriptableObject secara permanen.
    // Kategori leaderboard = plant + level. levelId "total" dipakai untuk total semua level.
    [System.Serializable]
    public class LeaderboardItem
    {
        [SerializeField] private string plantId;
        [SerializeField] private string levelId;
        [SerializeField] private string userName;
        [SerializeField] private float point;
        [SerializeField] private int star;

        public string PlantId => plantId;
        public string LevelId => levelId;
        public string UserName => userName;
        public float Point => point;
        public int Star => star;

        public LeaderboardItem() { }

        public LeaderboardItem(string plantId, string levelId, string userName, float point, int star)
        {
            this.plantId = plantId;
            this.levelId = levelId;
            this.userName = userName;
            this.point = point;
            this.star = star;
        }

        public void UpdateIfBetter(float newPoint, int newStar)
        {
            if (newPoint < point) return;
            point = newPoint;
            star = newStar;
        }
    }

    [System.Serializable]
    public class Leaderboard
    {
        public const string TotalCategory = "total";
        const string Key = "game_apd_leaderboard";

        [SerializeField] private List<LeaderboardItem> items = new List<LeaderboardItem>();
        public List<LeaderboardItem> Items => items;

        public static Leaderboard Load()
        {
            string json = PlayerPrefs.GetString(Key, "");
            var lb = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<Leaderboard>(json);
            return lb ?? new Leaderboard();
        }

        public void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        // Tambah atau perbarui skor pemain di satu kategori (skor terbaik yang disimpan).
        public void Add(string plantId, string levelId, string userName, float point, int star)
        {
            var item = items.FirstOrDefault(i => i.PlantId == plantId && i.LevelId == levelId && i.UserName == userName);
            if (item == null) items.Add(new LeaderboardItem(plantId, levelId, userName, point, star));
            else item.UpdateIfBetter(point, star);
            Save();
        }

        // Peringkat teratas untuk satu plant dan satu level (atau "total").
        public List<LeaderboardItem> GetTop(string plantId, string levelId, int count)
        {
            return items.Where(i => i.PlantId == plantId && i.LevelId == levelId)
                        .OrderByDescending(i => i.Point)
                        .Take(count)
                        .ToList();
        }
    }
}
