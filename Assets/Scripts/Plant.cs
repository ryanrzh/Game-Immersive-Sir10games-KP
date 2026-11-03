using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.APD
{
    // Satu plant (contoh: RMH). Berisi gambar peta dan daftar titik (Facility).
    [CreateAssetMenu(fileName = "Plant_", menuName = "Game APD/Plant")]
    public class Plant : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [Tooltip("Gambar peta plant. Posisi titik mengacu ke gambar ini.")]
        [SerializeField] private Sprite image;
        [SerializeField] private VectorImage vectorImage; // Untuk layar besar, pakai ini. Kalau null, pakai image biasa.

        [SerializeField] private Facility[] facilities = new Facility[0];

        [SerializeField] private bool isLocked = true; // Kalau true, plant ini tidak bisa dipilih di menu.

        public bool IsLocked => isLocked;

        public string Id => id;
        public string Name => displayName;
        public string Description => description;
        public Sprite Image => image;
        public VectorImage VectorImage => vectorImage;

        public Facility[] Facilities => facilities;

        // Jumlah semua kategori bahaya di plant ini (dipakai untuk skor maksimal Level 1).
        public int TotalHazardEntries
        {
            get
            {
                int total = 0;
                foreach (var f in facilities) if (f != null) total += f.Hazards.Length;
                return total;
            }
        }

        // Daftar bahaya unik di plant ini (pilihan kategori di Level 1).
        public List<Hazard> AllHazards
        {
            get
            {
                var list = new List<Hazard>();
                foreach (var f in facilities)
                {
                    if (f == null) continue;
                    foreach (var h in f.Hazards) if (h != null && !list.Contains(h)) list.Add(h);
                }
                return list;
            }
        }
    }
}
