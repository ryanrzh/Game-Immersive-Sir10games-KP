using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

namespace Game.APD
{
    public class Level4GameMenuUI : MonoBehaviour
    {
        [SerializeField] private UIDocument uIDocument;

        [Header("Pengaturan Game")]
        [SerializeField] private float levelDurationSeconds = 300f;
        private const float KKM = 70f;

        [Header("Pengaturan Bantuan (Hint)")]
        [SerializeField] private int baseHintPenalty = 5;
        [SerializeField] private int hintPenaltyIncrement = 5;

        // Data Management
        private Facility currentFacility;
        private List<CareCase> selectedCases = new List<CareCase>();
        private int currentRoundIndex = 0;
        private int totalRounds = 0;

        // Score Tracking
        private int currentScore = 0;
        private int totalPossibleScore = 0;
        private int hintUseCount = 0;
        private Dictionary<CareCase, CareDecision> userDecisions = new Dictionary<CareCase, CareDecision>();
        private Dictionary<CareCase, bool> hintUsedPerRound = new Dictionary<CareCase, bool>();

        // Timer
        private float timeRemaining;
        private bool isGameActive = false;
        private bool isPaused = false;

        // UI Variables
        private Label timerLabel, lblScore, lblRound;
        private VisualElement apdImage, tagContainer;
        private Label lblCardTitle, lblCondition;
        private Button btnConfirm;

        private Button[] decisionButtons = new Button[4];
        private CareDecision[] currentRoundOptions = new CareDecision[4];
        private CareDecision? currentSelectedDecision = null;

        // Modals
        private VisualElement hintModal, infoModal, resultModal;
        private Label hintMsg;
        private VisualElement root;

        private void Start()
        {
            currentFacility = APDGameManager.Instance.FacilitySelectedData;
            root = uIDocument.rootVisualElement;

            // Bind Top Navbar
            timerLabel = root.Q<Label>("timer-label");
            lblScore = root.Q<Label>("lbl-score");
            lblRound = root.Q<Label>("lbl-round");

            var btnBack = root.Q<Button>("btn-back");
            if (btnBack != null) btnBack.clicked += () => GameManager.LoadScene("01_StartScreen");

            // Bind Info Modal
            infoModal = root.Q<VisualElement>("info-modal");
            root.Q<Button>("btn-info").clicked += OpenInfoModal;
            root.Q<Button>("btn-info-ok").clicked += CloseInfoModal;

            // Bind Hint Modal
            hintModal = root.Q<VisualElement>("hint-modal");
            hintMsg = root.Q<Label>("hint-msg");
            root.Q<Button>("btn-hint").clicked += OnHintButtonClicked;
            root.Q<Button>("btn-hint-cancel").clicked += () => { isPaused = false; hintModal.style.display = DisplayStyle.None; };
            root.Q<Button>("btn-hint-confirm").clicked += ConfirmHint;

            // Bind Main Content
            apdImage = root.Q<VisualElement>("apd-image");
            lblCardTitle = root.Q<Label>("lbl-card-title");
            lblCondition = root.Q<Label>("lbl-condition");
            tagContainer = root.Q<VisualElement>("tag-container");

            btnConfirm = root.Q<Button>("btn-confirm");
            btnConfirm.clicked += SubmitDecision;

            // Bind Decision Buttons (4 Opsi)
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                decisionButtons[i] = root.Q<Button>($"btn-dec-{i}");
                decisionButtons[i].clicked += () => OnDecisionSelected(currentRoundOptions[index], index);
            }

            // Bind Result Modal
            resultModal = root.Q<VisualElement>("result-modal");
            root.Q<Button>("btn-retry").clicked += () => SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            root.Q<Button>("btn-next").clicked += () => Debug.Log("Lanjut Level Berikutnya!");

            SetupGameData();
        }

        private void SetupGameData()
        {
            if (currentFacility.CareCaseDatabase == null || currentFacility.CareCaseDatabase.Cases.Count == 0)
            {
                Debug.LogError("Tidak ada data CareCases di fasilitas ini!");
                return;
            }

            selectedCases = currentFacility.CareCaseDatabase.Cases
                            .OrderBy(x => Random.value)
                            .Take(3)
                            .ToList();

            totalRounds = selectedCases.Count;
            totalPossibleScore = totalRounds * 10;
            currentRoundIndex = 0;
            currentScore = 0;
            timeRemaining = levelDurationSeconds;

            isGameActive = true;
            LoadRound(currentRoundIndex);
        }

