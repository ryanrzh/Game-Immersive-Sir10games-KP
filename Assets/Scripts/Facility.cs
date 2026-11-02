using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.APD
{
    // Facility = satu TITIK di peta plant (contoh: "Panel listrik").
    // Di layar game tidak ada pemilihan fasilitas, ini hanya data titiknya.
    [CreateAssetMenu(fileName = "Facility_", menuName = "Game APD/Facility (Titik)")]
    public class Facility : ScriptableObject
    {
        [Header("Info")]
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private Sprite image;
        [Tooltip("Posisi di gambar peta plant. x dan y antara 0 sampai 1. (0,0) kiri bawah, (1,1) kanan atas.")]
        [SerializeField] private Vector2 positionPoint;

        [Header("Bahaya titik ini (boleh lebih dari satu)")]
        [SerializeField] private Hazard[] hazards = new Hazard[0];

        [Header("APD pengecoh untuk Level 2 dan 3 (APD benar dihitung otomatis dari bahaya)")]
        [SerializeField] private PersonalProtectiveEquipment[] decoyEquipment = new PersonalProtectiveEquipment[0];

        [Header("Level 3: Inspeksi")]
        [SerializeField, TextArea] private string inspectionScenario;
        [Tooltip("APD yang dipakai pekerja di skenario. Kalau ada APD benar yang tidak dipakai, berarti 'kurang'.")]
        [SerializeField] private PersonalProtectiveEquipment[] wornEquipment = new PersonalProtectiveEquipment[0];

        [Header("Level 4: Perawatan APD")]
        [SerializeField] private CareCase[] careCases = new CareCase[0];

        public string Id => id;
        public string Name => displayName;
        public string Description => description;
        public Sprite Image => image;
        public Vector2 PositionPoint => positionPoint;
        public Hazard[] Hazards => hazards;
        public PersonalProtectiveEquipment[] DecoyEquipment => decoyEquipment;
        public string InspectionScenario => inspectionScenario;
        public PersonalProtectiveEquipment[] WornEquipment => wornEquipment;
        public CareCase[] CareCases => careCases;

        public bool HasInspection => !string.IsNullOrEmpty(inspectionScenario);
        public bool HasCare => careCases != null && careCases.Length > 0;

        // APD yang benar = gabungan APD semua bahaya di titik ini.
        // Kalau APD dasar dan varian lengkapnya sama-sama dibutuhkan, hanya varian lengkap yang dipakai.
        public List<PersonalProtectiveEquipment> RequiredEquipment
        {
            get
            {
                var list = new List<PersonalProtectiveEquipment>();
                foreach (var h in hazards)
                {
                    if (h == null) continue;
                    foreach (var e in h.RequiredEquipment)
                        if (e != null && !list.Contains(e)) list.Add(e);
                }
                var all = new List<PersonalProtectiveEquipment>(list);
                list.RemoveAll(a => all.Any(b => b != a && b.Covers(a)));
                return list;
            }
        }

        // Pilihan APD yang tampil di Level 2 = APD benar + pengecoh.
        public List<PersonalProtectiveEquipment> EquipmentChoices
        {
            get
            {
                var list = RequiredEquipment;
                foreach (var e in decoyEquipment)
                    if (e != null && !list.Contains(e)) list.Add(e);
                return list;
            }
        }

        // APD benar yang TIDAK dipakai pekerja (untuk Level 3).
        // Pekerja yang memakai varian lengkap tetap dianggap memenuhi APD dasarnya.
        public List<PersonalProtectiveEquipment> MissingEquipment
        {
            get { return RequiredEquipment.Where(r => !wornEquipment.Any(w => w != null && w.Covers(r))).ToList(); }
        }
    }
}
