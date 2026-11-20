using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.APD
{
    // Facility = satu TITIK di peta plant (contoh: "Panel listrik").
    // Di layar game tidak ada pemilihan fasilitas, ini hanya data titiknya.
    [CreateAssetMenu(fileName = "Facility_", menuName = "Game APD/Facility (Titik)")]
    public class Facility : ScriptableObject
    {
        [System.Serializable]
        public class HazardPoint
        {
            public Vector2 positionPoint;
            public Hazard hazards;
        }

        [Header("Info")]
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private VectorImage image;

        [Header("Level 1: Bahaya")]
        public HazardPoint[] points;

        //[Header("APD pengecoh untuk Level 2 dan 3 (APD benar dihitung otomatis dari bahaya)")]
        //[SerializeField] private PersonalProtectiveEquipment[] decoyEquipment = new PersonalProtectiveEquipment[0];

        [Header("Level 3: Inspeksi")]
        [SerializeField, TextArea] private string inspectionScenario;

        [Tooltip("Set APD STANDAR (Benar & Lengkap) yang WAJIB dipakai pekerja di titik ini.")]
        [SerializeField] private PersonalProtectiveEquipment[] correctEquipment = new PersonalProtectiveEquipment[0];

        [Tooltip("Kumpulan APD pengecoh/salah untuk diacak oleh sistem menggantikan APD yang benar.")]
        [SerializeField] private PersonalProtectiveEquipment[] decoyEquipment = new PersonalProtectiveEquipment[0];

        [SerializeField] private Hazard[] hazardCategories;

        public string InspectionScenario => inspectionScenario;
        public PersonalProtectiveEquipment[] CorrectEquipment => correctEquipment;
        public PersonalProtectiveEquipment[] DecoyEquipment => decoyEquipment;


        [Header("Level 4: Perawatan APD")]
        [SerializeField] private CareCaseDatabase careCaseDatabase;

        public string Id => id;
        public string Name => displayName;
        public string Description => description;
        public VectorImage Image => image;
        public CareCaseDatabase CareCaseDatabase => careCaseDatabase;
    }
}
