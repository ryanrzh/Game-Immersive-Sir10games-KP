using UnityEngine;

namespace Game.APD
{
    // Satu jenis APD, contoh: Safety helmet, Full body harness.
    [CreateAssetMenu(fileName = "PPE_", menuName = "Game APD/APD (PPE)")]
    public class PersonalProtectiveEquipment : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private Sprite image;

        [Header("Bagian tubuh yang dilindungi (boleh pilih lebih dari satu)")]
        [SerializeField] private PPEZone zone = PPEZone.None;

        [Header("Varian (opsional)")]
        [Tooltip("Isi kalau APD ini versi lengkap dari APD dasar. Contoh: 'Safety shoes + foot strap' adalah varian dari 'Safety shoes'. " +
                 "Varian lengkap otomatis memenuhi kebutuhan APD dasarnya, tapi APD dasar tidak memenuhi kebutuhan varian lengkapnya.")]
        [SerializeField] private PersonalProtectiveEquipment variantOf;

        public string Id => id;
        public string Name => displayName;
        public string Description => description;
        public Sprite Image => image;
        public PPEZone Zone => zone;
        public PersonalProtectiveEquipment VariantOf => variantOf;

        // True kalau APD ini sama dengan "other", atau varian lengkap dari "other" (rantai varian ikut ditelusuri).
        // Contoh: shoesStrap.Covers(shoes) = true, tapi shoes.Covers(shoesStrap) = false.
        public bool Covers(PersonalProtectiveEquipment other)
        {
            var current = this;
            int guard = 0; // jaga-jaga kalau dua APD tidak sengaja saling menunjuk
            while (current != null && guard++ < 8)
            {
                if (current == other) return true;
                current = current.variantOf;
            }
            return false;
        }

        // True kalau APD ini menangani zona yang diminta (cocok dengan salah satu flag).
        public bool IsForZone(PPEZone target)
        {
            return (zone & target) != 0;
        }
    }
}
