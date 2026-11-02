using UnityEngine;

namespace Game
{
    public enum LevelType
    {
        IdentifyHazard, // Level 1: cari titik di peta dan pilih bahayanya
        ChoosePPE,      // Level 2: pilih APD
        Inspection,     // Level 3: inspeksi
        Care            // Level 4: perawatan APD
    }

    // Data satu level. Buat 4 asset: Level 1, 2, 3, 4.
    [CreateAssetMenu(fileName = "Level_", menuName = "Game APD/Level")]
    public class Level : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private LevelType type;

        public string Id => id;
        public string Name => displayName;
        public string Description => description;
        public LevelType Type => type;

        // Level 2, 3, 4 berupa rangkaian ronde (satu titik per ronde). Level 1 bebas tanpa ronde.
        public bool IsRoundBased => type != LevelType.IdentifyHazard;
    }
}
