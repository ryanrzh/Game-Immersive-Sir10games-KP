using System.Collections.Generic;
using System.Linq;
using Game.APD;

namespace Game
{
    public class CheckResult
    {
        public bool correct;
        public string message; // pesan feedback untuk pemain
    }

    // Mengecek jawaban pemain. Hasilnya (correct) dikirim ke RoundSession.Submit().
    public static class AnswerChecker
    {
        // LEVEL 2: pemain memilih APD untuk sebuah titik.
        public static CheckResult ChoosePPE(Facility f, List<PersonalProtectiveEquipment> chosen)
        {
            var required = f.RequiredEquipment;

            // Kebutuhan terpenuhi kalau ada pilihan yang sama, atau varian lengkapnya (Covers).
            var missing = required.Where(r => !chosen.Any(c => c.Covers(r))).ToList();

            var extra = new List<PersonalProtectiveEquipment>();
            var incomplete = new List<string>();
            var incompleteTargets = new List<PersonalProtectiveEquipment>();
            foreach (var c in chosen)
            {
                if (required.Any(r => c.Covers(r))) continue; // dibutuhkan, atau varian yang lebih lengkap
                var target = missing.FirstOrDefault(r => r.Covers(c));
                if (target != null)
                {
                    // APD dasar dipilih padahal yang dibutuhkan versi lengkapnya
                    incomplete.Add(c.Name + " belum lengkap, seharusnya " + target.Name + ".");
                    if (!incompleteTargets.Contains(target)) incompleteTargets.Add(target);
                }
                else extra.Add(c);
            }
            var stillMissing = missing.Where(m => !incompleteTargets.Contains(m)).ToList();

            if (missing.Count == 0 && extra.Count == 0)
                return new CheckResult { correct = true, message = "APD sudah lengkap dan tepat." };

            string msg = string.Join(" ", incomplete.ToArray());
            if (stillMissing.Count > 0) msg += " Kurang: " + Names(stillMissing) + ".";
            if (extra.Count > 0) msg += " Tidak diperlukan: " + Names(extra) + ".";
            return new CheckResult { correct = false, message = msg.Trim() };
        }

        // LEVEL 3: pemain menilai "sudah sesuai" atau "ada yang kurang" (lalu menunjuk APD yang kurang).
        public static CheckResult Inspection(Facility f, bool saysOk, List<PersonalProtectiveEquipment> pickedMissing)
        {
            var actualMissing = f.MissingEquipment;
            if (actualMissing.Count == 0)
            {
                return saysOk
                    ? new CheckResult { correct = true, message = "Tepat, APD pekerja sudah sesuai." }
                    : new CheckResult { correct = false, message = "APD pekerja sebenarnya sudah sesuai." };
            }
            if (saysOk)
                return new CheckResult { correct = false, message = "Masih ada APD yang kurang." };

            bool same = new HashSet<PersonalProtectiveEquipment>(actualMissing).SetEquals(pickedMissing);
            return same
                ? new CheckResult { correct = true, message = "Tepat, APD yang kurang: " + Names(actualMissing) + "." }
                : new CheckResult { correct = false, message = "APD yang kamu tunjuk belum tepat." };
        }

        // LEVEL 4: pemain memutuskan Layak / Bersihkan / Perbaiki / Ganti untuk tiap kartu.
        public static CheckResult Care(Facility f, List<CareDecision> decisions)
        {
            var lines = new List<string>();
            for (int i = 0; i < f.CareCases.Length; i++)
            {
                var c = f.CareCases[i];
                bool ok = i < decisions.Count && decisions[i] == c.CorrectDecision;
                if (!ok) lines.Add(c.Equipment.Name + ": seharusnya " + c.CorrectDecision + ". " + c.Explanation);
            }
            if (lines.Count == 0) return new CheckResult { correct = true, message = "Semua keputusan tepat." };
            return new CheckResult { correct = false, message = string.Join("\n", lines.ToArray()) };
        }

        static string Names(IEnumerable<PersonalProtectiveEquipment> list)
        {
            return string.Join(", ", list.Select(x => x.Name).ToArray());
        }
    }
}
