using UnityEngine;
using UnityEngine.UI;

namespace Game.APD
{
    // Keputusan untuk Level 4 diperbanyak agar lebih bervariasi.
    public enum CareDecision
    {
        Layak,              // Kondisi baik, siap pakai
        Bersihkan,          // Lap kain lembut + sabun ringan, bilas (helm, face shield, kacamata, sarung tangan)
        Disinfeksi,         // Respirator reusable: lepas filter, cuci sabun disinfektan ringan
        Keringkan,          // Gantung kering sendiri, jauhkan dari panas/matahari (harness, sarung tangan, sepatu)
        KondisikanKulit,    // Kondisioner kulit (apron las, sarung tangan kulit)
        Rapikan,            // Harness kusut: rapikan strap, lalu inspeksi
        Perbaiki,           // Rusak ringan yang boleh ditangani
        GantiKomponen,      // Lensa luar helm las, cushion earmuff, filter/kartrid, busa peredam, suspensi helm
        Ganti,              // Retak, penyok, berserabut, kedaluwarsa, setelah benturan keras
        BuangSekaliPakai,   // Earplug, masker debu, kartrid sekali pakai, coverall kimia
        Kalibrasi,          // Alat ukur/sensor (gas detector)
        InspeksiKompeten,   // Inspeksi oleh orang kompeten tiap 6 bulan (full body harness)
        UjiBerkala,         // Uji dielektrik (sarung tangan listrik), fit test (respirator)
        CekSegel,           // Seal check respirator sebelum pakai
        KonfirmasiPemasok,  // Jenis/spesifikasi tidak jelas: konfirmasi ke pemasok/safety
        TandaiTidakLayak,   // Tag "DO NOT USE"
        LaporAtasan,        // Laporkan ke supervisor/K3
        Karantina,          // Terkontaminasi parah/berbahaya
        MusnahkanBeritaAcara, // Pemusnahan APD berbahan berbahaya wajib berita acara (Permenaker Ps.8 ayat 3)

        // ===== BENAR =====
        TempatSejukKering,          // Umum: sejuk, kering, berventilasi
        JauhSinarMatahari,          // Hindari UV langsung (helm, harness, rompi)
        JauhBahanKimia,             // Jauh solvent, minyak, kimia (helm, harness)
        CaseMicrofiber,             // Kacamata: case/kantong microfiber, lensa menghadap atas
        DatarAtauGantung,           // Face shield: datar/tergantung, tidak tertindih (visor tidak melengkung)
        GantungHarness,             // Harness: gantung atau datar, tidak tertindih, bebas gas/UV/panas/korosif
        GantungKeringTanpaLipatan,  // Aluminized suit, heat suit, apron, rompi
        WadahTertutup,              // Respirator & kartrid: kantong/wadah tertutup, jauh debu, kelembapan, kimia
        KemasanAsli,                // Masker sekali pakai, kartrid, chemical suit
        RakTegakSepatu,             // Safety shoes: tegak di rak, kering, tidak ditumpuk
        BerdiriAtauDigantungBoot,   // Sepatu boot: berdiri/digantung, tidak dilipat
        KotakTasGantunganSuit,      // Chemical suit: kotak/tas/gantungan, tidak ditekan beban
        DispenserDekatHazardSign,   // Earplug dispenser: kering, dekat hazard sign
        GulungKering,               // Barrier tape/chain: digulung, kering
        TempatBersihKeringTerpisah, // Sarung tangan kimia: bersih, kering, jauh panas/uap

        // ===== SALAH (jebakan soal) =====
        DashboardMobilTerik,        // Mobil terik / dashboard
        SinarMatahariLangsung,
        DekatBahanKimia,            // Dekat solvent/oli/kimia
        DitumpukDitindih,           // Ditumpuk, tertindih benda berat
        DilemparDiTumpukanAlat,     // Dilempar bersama peralatan
        DilipatTajam,               // Suit, apron, boot dilipat tajam
        LembapBasah,                // Ruang lembap, APD masih basah
        DiLantaiTerbuka,            // Di lantai area kerja, kena debu/percikan
        LensaMenghadapBawah,        // Kacamata lensa menempel permukaan keras
        DekatSumberPanas,            // Dekat tungku/panas

        // Tambahkan di bagian paling akhir CareDecision
        IsiUlang,
        GantiSeluruhAPD,
        GantiJenisAPD
    }

    public enum CareCaseType
    {
        Maintenance, // Perawatan: bersihkan, ganti, perbaiki, dll
        Storage      // Penyimpanan: cara/tempat menyimpan yang benar
    }

    [System.Serializable]
    public class CareCase
    {
        [SerializeField] Sprite caseImage;
        [SerializeField] private PersonalProtectiveEquipment equipment;
        [SerializeField] private CareCaseType caseType;
        [SerializeField, TextArea] private string condition;

        [SerializeField] private string[] defectTags; 

        [SerializeField] private CareDecision correctDecision;
        [SerializeField, TextArea] private string explanation;

        public PersonalProtectiveEquipment Equipment => equipment;
        public string Condition => condition;
        public CareDecision CorrectDecision => correctDecision;
        public string Explanation => explanation;
        public CareCaseType CaseType => caseType;
        public string[] DefectTags => defectTags;

        public Sprite CaseImage => caseImage;
    }
}