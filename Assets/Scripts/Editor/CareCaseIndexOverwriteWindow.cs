#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game.APD;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Overwrites CareCase fields by index, preserving existing caseImage and equipment.
/// Accepts either the supplied Excel "Studi Kasus" CSV or the included normalized CSV.
/// </summary>
public class CareCaseIndexOverwriteWindow : EditorWindow
{
    private sealed class ImportedRow
    {
        public string equipmentId, caseKey, condition, defectTags, explanation, part, answerLabel;
        public CareDecision decision;
        public CareCaseType caseType;
    }

    private static readonly HashSet<string> StorageDecisions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "TempatSejukKering", "JauhSinarMatahari", "JauhBahanKimia", "CaseMicrofiber",
        "DatarAtauGantung", "GantungHarness", "GantungKeringTanpaLipatan", "WadahTertutup",
        "KemasanAsli", "RakTegakSepatu", "BerdiriAtauDigantungBoot", "KotakTasGantunganSuit",
        "DispenserDekatHazardSign", "GulungKering", "TempatBersihKeringTerpisah",
        "DashboardMobilTerik", "SinarMatahariLangsung", "DekatBahanKimia",
        "DitumpukDitindih", "DilemparDiTumpukanAlat", "DilipatTajam",
        "LembapBasah", "DiLantaiTerbuka", "LensaMenghadapBawah", "DekatSumberPanas"
    };

    private CareCaseDatabase database;
    private TextAsset csv;
    private readonly List<ImportedRow> preview = new List<ImportedRow>();
    private string feedback = "Pilih asset database dan CSV, kemudian klik Periksa.";
    private bool validated;
    private string validatedCsv;
    private int validatedCount;
    private string validatedDatabasePath;
    private Vector2 scroll;

    [MenuItem("Tools/Game APD/Timpa Data Level 4 (Pertahankan Gambar)")]
    public static void Open()
    {
        var window = GetWindow<CareCaseIndexOverwriteWindow>("Timpa Data Level 4");
        window.minSize = new Vector2(650, 460);
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Excel baris 2 -> cases[0], baris 3 -> cases[1], dst. Sprite caseImage dan equipment lama TIDAK DIUBAH. Mendukung CSV UTF-8 dari sheet Studi Kasus ataupun CSV yang disertakan.", MessageType.Info);
        EditorGUI.BeginChangeCheck();
        database = (CareCaseDatabase)EditorGUILayout.ObjectField("Database LAMA", database, typeof(CareCaseDatabase), false);
        csv = (TextAsset)EditorGUILayout.ObjectField("CSV data terbaru", csv, typeof(TextAsset), false);
        if (EditorGUI.EndChangeCheck()) ResetValidation();

        EditorGUILayout.Space(8);
        if (GUILayout.Button("1. Periksa CSV & Database", GUILayout.Height(36))) Validate();
        EditorGUILayout.HelpBox(feedback, validated ? MessageType.Info : MessageType.Warning);
        using (new EditorGUI.DisabledScope(!validated))
        {
            if (GUILayout.Button("2. BACKUP & TIMPA DATA BERDASARKAN INDEX", GUILayout.Height(40))) Apply();
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Pratinjau:", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int i = 0; i < Math.Min(8, preview.Count); i++)
            EditorGUILayout.LabelField("cases[" + i + "]  " + preview[i].equipmentId + "  -> " + preview[i].decision);
        if (preview.Count > 8) EditorGUILayout.LabelField("... hingga cases[" + (preview.Count - 1) + "]");
        EditorGUILayout.EndScrollView();
    }

    private void ResetValidation()
    {
        validated = false;
        preview.Clear();
        validatedCsv = null;
        validatedDatabasePath = null;
        validatedCount = 0;
    }

    private void Validate()
    {
        ResetValidation();
        if (database == null || csv == null)
        {
            feedback = "Pilih CareCaseDatabase.asset dan file CSV terlebih dahulu.";
            return;
        }
        try
        {
            List<List<string>> table = ParseCsv(csv.text);
            if (table.Count < 2) throw new Exception("CSV tidak memiliki baris data.");
            List<string> header = table[0].Select(NormalizeHeader).ToList();
            bool normalized = header.Contains("equipmentid") && header.Contains("casekey") && header.Contains("decision");
            bool excel = header.Contains("idapd") && header.Contains("skenariodeskripsikasus") && header.Contains("jawabanbenarcaredecision");
            if (!normalized && !excel)
            {
                throw new Exception("Header CSV tidak dikenal. Pilih sheet 'Studi Kasus' (bukan Rekap/CareDecision) saat Save As CSV UTF-8, atau gunakan Level4_Skenario_317.csv dari ZIP. Header terbaca: " + string.Join(" | ", table[0].Take(5).ToArray()));
            }

            for (int i = 1; i < table.Count; i++)
            {
                var cells = table[i];
                Func<string, string> get = key =>
                {
                    int pos = header.IndexOf(NormalizeHeader(key));
                    return pos < 0 || pos >= cells.Count ? "" : (cells[pos] ?? "").Trim();
                };
                ImportedRow r = new ImportedRow();
                string decisionText;
                string typeText;
                if (normalized)
                {
                    r.equipmentId = get("equipmentId");
                    r.caseKey = get("caseKey");
                    r.condition = get("condition");
                    r.defectTags = get("defectTags");
                    decisionText = get("decision");
                    typeText = get("caseType");
                    r.explanation = get("explanation");
                    r.part = get("part");
                    r.answerLabel = get("answerLabel");
                }
                else
                {
                    r.equipmentId = get("ID APD");
                    r.caseKey = r.equipmentId + "#" + (i).ToString("D3");
                    r.condition = get("Skenario / Deskripsi Kasus");
                    r.defectTags = get("Tag (defectTags)");
                    decisionText = get("Jawaban Benar (CareDecision)");
                    typeText = get("Jenis Soal");
                    r.explanation = get("Penjelasan");
                    r.part = get("Bagian yang Dimaksud");
                    r.answerLabel = get("Jawaban Tampil (label)");
                }
                if (string.IsNullOrWhiteSpace(r.condition) || string.IsNullOrWhiteSpace(decisionText))
                    throw new Exception("Baris CSV " + (i + 1) + " kehilangan skenario atau jawaban. Import dibatalkan demi menjaga indeks.");
                CareDecision parsedDecision;
                if (!Enum.TryParse(decisionText, false, out parsedDecision) || !Enum.IsDefined(typeof(CareDecision), parsedDecision))
                    throw new Exception("CareDecision '" + decisionText + "' pada baris " + (i + 1) + " tidak ada dalam CareCase.cs. Perbarui enum terlebih dahulu.");
                r.decision = parsedDecision;
                if (string.IsNullOrWhiteSpace(typeText))
                    r.caseType = StorageDecisions.Contains(decisionText) ? CareCaseType.Storage : CareCaseType.Maintenance;
                else
                {
                    CareCaseType parsedType;
                    if (string.Equals(typeText, "Perawatan", StringComparison.OrdinalIgnoreCase)) typeText = "Maintenance";
                    if (string.Equals(typeText, "Penyimpanan", StringComparison.OrdinalIgnoreCase)) typeText = "Storage";
                    if (!Enum.TryParse(typeText, true, out parsedType) || !Enum.IsDefined(typeof(CareCaseType), parsedType))
                        throw new Exception("Jenis Soal '" + typeText + "' tidak dikenal pada baris " + (i + 1));
                    r.caseType = parsedType;
                }
                preview.Add(r);
            }

            // Fixed expected scenario count prevents accidental import of another sheet / partial CSV.
            if (preview.Count != 317) throw new Exception("Jumlah baris skenario " + preview.Count + ", bukan 317. Periksa sheet dan pastikan CSV tidak terpotong.");
            if (database.Cases.Count < preview.Count)
                throw new Exception("Database lama hanya " + database.Cases.Count + " kasus, membutuhkan minimum " + preview.Count + ". Tidak membuat slot baru karena gambar perlu dipertahankan.");

            string dbPath = AssetDatabase.GetAssetPath(database);
            if (string.IsNullOrEmpty(dbPath) || !dbPath.StartsWith("Assets/"))
                throw new Exception("Database bukan asset .asset yang tersimpan di folder Assets.");

            validated = true;
            validatedCsv = csv.text;
            validatedCount = database.Cases.Count;
            validatedDatabasePath = dbPath;
            feedback = "VALID: " + preview.Count + " skenario; " + database.Cases.Count + " kasus lama. Import cases[0..316]. Equipment dan Sprite tetap, semua data teks dan keputusan diperbarui. " +
                       (normalized ? "Format CSV internal terdeteksi." : "Format CSV Excel 'Studi Kasus' terdeteksi.") +
                       " Kasus ekstra setelah indeks 316 tidak diubah. BACKUP otomatis sebelum import. Pastikan urutan gambar lama cocok dengan urutan Excel.";
        }
        catch (Exception e)
        {
            ResetValidation();
            feedback = "Gagal validasi: " + e.Message;
        }
    }

    private void Apply()
    {
        if (!validated || database == null || csv == null) return;
        if (validatedCsv != csv.text || validatedCount != database.Cases.Count || validatedDatabasePath != AssetDatabase.GetAssetPath(database))
        {
            ResetValidation();
            feedback = "Data CSV atau database berubah. Klik Periksa ulang.";
            return;
        }
        if (!EditorUtility.DisplayDialog("Konfirmasi overwrite", "Timpa data 317 kasus berdasarkan indeks, TANPA mengubah caseImage dan equipment?\n\nBackup .asset akan dibuat terlebih dahulu. Pastikan gambar pada setiap indeks sesuai dengan kasus baru.", "Backup & Timpa", "Batal")) return;
        const string backupFolder = "Assets/CareCaseBackups";
        if (!AssetDatabase.IsValidFolder(backupFolder)) AssetDatabase.CreateFolder("Assets", "CareCaseBackups");
        string path = validatedDatabasePath;
        string backupPath = AssetDatabase.GenerateUniqueAssetPath(backupFolder + "/" + Path.GetFileNameWithoutExtension(path) + "_Backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".asset");
        AssetDatabase.SaveAssets();
        if (!AssetDatabase.CopyAsset(path, backupPath))
        {
            feedback = "Gagal membuat backup. Tidak ada data yang ditimpa.";
            return;
        }
        try
        {
            var serialized = new SerializedObject(database);
            SerializedProperty cases = serialized.FindProperty("cases");
            if (cases == null || !cases.isArray || cases.arraySize < preview.Count)
                throw new Exception("Field cases tidak ditemukan atau jumlahnya berubah.");
            // Preflight: only the original CareCase fields are required.
            // Optional metadata (sourceCaseKey, part, answerLabel) may not exist in older CareCase.cs.
            string[] fields = { "caseType", "correctDecision", "condition", "explanation", "defectTags" };
            for (int i = 0; i < preview.Count; i++)
            {
                SerializedProperty item = cases.GetArrayElementAtIndex(i);
                foreach (string f in fields)
                    if (item.FindPropertyRelative(f) == null)
                        throw new Exception("Field '" + f + "' belum ada di CareCase.cs (indeks " + i + "). Gunakan versi CareCase.cs dari ZIP.");
            }
            Undo.RecordObject(database, "Timpa Data Level 4 (Sprite Tetap)");
            for (int i = 0; i < preview.Count; i++)
            {
                SerializedProperty item = cases.GetArrayElementAtIndex(i);
                ImportedRow r = preview[i];
                item.FindPropertyRelative("caseType").intValue = (int)r.caseType;
                item.FindPropertyRelative("correctDecision").intValue = (int)r.decision;
                item.FindPropertyRelative("condition").stringValue = r.condition;
                item.FindPropertyRelative("explanation").stringValue = r.explanation;
                // Set extra metadata only when the current CareCase.cs defines these fields.
                SerializedProperty keyProperty = item.FindPropertyRelative("sourceCaseKey");
                if (keyProperty != null) keyProperty.stringValue = r.caseKey;
                SerializedProperty partProperty = item.FindPropertyRelative("part");
                if (partProperty != null) partProperty.stringValue = r.part;
                SerializedProperty answerProperty = item.FindPropertyRelative("answerLabel");
                if (answerProperty != null) answerProperty.stringValue = r.answerLabel;
                SerializedProperty tags = item.FindPropertyRelative("defectTags");
                string[] split = (r.defectTags ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                tags.arraySize = split.Length;
                for (int t = 0; t < split.Length; t++) tags.GetArrayElementAtIndex(t).stringValue = split[t].Trim();
                // Never touch fields caseImage or equipment.
            }
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            feedback = "BERHASIL: 317 kasus ditimpa berdasarkan indeks. Sprite & equipment tidak diubah. Backup: " + backupPath;
            Debug.Log(feedback, database);
            ResetValidationPreserveFeedback();
        }
        catch (Exception ex)
        {
            feedback = "Import gagal: " + ex.Message + " | Backup tersimpan: " + backupPath + ". Jika perlu pulihkan melalui backup.";
            Debug.LogError(feedback, database);
            ResetValidationPreserveFeedback();
        }
    }

    private void ResetValidationPreserveFeedback()
    {
        validated = false;
        preview.Clear();
        validatedCsv = null;
        validatedDatabasePath = null;
    }

    private static string NormalizeHeader(string s)
    {
        return new string((s ?? "").Trim().TrimStart('\uFEFF').Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }

    private static List<List<string>> ParseCsv(string source)
    {
        source = (source ?? "").TrimStart('\uFEFF');
        // Detect field separator from first physical header line (no quoted headers in the supported files).
        string firstLine = source.Split(new[] { '\r', '\n' }, 2)[0];
        char delimiter = firstLine.Count(x => x == ';') > firstLine.Count(x => x == ',') ? ';' : ',';
        var result = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < source.Length; i++)
        {
            char ch = source[i];
            if (ch == '"')
            {
                if (quoted && i + 1 < source.Length && source[i + 1] == '"') { field.Append('"'); i++; }
                else quoted = !quoted;
            }
            else if (ch == delimiter && !quoted)
            {
                row.Add(field.ToString()); field.Length = 0;
            }
            else if ((ch == '\n' || ch == '\r') && !quoted)
            {
                if (ch == '\r' && i + 1 < source.Length && source[i + 1] == '\n') i++;
                row.Add(field.ToString()); field.Length = 0;
                if (row.Any(x => !string.IsNullOrWhiteSpace(x))) result.Add(row);
                row = new List<string>();
            }
            else field.Append(ch);
        }
        if (quoted) throw new Exception("CSV memiliki tanda kutip yang tidak ditutup.");
        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            if (row.Any(x => !string.IsNullOrWhiteSpace(x))) result.Add(row);
        }
        return result;
    }
}
#endif
