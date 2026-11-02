using System;
using System.Collections.Generic;
using System.Linq;
using Game;
using Game.APD;
using UnityEngine;

// Contoh pemakaian logika game, dijalankan otomatis dan hasilnya tampil di Console.
// Berguna untuk memahami alur dan mengecek bahwa aturan skor sudah benar.
// Pasang di GameObject yang sama dengan GameBoostrap. Hapus saja jika sudah punya UI.
public class ConsoleDemo : MonoBehaviour
{
    [SerializeField] private bool runOnStart = true;

    void Start()
    {
        if (runOnStart) Run();
    }

    [ContextMenu("Jalankan Demo")]
    public void Run()
    {
        var boot = GameBoostrap.Instance;
        if (boot == null || boot.Database == null) { Debug.LogError("GameBoostrap atau Database belum terpasang."); return; }

        var db = boot.Database;
        var rule = db.ScoreRule;
        var plant = db.Plants[0];

        // ---------- LEVEL 1 ----------
        var level1 = db.Levels[0];
        var identify = new IdentifySession(plant, rule);
        identify.TapEmpty(); // satu salah ketuk, supaya terlihat penalti
        foreach (var f in plant.Facilities)
        {
            identify.TapFacility(f);
            identify.SubmitHazards(f, f.Hazards.ToList()); // jawab benar semua
        }
        Show("Level 1", identify.GetScore());
        boot.FinishLevel(plant, level1, identify.GetScore());

        // ---------- LEVEL 2, 3, 4 ----------
        for (int i = 1; i < db.Levels.Length; i++)
        {
            var level = db.Levels[i];
            var session = RoundSession.Create(level, plant, rule);
            bool firstRound = true;

            do
            {
                var f = session.Current;
                if (firstRound)
                {
                    // Ronde pertama sengaja dijawab salah dulu, lalu benar (dapat poin separuh).
                    session.Submit(false);
                    firstRound = false;
                }
                RoundResult r = session.Submit(true);
                if (!r.roundFinished) Debug.LogWarning("Ronde belum selesai: " + f.Name);
            }
            while (session.NextRound());

            Show(level.Name, session.GetScore());
            boot.FinishLevel(plant, level, session.GetScore());
        }

        // ---------- LEADERBOARD ----------
        var top = boot.Ranking.GetTop(plant.Id, Leaderboard.TotalCategory, 5);
        foreach (var item in top)
            Debug.Log("[Leaderboard total] " + item.UserName + " | " + item.Point + " poin | " + item.Star + " bintang");
    }

    static void Show(string title, Score s)
    {
        Debug.Log(title + ": " + s.Point + " / " + s.MaxPoint + " (" + Mathf.RoundToInt(s.Percent) + "%), bintang " + s.Star + ", lulus: " + s.Passed);
    }
}
