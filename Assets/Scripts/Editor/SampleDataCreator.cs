#if UNITY_EDITOR
using System.Collections.Generic;
using Game;
using Game.APD;
using UnityEditor;
using UnityEngine;

// Membuat semua asset contoh sekali klik: Tools > Game APD > Buat Data Contoh
// Hasilnya ada di Assets/GameData. Isinya contoh (plant RMH, 10 titik, 13 bahaya, 24 APD) dan boleh diedit.
public static class SampleDataCreator
{
    const string Root = "Assets/GameData";

    // Asset lama dari versi data contoh sebelumnya yang sudah tidak dipakai, dibuang otomatis kalau ada.
    // (foot_strap sudah digabung jadi varian sepatu)
    static readonly string[] StaleAssets = { "PPE/foot_strap" };

    // id, nama, (opsional) id APD dasar kalau ini varian lengkapnya.
    // Contoh: "shoes_strap" adalah varian lengkap dari "shoes". Varian lengkap otomatis memenuhi kebutuhan APD dasarnya.
    static readonly string[][] PpeList =
    {
        new[]{"glove","Safety glove"}, new[]{"shoes","Safety shoes / boots"}, new[]{"shoes_strap","Safety shoes / boots + foot strap","shoes"},
        new[]{"helmet","Safety helmet"}, new[]{"glasses","Safety glasses"}, new[]{"mask","Safety mask"},
        new[]{"face_shield","Face shield"}, new[]{"ear_protect","Ear plug / ear muff"}, new[]{"flash_light","Flash light"},
        new[]{"harness","Full body harness"}, new[]{"harness_lanyard","Full body harness + double lanyard","harness"}, new[]{"elec_glove","Electrical glove"},
        new[]{"elec_suit","Full electrical suit"}, new[]{"hivis","Baju lengan panjang hi-vis"}, new[]{"vest","Safety vest"},
        new[]{"co_mask","CO mask"}, new[]{"fr_cloth","Fire resistant clothing"}, new[]{"chem_glove","Chemical gloves"},
        new[]{"goggles","Goggles"}, new[]{"chem_suit","Chemical suit"}, new[]{"cut_glove","Cut resistant glove (leather)"},
        new[]{"respirator","Respirator"}, new[]{"scba","SCBA"}, new[]{"gas_detector","Gas detector"},
    };

    // id APD -> bagian tubuh yang dilindungi. Boleh gabungan, contoh: PPEZone.Head | PPEZone.Eye.
    static readonly Dictionary<string, PPEZone> PpeZones = new Dictionary<string, PPEZone>
    {
        {"glove", PPEZone.Hand}, {"shoes", PPEZone.Foot}, {"shoes_strap", PPEZone.Foot},
        {"helmet", PPEZone.Head}, {"glasses", PPEZone.Eye}, {"mask", PPEZone.Respiratory},
        {"face_shield", PPEZone.Eye}, {"ear_protect", PPEZone.Ear}, {"flash_light", PPEZone.Other},
        {"harness", PPEZone.FallProtection}, {"harness_lanyard", PPEZone.FallProtection}, {"elec_glove", PPEZone.Hand},
        {"elec_suit", PPEZone.Body}, {"hivis", PPEZone.Body}, {"vest", PPEZone.Body},
        {"co_mask", PPEZone.Respiratory}, {"fr_cloth", PPEZone.Body}, {"chem_glove", PPEZone.Hand},
        {"goggles", PPEZone.Eye}, {"chem_suit", PPEZone.Body}, {"cut_glove", PPEZone.Hand},
        {"respirator", PPEZone.Respiratory}, {"scba", PPEZone.Respiratory}, {"gas_detector", PPEZone.Other},
    };

    static PPEZone ZoneOf(string ppeId)
    {
        PPEZone zone;
        return PpeZones.TryGetValue(ppeId, out zone) ? zone : PPEZone.None;
    }

