using UnityEngine;

namespace Game.APD
{
    // Satu jenis bahaya, contoh: Fire, Falling, Electric shock.
    // Cara buat: klik kanan di Project > Create > Game APD > Hazard.
    [CreateAssetMenu(fileName = "Hazard_", menuName = "Game APD/Hazard")]
    public class Hazard : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName; // bukan "name" karena sudah dipakai Unity
        [SerializeField, TextArea] private string description;
        [SerializeField] private Sprite image;
        [SerializeField] private Color color = Color.white;

        [Header("APD yang dibutuhkan untuk bahaya ini (dari tabel HIRADC)")]
        [SerializeField] private PersonalProtectiveEquipment[] requiredEquipment = new PersonalProtectiveEquipment[0];

        public string Id => id;
        public string Name => displayName;
        public string Description => description;
        public Sprite Image => image;
        public Color Color => color;
        public PersonalProtectiveEquipment[] RequiredEquipment => requiredEquipment;
    }
}