        private void Update()
        {
            if (!isGameActive || isPaused) return;

            if (timeRemaining > 0)
            {
                timeRemaining -= Time.deltaTime;
                UpdateTimerUI();

                if (timeRemaining <= 0)
                {
                    timeRemaining = 0;
                    UpdateTimerUI();
                    isGameActive = false;
                    ShowResult();
                }
            }
        }

        private void UpdateTimerUI()
        {
            int m = Mathf.FloorToInt(timeRemaining / 60);
            int s = Mathf.FloorToInt(timeRemaining % 60);
            timerLabel.text = string.Format("{0:00}:{1:00}", m, s);

            // Berubah merah saat waktu kritis (di bawah 30 detik)
            if (timeRemaining <= 30f) timerLabel.style.color = new StyleColor(new Color32(239, 68, 68, 255));
        }

        private void LoadRound(int roundIndex)
        {
            CareCase currentCase = selectedCases[roundIndex];

            lblRound.text = $"{roundIndex + 1}/{totalRounds}";
            lblScore.text = currentScore.ToString();

            lblCardTitle.text = currentCase.Equipment.Name;
            lblCondition.text = currentCase.Condition;

            if (currentCase.CaseImage != null)
                apdImage.style.backgroundImage = new StyleBackground(currentCase.CaseImage);
            else if (currentCase.Equipment.Image != null)
                apdImage.style.backgroundImage = new StyleBackground(currentCase.Equipment.Image);
            else
                apdImage.style.backgroundImage = null;

            // Render Tag Defect (Warna Merah Soft / Alert)
            tagContainer.Clear();
            if (currentCase.DefectTags != null && currentCase.DefectTags.Length > 0)
            {
                foreach (string tag in currentCase.DefectTags)
                {
                    Label tagLbl = new Label(tag);
                    tagLbl.style.backgroundColor = new StyleColor(new Color32(254, 226, 226, 255)); // bg-red-100
                    tagLbl.style.color = new StyleColor(new Color32(185, 28, 28, 255)); // text-red-700
                    tagLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
                    tagLbl.style.paddingLeft = 12; tagLbl.style.paddingRight = 12;
                    tagLbl.style.paddingTop = 6; tagLbl.style.paddingBottom = 6;
                    tagLbl.style.marginRight = 8; tagLbl.style.marginBottom = 8;
                    SetBorderRadius(tagLbl, 8);
                    SetBorderWidth(tagLbl, 1);
                    SetBorderColor(tagLbl, new Color32(252, 165, 165, 255)); // border-red-300
                    tagContainer.Add(tagLbl);
                }
            }

            GenerateOptionsForCurrentCase();

            // Reset UI State (Tombol Konfirmasi Abu-abu)
            currentSelectedDecision = null;
            btnConfirm.SetEnabled(false);
            btnConfirm.style.backgroundColor = new StyleColor(new Color32(226, 232, 240, 255)); // bg-slate-200
            btnConfirm.style.color = new StyleColor(new Color32(148, 163, 184, 255)); // text-slate-400

            root.Q<Button>("btn-hint").text = $"Bantuan (-{GetNextHintPenalty()})";
            root.Q<Button>("btn-hint").SetEnabled(true);
        }

        private void GenerateOptionsForCurrentCase()
        {
            CareCase currentCase = selectedCases[currentRoundIndex];
            CareDecision correctAnswer = currentCase.CorrectDecision;

            CareDecision[] allDecisions = (CareDecision[])System.Enum.GetValues(typeof(CareDecision));
            List<CareDecision> decoys = allDecisions.Where(d => d != correctAnswer).ToList();
            decoys = decoys.OrderBy(x => Random.value).Take(3).ToList();

            List<CareDecision> finalOptions = new List<CareDecision> { correctAnswer };
            finalOptions.AddRange(decoys);
            finalOptions = finalOptions.OrderBy(x => Random.value).ToList();

            for (int i = 0; i < 4; i++)
            {
                currentRoundOptions[i] = finalOptions[i];
                decisionButtons[i].text = finalOptions[i].ToString();

                // Tema Awal Tombol Opsi: Putih dengan border Abu-abu
                decisionButtons[i].style.backgroundColor = new StyleColor(Color.white);
                decisionButtons[i].style.color = new StyleColor(new Color32(51, 65, 85, 255)); // text-slate-700
                SetBorderColor(decisionButtons[i], new Color32(203, 213, 225, 255)); // border-slate-300
                SetBorderWidth(decisionButtons[i], 2);
            }
        }