    // id, nama, warna, id APD yang dibutuhkan (dipisah koma)
    static readonly string[][] HazardList =
    {
        new[]{"pinched","Pinched","D64545","glove,shoes_strap"},
        new[]{"flying_object","Flying object","F28C00","helmet,glasses,mask,face_shield"},
        new[]{"occupational_disease","Occupational disease","F2A900","ear_protect,mask,flash_light"},
        new[]{"falling","Falling","E3C300","harness_lanyard"},
        new[]{"electric_shock","Electric shock","8CC63F","elec_glove,elec_suit"},
        new[]{"collision","Collision/crash","1BA9C9","hivis,vest"},
        new[]{"fall_down","Fall down","3B9AE8","helmet,shoes"},
        new[]{"collapse","Collapse","4F7DE0","helmet"},
        new[]{"fire","Fire","5B6BE0","co_mask,fr_cloth"},
        new[]{"hazardous","Hazardous substances","9A63E0","co_mask,chem_glove,goggles,chem_suit"},
        new[]{"cut","Cut","C46AD0","cut_glove,shoes"},
        new[]{"gas_poisoning","Gas poisoning, suffocation","F06BA8","respirator,scba,gas_detector"},
        new[]{"other","Other","A8A8A8",""}, // tidak ada APD di tabel HIRADC
    };

    class FacilityData
    {
        public string id, name, description, scenario;
        public float x, y;
        public string hazards, decoys, worn;
        public string careApd, careCondition, careExplanation;
        public CareDecision careDecision;
    }

    static FacilityData F(string id, string name, float x, float y, string desc, string hazards, string decoys,
        string scenario, string worn, string careApd, string careCondition, CareDecision decision, string careExplanation)
    {
        return new FacilityData
        {
            id = id, name = name, x = x, y = y, description = desc, hazards = hazards, decoys = decoys,
            scenario = scenario, worn = worn, careApd = careApd, careCondition = careCondition,
            careDecision = decision, careExplanation = careExplanation
        };
    }

    static readonly FacilityData[] FacilityList =
    {
        F("f1","Roller belt conveyor",0.5f,0.444f,"Tangan bisa terjepit di titik jepit roller dan pulley.","pinched","harness,ear_protect,elec_suit,shoes",
          "Pekerja mengganti roller conveyor yang macet.","glove,shoes","glove","Sobek 2 cm di telapak tangan kiri.",CareDecision.Ganti,"Sarung tangan sobek tidak melindungi dari jepitan."),
        F("f2","Puncak stockpile",0.2375f,0.378f,"Debu dan material beterbangan, juga paparan bising.","flying_object,occupational_disease","harness,elec_glove,vest",
          "Pekerja membersihkan debu di puncak stockpile.","helmet,glasses,mask,ear_protect,flash_light","mask","Filter penuh debu dan sulit dipakai bernapas. Masker utuh, tali normal.",CareDecision.Ganti,"Filter yang penuh debu harus diganti."),
        F("f3","Stockpile batubara",0.1375f,0.156f,"Batubara mudah terbakar dan bisa mengeluarkan gas CO.","fire,hazardous","vest,ear_protect,harness",
          "Pekerja memeriksa titik panas di stockpile batubara.","co_mask,fr_cloth,chem_glove,goggles,chem_suit","co_mask","Kanister filter melewati tanggal kedaluwarsa.",CareDecision.Ganti,"Filter yang kedaluwarsa tidak lagi andal."),
        F("f4","Panel listrik",0.9025f,0.233f,"Risiko tersengat listrik saat membuka atau memeriksa panel.","electric_shock","helmet,vest,goggles",
          "Pekerja membuka panel listrik untuk memeriksa sensor.","elec_glove","elec_glove","Bersih, tidak ada lubang, tag uji masih berlaku.",CareDecision.Layak,"Kondisi baik dan masih dalam masa uji."),
        F("f5","Tangga transfer house",0.775f,0.422f,"Pekerja bisa jatuh dari ketinggian atau terpeleset di tangga.","falling,fall_down","ear_protect,mask,cut_glove,harness",
          "Pekerja naik tangga transfer house untuk memeriksa roller.","helmet,shoes","harness_lanyard","Tali berserabut dan jahitan terlepas di bagian bahu.",CareDecision.Ganti,"Harness rusak tidak boleh dipakai."),
        F("f6","Hopper",0.581f,0.189f,"Material lengket bisa runtuh atau struktur rusak.","collapse","glove,vest,harness",
          "Pekerja membersihkan material lengket di hopper.","helmet","helmet","Kotor oleh debu dan lumpur, tidak ada retakan.",CareDecision.Bersihkan,"Cukup dibersihkan karena tidak ada kerusakan."),
        F("f7","Jalan akses alat berat",0.3f,0.07f,"Pekerja bisa tertabrak alat yang bergerak.","collision","goggles,chem_glove,elec_glove",
          "Pekerja menyeberang jalan akses saat loader beroperasi.","vest","vest","Kotor oleh oli, garis reflektif masih utuh.",CareDecision.Bersihkan,"Garis reflektif utuh, cukup dibersihkan."),
        F("f8","Conveyor (bising)",0.4125f,0.353f,"Paparan bising dan kerja manual yang lama.","occupational_disease","harness,elec_suit,goggles",
          "Pekerja berjaga di dekat conveyor yang bising.","mask,flash_light","ear_protect","Bantalan ear muff robek, ear plug masih baru.",CareDecision.Perbaiki,"Ganti bantalan ear muff yang robek."),
        F("f9","Lantai licin dekat hopper",0.5375f,0.07f,"Permukaan licin dan area tidak rapi.","fall_down","face_shield,chem_suit,glove",
          "Pekerja membersihkan lantai licin dekat hopper.","helmet,shoes","shoes","Sol terkelupas sebagian dan lapisan baja terlihat.",CareDecision.Ganti,"Sol rusak tidak lagi anti-slip dan melindungi."),
        F("f10","Area gerinda",0.7875f,0.156f,"Percikan dan serpihan, serta risiko luka sayat.","cut,flying_object","harness,ear_protect,vest",
          "Pekerja menggerinda bracket di area perawatan.","cut_glove,shoes,helmet,glasses,mask","cut_glove","Tali pengikat longgar, bahan masih utuh.",CareDecision.Perbaiki,"Cukup kencangkan tali pengikat."),
    };

