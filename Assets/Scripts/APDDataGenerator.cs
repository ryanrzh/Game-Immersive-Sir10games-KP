using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Game.APD.EditorTools
{
    public class APDDataGenerator : EditorWindow
    {
        const string PpeFolder = "Assets/Data/APD/PPE";
        const string DbPath = "Assets/Data/APD/CareCaseDatabase.asset";
        const string PrefKey = "APDDataGenerator.TablePath";

        // SESUAIKAN nama di kanan dengan isi enum PPEZone kamu.
        // Boleh lebih dari satu, pisahkan dengan koma (enum flags).
        static readonly Dictionary<string, string> ZoneMap = new Dictionary<string, string>
        {
            { "Kepala", "Head" },
            { "Mata & Muka", "Eye" },
            { "Telinga", "Ear" },
            { "Pernapasan", "Respiratory" },
            { "Badan", "Body" },
            { "Tangan", "Hand" },
            { "Kaki", "Foot" },
            { "Pelindung Jatuh", "FallProtection" },
            { "Lainnya", "Other" },
        };

        class Row
        {
            public string id, kategori, nama, skenario, tags, jawaban, jenis, penjelasan;
        }

        string tablePath;

        [MenuItem("Tools/Game APD/Generate Data dari Tabel")]
        static void Open() => GetWindow<APDDataGenerator>("APD Data Generator");

        void OnEnable() => tablePath = EditorPrefs.GetString(PrefKey, "");

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "File tabel = teks dipisah TAB (Excel: Save As > Text (Tab delimited), atau copy-paste ke .txt).\n" +
                "Baris pertama = header. Sprite gambar tidak disentuh, isi manual di tiap asset PPE.",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            tablePath = EditorGUILayout.TextField("File tabel", tablePath);
            if (GUILayout.Button("Browse", GUILayout.Width(70)))
            {
                var p = EditorUtility.OpenFilePanel("Pilih file tabel", "", "");
                if (!string.IsNullOrEmpty(p)) { tablePath = p; EditorPrefs.SetString(PrefKey, p); }
            }
            EditorGUILayout.EndHorizontal();

            GUI.enabled = !string.IsNullOrEmpty(tablePath) && File.Exists(tablePath);
            if (GUILayout.Button("1. Generate / Update APD (PPE assets)", GUILayout.Height(30)))
                GeneratePPE();
            if (GUILayout.Button("2. Generate / Update Care Cases (database)", GUILayout.Height(30)))
                GenerateCases();
            if (GUILayout.Button("Generate Semua", GUILayout.Height(30)))
            {
                GeneratePPE();
                GenerateCases();
            }
            GUI.enabled = true;
        }

        static readonly Regex IdRegex = new Regex(@"^[A-Za-z0-9_\-]+$");

        // ---------- Parsing ----------
        List<Row> ReadRows()
        {
            var rows = new List<Row>();

            var ext = Path.GetExtension(tablePath).ToLowerInvariant();
            if (ext == ".xlsx" || ext == ".xls" || ext == ".xlsm")
            {
                Debug.LogError("[APD] File ini Excel. Simpan dulu sebagai 'Text (Tab delimited) (*.txt)' lalu pilih file .txt-nya.");
                return rows;
            }

            string[] lines;
            using (var sr = new StreamReader(tablePath, System.Text.Encoding.UTF8, true)) // deteksi BOM UTF-8/UTF-16
                lines = sr.ReadToEnd().Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

            if (lines.Length == 0 || !lines[0].Contains("ID APD"))
                Debug.LogWarning("[APD] Header tidak mengandung 'ID APD'. Pastikan baris pertama adalah header tabel.");

            for (int i = 1; i < lines.Length; i++) // skip header
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var c = lines[i].Split('\t').Select(s => s.Trim().Trim('"')).ToArray();
                if (c.Length < 8)
                {
                    Debug.LogWarning($"[APD] Baris {i + 1} dilewati (kolom kurang dari 8).");
                    continue;
                }
                if (!IdRegex.IsMatch(c[0]))
                {
                    Debug.LogWarning($"[APD] Baris {i + 1} dilewati: ID '{c[0]}' tidak valid.");
                    continue;
                }
                rows.Add(new Row
                {
                    id = c[0],
                    kategori = c[1],
                    nama = c[2],
                    skenario = c[3],
                    tags = c[4],
                    jawaban = c[5],
                    jenis = c[6],
                    penjelasan = c[7]
                });
            }

            if (rows.Count == 0)
                Debug.LogError("[APD] Tidak ada baris valid terbaca. Cek format file (harus dipisah TAB).");
            return rows;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ---------- PPE assets ----------
        Dictionary<string, PersonalProtectiveEquipment> GeneratePPE()
        {
            EnsureFolder(PpeFolder);
            var result = new Dictionary<string, PersonalProtectiveEquipment>();
            int created = 0, updated = 0;

            foreach (var g in ReadRows().GroupBy(r => r.id))
            {
                var first = g.First();
                var path = $"{PpeFolder}/PPE_{first.id}.asset";
                var so = AssetDatabase.LoadAssetAtPath<PersonalProtectiveEquipment>(path);
                if (so == null)
                {
                    so = CreateInstance<PersonalProtectiveEquipment>();
                    AssetDatabase.CreateAsset(so, path);
                    created++;
                }
                else updated++;

                var ser = new SerializedObject(so);
                ser.FindProperty("id").stringValue = first.id;
                ser.FindProperty("displayName").stringValue = first.nama;

                if (ZoneMap.TryGetValue(first.kategori, out var zoneNames)
                    && Enum.TryParse<PPEZone>(zoneNames, true, out var zone))
                    ser.FindProperty("zone").enumValueFlag = (int)zone;
                else
                    Debug.LogWarning($"[APD] Zona untuk kategori '{first.kategori}' ({first.id}) tidak cocok dengan enum PPEZone. Cek ZoneMap.");

                // description & image & variantOf sengaja tidak ditimpa
                ser.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(so);
                result[first.id] = so;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[APD] PPE: {created} dibuat, {updated} diupdate di {PpeFolder}");
            return result;
        }

        // ---------- Care cases ----------
        void GenerateCases()
        {
            var ppeMap = GeneratePPE(); // pastikan APD sudah ada
            var rows = ReadRows();

            EnsureFolder(Path.GetDirectoryName(DbPath).Replace('\\', '/'));
            var db = AssetDatabase.LoadAssetAtPath<CareCaseDatabase>(DbPath);
            if (db == null)
            {
                db = CreateInstance<CareCaseDatabase>();
                AssetDatabase.CreateAsset(db, DbPath);
            }

            var ser = new SerializedObject(db);
            Fill(ser.FindProperty("cases"), rows, ppeMap);
            ser.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();

            int maint = db.GetByType(CareCaseType.Maintenance).Count();
            int stor = db.GetByType(CareCaseType.Storage).Count();
            Debug.Log($"[APD] Database: {db.Cases.Count} kasus ({maint} Maintenance, {stor} Storage)");
        }

        static void Fill(SerializedProperty list, IEnumerable<Row> rows,
                         Dictionary<string, PersonalProtectiveEquipment> ppeMap)
        {
            list.ClearArray();
            int i = 0;
            foreach (var r in rows)
            {
                list.InsertArrayElementAtIndex(i);
                var el = list.GetArrayElementAtIndex(i++);

                el.FindPropertyRelative("equipment").objectReferenceValue =
                    ppeMap.TryGetValue(r.id, out var ppe) ? ppe : null;
                el.FindPropertyRelative("condition").stringValue = r.skenario;
                el.FindPropertyRelative("explanation").stringValue = r.penjelasan;

                // Kolom "Jenis Soal": "Perawatan" -> Maintenance, selain itu ("Penyimpanan - benar") -> Storage
                var type = r.jenis.StartsWith("Perawatan", StringComparison.OrdinalIgnoreCase)
                    ? CareCaseType.Maintenance
                    : CareCaseType.Storage;
                var typeProp = el.FindPropertyRelative("caseType");
                typeProp.enumValueIndex = Array.IndexOf(typeProp.enumNames, type.ToString());

                var tags = r.tags.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).ToArray();
                var tagProp = el.FindPropertyRelative("defectTags");
                tagProp.arraySize = tags.Length;
                for (int t = 0; t < tags.Length; t++)
                    tagProp.GetArrayElementAtIndex(t).stringValue = tags[t];

                var dec = el.FindPropertyRelative("correctDecision");
                int idx = Array.IndexOf(dec.enumNames, r.jawaban);
                if (idx < 0) Debug.LogWarning($"[APD] CareDecision '{r.jawaban}' tidak ada di enum ({r.id}: {r.skenario})");
                else dec.enumValueIndex = idx;
            }
        }

        //static void Fill(SerializedProperty list, IEnumerable<Row> rows,
        //                 Dictionary<string, PersonalProtectiveEquipment> ppeMap)
        //{
        //    list.ClearArray();
        //    int i = 0;
        //    foreach (var r in rows)
        //    {
        //        list.InsertArrayElementAtIndex(i);
        //        var el = list.GetArrayElementAtIndex(i++);

        //        el.FindPropertyRelative("equipment").objectReferenceValue =
        //            ppeMap.TryGetValue(r.id, out var ppe) ? ppe : null;
        //        el.FindPropertyRelative("condition").stringValue = r.skenario;
        //        el.FindPropertyRelative("explanation").stringValue = r.penjelasan;

        //        var tags = r.tags.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).ToArray();
        //        var tagProp = el.FindPropertyRelative("defectTags");
        //        tagProp.arraySize = tags.Length;
        //        for (int t = 0; t < tags.Length; t++)
        //            tagProp.GetArrayElementAtIndex(t).stringValue = tags[t];

        //        var dec = el.FindPropertyRelative("correctDecision");
        //        int idx = Array.IndexOf(dec.enumNames, r.jawaban);
        //        if (idx < 0) Debug.LogWarning($"[APD] CareDecision '{r.jawaban}' tidak ada di enum ({r.id}: {r.skenario})");
        //        else dec.enumValueIndex = idx;
        //    }
        //}
    }
}