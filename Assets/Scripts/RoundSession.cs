using System.Collections.Generic;
using System.Linq;
using Game.APD;

namespace Game
{
    public class RoundResult
    {
        public bool correct;        // jawaban benar?
        public bool canRetry;       // masih boleh coba lagi?
        public bool roundFinished;  // ronde ini selesai?
        public float point;         // poin ronde ini (terisi jika ronde selesai)
    }

    // LEVEL 2, 3, 4: rangkaian ronde. Satu ronde = satu titik (Facility).
    // Aturan: maksimal 2 percobaan per ronde, pemain tidak pernah macet di satu titik.
    //   benar di percobaan 1 = 20 poin, percobaan 2 = 10 poin, salah semua = 0 (angka dari ScoreRule).
    // Kelas ini hanya mengatur ronde dan poin. Benar atau salahnya jawaban dicek oleh AnswerChecker.
    public class RoundSession
    {
        readonly List<Facility> facilities;
        readonly ScoreRule rule;
        int index;
        int attempt;
        float totalPoint;
        bool roundFinished;

        public RoundSession(List<Facility> facilities, ScoreRule rule)
        {
            this.facilities = facilities;
            this.rule = rule;
        }

        // Memilih titik yang cocok untuk jenis level.
        public static RoundSession Create(Level level, Plant plant, ScoreRule rule)
        {
            IEnumerable<Facility> list = plant.Facilities;
            if (level.Type == LevelType.Inspection) list = list.Where(f => f.HasInspection);
            if (level.Type == LevelType.Care) list = list.Where(f => f.HasCare);
            return new RoundSession(list.ToList(), rule);
        }

        public Facility Current { get { return facilities[index]; } }
        public int RoundNumber { get { return index + 1; } }
        public int RoundCount { get { return facilities.Count; } }
        public int Attempt { get { return attempt; } }      // percobaan yang sudah dipakai di ronde ini
        public float TotalPoint { get { return totalPoint; } }
        public float MaxPoint { get { return facilities.Count * rule.roundFirstTry; } }
        public bool IsLastRound { get { return index >= facilities.Count - 1; } }

        // Kirim hasil pengecekan jawaban pemain.
        public RoundResult Submit(bool correct)
        {
            var r = new RoundResult();
            if (roundFinished) return r;

            attempt++;
            r.correct = correct;
            if (correct)
            {
                r.point = rule.RoundPoint(attempt);
                r.roundFinished = true;
            }
            else if (attempt >= rule.maxAttempts)
            {
                r.point = rule.roundFail;
                r.roundFinished = true;
            }
            else
            {
                r.canRetry = true;
            }

            if (r.roundFinished)
            {
                totalPoint += r.point;
                roundFinished = true;
            }
            return r;
        }

        // Pindah ke ronde berikutnya. Mengembalikan false jika semua ronde sudah selesai.
        public bool NextRound()
        {
            if (!roundFinished) return true;
            if (IsLastRound) return false;
            index++;
            attempt = 0;
            roundFinished = false;
            return true;
        }

        public Score GetScore() { return new Score(totalPoint, MaxPoint, rule); }
    }
}