    [MenuItem("Tools/Game APD/Buat Data Contoh")]
    public static void Create()
    {
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(Root + "/GameDatabase.asset");
        if (db != null)
        {
            // Data sudah ada: tawarkan isi ulang. GameDatabase tidak dihapus, jadi referensi di GameBoostrap tetap aman.
            bool ok = EditorUtility.DisplayDialog("Data sudah ada",
                "Data contoh di " + Root + " akan ditulis ulang: id, nama, zona, bahaya, dan hubungan antar asset dikembalikan ke isi contoh. " +
                "Gambar (Sprite) yang sudah kamu pasang tetap ada. Lanjut?", "Ya, isi ulang", "Batal");
            if (!ok) return;
        }
        else
        {
            db = Make<GameDatabase>("", "GameDatabase");
        }
        Build(db);
    }

    // Mengisi satu GameDatabase dengan data contoh lengkap (13 bahaya, 24 APD, 10 titik, 4 level, aturan skor).
    static void Build(GameDatabase db)
    {
        // Asset yang sudah ada dipakai ulang (bukan dihapus lalu dibuat ulang), jadi referensi dan Sprite aman.
        foreach (var stale in StaleAssets)
        {
            string stalePath = Root + "/" + stale + ".asset";
            if (AssetDatabase.LoadAssetAtPath<Object>(stalePath) != null) AssetDatabase.DeleteAsset(stalePath);
        }

        // 1. APD
        var ppe = new Dictionary<string, PersonalProtectiveEquipment>();
        foreach (var p in PpeList) ppe[p[0]] = CreatePpe(p);
        // Hubungkan varian ke APD dasarnya (kolom ke-3 di PpeList)
        foreach (var p in PpeList)
        {
            if (p.Length < 3) continue;
            var variantSo = new SerializedObject(ppe[p[0]]);
            variantSo.FindProperty("variantOf").objectReferenceValue = ppe[p[2]];
            Apply(variantSo);
        }

        // 2. Bahaya
        var hazards = new Dictionary<string, Hazard>();
        foreach (var h in HazardList) hazards[h[0]] = CreateHazard(h, ppe);

        // 3. Titik (Facility)
        var facilities = new List<Facility>();
        foreach (var d in FacilityList)
        {
            var asset = Make<Facility>("Facilities", d.id);
            var so = new SerializedObject(asset);
            so.FindProperty("id").stringValue = d.id;
            so.FindProperty("displayName").stringValue = d.name;
            so.FindProperty("description").stringValue = d.description;
            so.FindProperty("positionPoint").vector2Value = new Vector2(d.x, d.y);
            SetRefs(so, "hazards", Pick(hazards, d.hazards));
            SetRefs(so, "decoyEquipment", Pick(ppe, d.decoys));
            so.FindProperty("inspectionScenario").stringValue = d.scenario;
            SetRefs(so, "wornEquipment", Pick(ppe, d.worn));

            var care = so.FindProperty("careCases");
            care.arraySize = 1;
            var e = care.GetArrayElementAtIndex(0);
            e.FindPropertyRelative("equipment").objectReferenceValue = ppe[d.careApd];
            e.FindPropertyRelative("condition").stringValue = d.careCondition;
            e.FindPropertyRelative("correctDecision").enumValueIndex = (int)d.careDecision;
            e.FindPropertyRelative("explanation").stringValue = d.careExplanation;
            Apply(so);
            facilities.Add(asset);
        }

        // 4. Plant RMH
        var plant = Make<Plant>("Plants", "Plant_RMH");
        var plantSo = new SerializedObject(plant);
        plantSo.FindProperty("id").stringValue = "rmh";
        plantSo.FindProperty("displayName").stringValue = "RMH";
        plantSo.FindProperty("description").stringValue = "Raw Material Handling Plant";
        SetRefs(plantSo, "facilities", facilities.ToArray());
        Apply(plantSo);

        // 5. Level 1 sampai 4
        var levels = new List<Level>();
        string[][] levelData =
        {
            new[]{"level1","Identifikasi Bahaya","0"}, new[]{"level2","Pakai APD","1"},
            new[]{"level3","Inspeksi","2"}, new[]{"level4","Perawatan APD","3"},
        };
        foreach (var l in levelData)
        {
            var asset = Make<Level>("Levels", "Level_" + l[0].Substring(5));
            var so = new SerializedObject(asset);
            so.FindProperty("id").stringValue = l[0];
            so.FindProperty("displayName").stringValue = l[1];
            so.FindProperty("type").enumValueIndex = int.Parse(l[2]);
            Apply(so);
            levels.Add(asset);
        }

        // 6. Aturan skor dan isi database
        var dbSo = new SerializedObject(db);
        SetRefs(dbSo, "plants", new Object[] { plant });
        SetRefs(dbSo, "levels", levels.ToArray());
        var ruleProp = dbSo.FindProperty("scoreRule");
        if (ruleProp.objectReferenceValue == null)
        {
            // pakai ScoreRule yang sudah ada (aturan skor milikmu tetap aman), kalau belum ada baru dibuat
            var rule = AssetDatabase.LoadAssetAtPath<ScoreRule>(Root + "/ScoreRule.asset");
            ruleProp.objectReferenceValue = rule != null ? rule : Make<ScoreRule>("", "ScoreRule");
        }
        Apply(dbSo);
        EditorUtility.SetDirty(db);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = db;
        EditorGUIUtility.PingObject(db);

        var problems = Verify(ppe, hazards, facilities, plant, levels, db);
        if (problems.Count == 0)
            Debug.Log("Data contoh selesai di " + Root + " dan lolos pengecekan (" + ppe.Count + " APD, " + hazards.Count +
                      " bahaya, " + facilities.Count + " titik, " + levels.Count + " level). Pasang GameDatabase ke GameBoostrap. " +
                      "Gambar peta (Plant.image) dan gambar APD/bahaya bisa diisi sendiri.");
        else
            Debug.LogWarning("Data contoh selesai, tapi ada yang belum terisi:\n- " + string.Join("\n- ", problems.ToArray()));
    }