        private void OnDecisionSelected(CareDecision decision, int index)
        {
            if (!isGameActive || isPaused) return;
            currentSelectedDecision = decision;

            for (int i = 0; i < decisionButtons.Length; i++)
            {
                if (i == index)
                {
                    // Tema Terpilih: Biru Terang sesuai referensi gambar (Blue-600)
                    decisionButtons[i].style.backgroundColor = new StyleColor(new Color32(37, 99, 235, 255));
                    decisionButtons[i].style.color = new StyleColor(Color.white);
                    SetBorderColor(decisionButtons[i], new Color32(37, 99, 235, 255));
                }
                else
                {
                    // Tema Tidak Terpilih: Kembali Putih
                    decisionButtons[i].style.backgroundColor = new StyleColor(Color.white);
                    decisionButtons[i].style.color = new StyleColor(new Color32(51, 65, 85, 255));
                    SetBorderColor(decisionButtons[i], new Color32(203, 213, 225, 255));
                }
            }

            // Aktifkan Tombol Konfirmasi dengan warna Kuning Emas (Amber-400)
            btnConfirm.SetEnabled(true);
            btnConfirm.style.backgroundColor = new StyleColor(new Color32(251, 191, 36, 255));
            btnConfirm.style.color = new StyleColor(new Color32(15, 23, 42, 255)); // text-slate-900
        }

        private void SubmitDecision()
        {
            if (currentSelectedDecision == null) return;

            CareCase currentCase = selectedCases[currentRoundIndex];
            userDecisions.Add(currentCase, currentSelectedDecision.Value);

            bool isCorrect = currentSelectedDecision.Value == currentCase.CorrectDecision;
            if (isCorrect) currentScore += 10;
            else currentScore -= 5;

            currentRoundIndex++;

            if (currentRoundIndex < totalRounds) LoadRound(currentRoundIndex);
            else { isGameActive = false; ShowResult(); }
        }

        private int GetNextHintPenalty() => baseHintPenalty + (hintUseCount * hintPenaltyIncrement);

        private void OnHintButtonClicked()
        {
            if (!isGameActive || isPaused) return;
            hintMsg.text = $"Menggunakan bantuan akan langsung memilihkan jawaban benar dan memotong skor Anda sebesar {GetNextHintPenalty()} poin. Lanjutkan?";
            isPaused = true;
            hintModal.style.display = DisplayStyle.Flex;
        }

        private void ConfirmHint()
        {
            isPaused = false;
            hintModal.style.display = DisplayStyle.None;
            CareCase currentCase = selectedCases[currentRoundIndex];

            int penalty = GetNextHintPenalty();
            currentScore -= penalty;
            hintUseCount++;
            hintUsedPerRound[currentCase] = true;

            int correctIndex = System.Array.IndexOf(currentRoundOptions, currentCase.CorrectDecision);
            OnDecisionSelected(currentCase.CorrectDecision, correctIndex);

            root.Q<Button>("btn-hint").SetEnabled(false);
            lblScore.text = currentScore.ToString();
        }

        private void OpenInfoModal() { isPaused = true; infoModal.style.display = DisplayStyle.Flex; }
        private void CloseInfoModal() { isPaused = false; infoModal.style.display = DisplayStyle.None; }

