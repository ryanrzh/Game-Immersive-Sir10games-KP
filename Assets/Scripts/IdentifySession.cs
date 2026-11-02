using System.Collections.Generic;
using System.Linq;
using Game.APD;

namespace Game
{
    public enum TapResult { Found, AlreadyFound, Wrong }

    public class HazardPickResult
    {
        public int correct;   // kategori yang benar dipilih
        public int wrong;     // kategori yang salah dipilih
        public int missed;    // kategori yang terlewat
        public float point;   // poin dari jawaban ini
    }

    // LEVEL 1: bebas di peta, tanpa ronde.
    // Alur: TapFacility (ketuk titik) -> SubmitHazards (pilih semua kategori bahayanya).
    public class IdentifySession
    {
        readonly Plant plant;
        readonly ScoreRule rule;
        readonly HashSet<Facility> found = new HashSet<Facility>();
        readonly HashSet<Facility> answered = new HashSet<Facility>();
        float point;

        public IdentifySession(Plant plant, ScoreRule rule)
        {
            this.plant = plant;
            this.rule = rule;
        }

        public int FoundCount { get { return found.Count; } }
        public int TotalFacilities { get { return plant.Facilities.Length; } }
        public float Point { get { return point; } }
        public bool IsFinished { get { return answered.Count == TotalFacilities; } }

        public float MaxPoint
        {
            get { return TotalFacilities * rule.identifyFacilityFound + plant.TotalHazardEntries * rule.identifyHazardCorrect; }
        }

        public bool IsFound(Facility f) { return found.Contains(f); }

        // Pemain mengetuk sebuah titik yang dianggap berbahaya.
        public TapResult TapFacility(Facility f)
        {
            if (!plant.Facilities.Contains(f)) return TapEmpty();
            if (found.Contains(f)) return TapResult.AlreadyFound;
            found.Add(f);
            point += rule.identifyFacilityFound;
            return TapResult.Found;
        }

        // Pemain mengetuk tempat yang bukan titik bahaya.
        public TapResult TapEmpty()
        {
            point += rule.identifyWrongTap;
            return TapResult.Wrong;
        }

        // Pemain memilih semua kategori bahaya untuk titik yang sudah ditemukan.
        public HazardPickResult SubmitHazards(Facility f, List<Hazard> chosen)
        {
            var result = new HazardPickResult();
            if (!found.Contains(f) || answered.Contains(f)) return result;

            foreach (var h in chosen.Distinct())
            {
                if (f.Hazards.Contains(h)) result.correct++;
                else result.wrong++;
            }
            result.missed = f.Hazards.Length - result.correct;
            result.point = result.correct * rule.identifyHazardCorrect + result.wrong * rule.identifyHazardWrong;

            point += result.point;
            answered.Add(f);
            return result;
        }

        public Score GetScore() { return new Score(point, MaxPoint, rule); }
    }
}