    static int CountIds(string commaIds)
    {
        return string.IsNullOrEmpty(commaIds) ? 0 : commaIds.Split(',').Length;
    }

    // Membaca ulang hasil build dan melaporkan apa saja yang kosong atau tidak cocok dengan data contoh.
    static List<string> Verify(Dictionary<string, PersonalProtectiveEquipment> ppe, Dictionary<string, Hazard> hazards,
        List<Facility> facilities, Plant plant, List<Level> levels, GameDatabase db)
    {
        var problems = new List<string>();

        foreach (var p in PpeList)
        {
            var a = ppe[p[0]];
            if (string.IsNullOrEmpty(a.Id) || string.IsNullOrEmpty(a.Name)) problems.Add("APD " + p[0] + ": id atau nama kosong");
            if (a.Zone == PPEZone.None) problems.Add("APD " + p[0] + ": zona kosong");
            if (p.Length > 2 && a.VariantOf != ppe[p[2]]) problems.Add("APD " + p[0] + ": Variant Of belum terhubung ke " + p[2]);
        }

        foreach (var h in HazardList)
        {
            var a = hazards[h[0]];
            if (string.IsNullOrEmpty(a.Id) || string.IsNullOrEmpty(a.Name)) problems.Add("Bahaya " + h[0] + ": id atau nama kosong");
            int expected = CountIds(h[3]);
            int linked = 0;
            foreach (var e in a.RequiredEquipment) if (e != null) linked++;
            if (linked != expected) problems.Add("Bahaya " + h[0] + ": APD terhubung " + linked + " dari " + expected);
        }

        for (int i = 0; i < FacilityList.Length; i++)
        {
            var d = FacilityList[i];
            var f = facilities[i];
            if (f.Hazards.Length != CountIds(d.hazards)) problems.Add("Titik " + d.id + ": jumlah bahaya tidak cocok");
            if (f.DecoyEquipment.Length != CountIds(d.decoys)) problems.Add("Titik " + d.id + ": jumlah APD pengecoh tidak cocok");
            if (f.WornEquipment.Length != CountIds(d.worn)) problems.Add("Titik " + d.id + ": jumlah APD yang dipakai tidak cocok");
            if (f.CareCases.Length != 1 || f.CareCases[0].Equipment == null) problems.Add("Titik " + d.id + ": kartu perawatan kosong");
        }

        if (plant.Facilities.Length != FacilityList.Length) problems.Add("Plant: jumlah titik tidak cocok");
        if (levels.Count != 4) problems.Add("Level: jumlah tidak 4");
        if (db.Plants.Length != 1 || db.Levels.Length != 4) problems.Add("GameDatabase: daftar plant atau level belum terisi");
        if (db.ScoreRule == null) problems.Add("GameDatabase: ScoreRule kosong");
        return problems;
    }