        private void ShowResult()
        {
            var root = uIDocument.rootVisualElement;
            var detailList = root.Q<ScrollView>("detail-list");
            detailList.Clear();

            int finalBaseScore = Mathf.Max(0, currentScore);
            float percentage = totalPossibleScore > 0 ? ((float)finalBaseScore / totalPossibleScore) * 100f : 0f;
            bool isPassed = percentage >= KKM;

            // Merender Tabel Result
            for (int i = 0; i < selectedCases.Count; i++)
            {
                var cCase = selectedCases[i];
                bool isAnswered = userDecisions.TryGetValue(cCase, out CareDecision userDecision);
                bool isCorrect = isAnswered && userDecision == cCase.CorrectDecision;

                VisualElement row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row; row.style.alignItems = Align.Center;
                row.style.paddingLeft = 12; row.style.paddingRight = 12; row.style.paddingTop = 10; row.style.paddingBottom = 10;
                row.style.borderBottomWidth = 1; row.style.borderBottomColor = new StyleColor(new Color32(226, 232, 240, 255));

                // Selang-seling warna baris (Slate-50)
                if (i % 2 != 0) row.style.backgroundColor = new StyleColor(new Color32(248, 250, 252, 255));

                VisualElement col1 = new VisualElement(); col1.style.width = new Length(40, LengthUnit.Percent); col1.style.flexDirection = FlexDirection.Column;
                col1.Add(new Label($"KASUS {i + 1} | {cCase.Equipment.Name.ToUpper()}") { style = { fontSize = 11, color = Color.gray, unityFontStyleAndWeight = FontStyle.Bold } });

                Label ansLbl = new Label($"Seharusnya: {cCase.CorrectDecision}");
                ansLbl.style.unityFontStyleAndWeight = FontStyle.Bold; ansLbl.style.color = new StyleColor(new Color32(15, 23, 42, 255)); // text-slate-900
                col1.Add(ansLbl);

                Label col2 = new Label(isAnswered ? userDecision.ToString() : "TIDAK DIJAWAB");
                col2.style.width = new Length(25, LengthUnit.Percent); col2.style.unityTextAlign = TextAnchor.MiddleCenter; col2.style.unityFontStyleAndWeight = FontStyle.Bold;
                col2.style.color = isCorrect ? new StyleColor(new Color32(34, 197, 94, 255)) : new StyleColor(new Color32(239, 68, 68, 255));

                int pts = isCorrect ? 10 : (isAnswered ? -5 : 0);
                Label col3 = new Label(pts > 0 ? $"+{pts}" : pts.ToString());
                col3.style.width = new Length(20, LengthUnit.Percent); col3.style.unityTextAlign = TextAnchor.MiddleCenter; col3.style.unityFontStyleAndWeight = FontStyle.Bold;
                col3.style.color = isCorrect ? new StyleColor(new Color32(34, 197, 94, 255)) : new StyleColor(new Color32(239, 68, 68, 255));

                Label col4 = new Label(isCorrect ? "✅" : "❌");
                col4.style.width = new Length(15, LengthUnit.Percent); col4.style.unityTextAlign = TextAnchor.MiddleCenter; col4.style.fontSize = 16;

                row.Add(col1); row.Add(col2); row.Add(col3); row.Add(col4);
                detailList.Add(row);
            }

            int timeBonus = (isPassed && timeRemaining > 0) ? Mathf.FloorToInt(timeRemaining) : 0;
            int finalTotal = currentScore + timeBonus;

            string starText = percentage >= 90f ? "★ ★ ★" : (percentage >= 70f ? "★ ★ ☆" : "★ ☆ ☆");
            int starCount = percentage >= 90f ? 3 : (percentage >= 70f ? 2 : 1);

            // Pastikan tersimpan dengan ID Level 4
            if (APDGameManager.Instance != null)
                APDGameManager.Instance.SavePlayerScore("Level 4", finalTotal, starCount, percentage);

            root.Q<Label>("stars-label").text = starText;
            root.Q<Label>("score-pct").text = $"{Mathf.RoundToInt(percentage)}%";
            root.Q<Label>("score-pct").style.color = isPassed ? new StyleColor(new Color32(37, 99, 235, 255)) : new StyleColor(new Color32(239, 68, 68, 255));

            root.Q<Label>("status-label").text = isPassed ? "(LULUS)" : "(TIDAK LULUS)";
            root.Q<Label>("status-label").style.color = isPassed ? new StyleColor(new Color32(34, 197, 94, 255)) : new StyleColor(new Color32(239, 68, 68, 255));

            root.Q<Label>("summary-base").text = $"Total Poin Jawaban: {currentScore} / {totalPossibleScore}";
            root.Q<Label>("summary-bonus").text = $"Bonus Waktu: +{timeBonus}";
            root.Q<Label>("score-final").text = $"SKOR AKHIR: {finalTotal}";

            root.Q<Button>("btn-next").style.display = isPassed ? DisplayStyle.Flex : DisplayStyle.None;
            resultModal.style.display = DisplayStyle.Flex;
        }

        // Helper Methods untuk UI
        private void SetBorderColor(VisualElement e, Color32 c) { e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = new StyleColor(c); }
        private void SetBorderRadius(VisualElement e, float r) { e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r; }
        private void SetBorderWidth(VisualElement e, float w) { e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = w; }
    }
}