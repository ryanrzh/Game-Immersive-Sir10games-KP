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
    }
}