    // Membuat satu asset APD dari satu baris PpeList (id, nama), lengkap dengan zonanya.
    static PersonalProtectiveEquipment CreatePpe(string[] p)
    {
        var asset = Make<PersonalProtectiveEquipment>("PPE", p[0]);
        var so = new SerializedObject(asset);
        so.FindProperty("id").stringValue = p[0];
        so.FindProperty("displayName").stringValue = p[1];
        so.FindProperty("zone").intValue = (int)ZoneOf(p[0]);
        Apply(so);
        return asset;
    }

    // Membuat satu asset Hazard dari satu baris HazardList (id, nama, warna, id APD).
    static Hazard CreateHazard(string[] h, Dictionary<string, PersonalProtectiveEquipment> ppe)
    {
        var asset = Make<Hazard>("Hazards", h[0]);
        var so = new SerializedObject(asset);
        so.FindProperty("id").stringValue = h[0];
        so.FindProperty("displayName").stringValue = h[1];
        Color c; ColorUtility.TryParseHtmlString("#" + h[2], out c);
        so.FindProperty("color").colorValue = c;
        SetRefs(so, "requiredEquipment", Pick(ppe, h[3]));
        Apply(so);
        return asset;
    }

    static T Make<T>(string subFolder, string fileName) where T : ScriptableObject
    {
        string folder = string.IsNullOrEmpty(subFolder) ? Root : Root + "/" + subFolder;
        EnsureFolder(folder);
        string path = folder + "/" + fileName + ".asset";
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
        }
        return asset;
    }

    // Terapkan perubahan SerializedObject dan tandai asset berubah supaya pasti tersimpan ke disk.
    static void Apply(SerializedObject so)
    {
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(so.targetObject);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int i = path.LastIndexOf('/');
        string parent = path.Substring(0, i);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(i + 1));
    }

    static void SetRefs(SerializedObject so, string field, Object[] refs)
    {
        var prop = so.FindProperty(field);
        prop.arraySize = refs.Length;
        for (int i = 0; i < refs.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = refs[i];
    }

    static T[] Pick<T>(Dictionary<string, T> dict, string commaIds) where T : Object
    {
        var list = new List<T>();
        foreach (var rawId in commaIds.Split(','))
        {
            string id = rawId.Trim();
            if (id.Length == 0) continue;
            if (dict.ContainsKey(id)) list.Add(dict[id]);
            else Debug.LogWarning("Id tidak dikenal di data contoh, dilewati: " + id);
        }
        return list.ToArray();
    }
}
#endif
