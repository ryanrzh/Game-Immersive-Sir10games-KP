using Game;
using Game.APD;
using UnityEngine;

// Titik masuk game. Pasang script ini ke satu GameObject di scene pertama,
// lalu isi kolom Database dengan asset GameDatabase.
public class GameBoostrap : MonoBehaviour
{
    [SerializeField] private GameDatabase database;

    public static GameBoostrap Instance { get; private set; }

    public GameDatabase Database => database;
    public PlayerProgress Progress { get; private set; }
    public Leaderboard Ranking { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Progress = PlayerProgress.Load();
        Ranking = Leaderboard.Load();
    }

    // Panggil ini saat sebuah level selesai (dari Level 1 maupun Level 2-4).
    // Menyimpan skor terbaik, memperbarui leaderboard level, dan leaderboard total.
    public void FinishLevel(Plant plant, Level level, Score score)
    {
        Progress.Record(plant, level, score);
        string user = Progress.User.Name;

        var best = Progress.GetBest(plant.Id, level.Id);
        Ranking.Add(plant.Id, level.Id, user, best.point, best.star);
        Ranking.Add(plant.Id, Leaderboard.TotalCategory, user, Progress.TotalPoint(plant.Id), Progress.TotalStar(plant.Id));
    }
}
