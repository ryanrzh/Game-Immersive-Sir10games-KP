using System.Collections.Generic;
using System.Linq;
using Game.APD;
using UnityEngine;

namespace Game
{
    [System.Serializable]
    public class ProgressItem
    {
        public string plantId;
        public string levelId;
        public float point;
        public float maxPoint;
        public int star;
        public float percent;
    }

    [System.Serializable]
    class ProgressData
    {
        public string userName = "Pemain";
        public List<ProgressItem> items = new List<ProgressItem>();
    }

    // Menyimpan skor terbaik pemain per plant dan level (PlayerPrefs) dan menentukan level yang terbuka.
    public class PlayerProgress
    {
        const string Key = "game_apd_progress";
        ProgressData data = new ProgressData();

        public static PlayerProgress Load()
        {
            var p = new PlayerProgress();
            string json = PlayerPrefs.GetString(Key, "");
            if (!string.IsNullOrEmpty(json))
            {
                var loaded = JsonUtility.FromJson<ProgressData>(json);
                if (loaded != null) p.data = loaded;
            }
            return p;
        }

        void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public User User { get { return new User(data.userName, data.userName); } }

        public void SetUserName(string name)
        {
            data.userName = string.IsNullOrEmpty(name) || name.Trim().Length == 0 ? "Pemain" : name.Trim();
            Save();
        }

        public ProgressItem GetBest(string plantId, string levelId)
        {
            return data.items.FirstOrDefault(i => i.plantId == plantId && i.levelId == levelId);
        }

        // Simpan hanya jika lebih baik dari skor sebelumnya.
        public void Record(Plant plant, Level level, Score score)
        {
            var best = GetBest(plant.Id, level.Id);
            if (best == null)
            {
                best = new ProgressItem { plantId = plant.Id, levelId = level.Id };
                data.items.Add(best);
            }
            if (score.Point >= best.point)
            {
                best.point = score.Point;
                best.maxPoint = score.MaxPoint;
                best.star = score.Star;
                best.percent = score.Percent;
            }
            Save();
        }

        // Level pertama selalu terbuka. Level berikutnya terbuka jika level sebelumnya lulus.
        public bool IsLevelUnlocked(Plant plant, GameDatabase db, int levelIndex)
        {
            if (levelIndex <= 0) return true;
            var prev = GetBest(plant.Id, db.Levels[levelIndex - 1].Id);
            return prev != null && db.ScoreRule.IsPassed(prev.percent);
        }

        public float TotalPoint(string plantId) { return data.items.Where(i => i.plantId == plantId).Sum(i => i.point); }
        public int TotalStar(string plantId) { return data.items.Where(i => i.plantId == plantId).Sum(i => i.star); }
    }
}
