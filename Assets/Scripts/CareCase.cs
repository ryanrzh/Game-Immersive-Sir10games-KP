using UnityEngine;

namespace Game.APD
{
    // Keputusan untuk Level 4 (Perawatan APD).
    public enum CareDecision { Layak, Bersihkan, Perbaiki, Ganti }

    // Satu kartu kondisi APD untuk Level 4. Kondisi ditulis sebagai teks, tanpa gambar.
    [System.Serializable]
    public class CareCase
    {
        [SerializeField] private PersonalProtectiveEquipment equipment;
        [SerializeField, TextArea] private string condition;
        [SerializeField] private CareDecision correctDecision;
        [SerializeField, TextArea] private string explanation;

        public PersonalProtectiveEquipment Equipment => equipment;
        public string Condition => condition;
        public CareDecision CorrectDecision => correctDecision;
        public string Explanation => explanation;
    }
}
