using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

namespace Game.APD
{
    public class Level1GameMenuUI : MonoBehaviour
    {
        [SerializeField] private UIDocument uIDocument;
        [SerializeField] private VisualTreeAsset circlePoint;

        [Header("Data Base")]
        [SerializeField] private List<Hazard> allAvailableHazards;

        [Header("Pengaturan Game")]
        [SerializeField] private float levelDurationSeconds = 330f;
        private const float KKM = 70f; // Kriteria Ketuntasan Minimal

        [Header("Pengaturan Penalti Bantuan (Hint)")]
        [Tooltip("Berapa poin yang dikurangi pada penggunaan hint pertama")]
        [SerializeField] private int baseHintPenalty = 5;
        [Tooltip("Penambahan poin yang dikurangi untuk hint selanjutnya")]
        [SerializeField] private int hintPenaltyIncrement = 5;

        private Facility currentFacility;
        private Facility.HazardPoint activePoint;

        private Dictionary<Facility.HazardPoint, Hazard> userAnswers = new Dictionary<Facility.HazardPoint, Hazard>();
        private Dictionary<Facility.HazardPoint, VisualElement> pointElements = new Dictionary<Facility.HazardPoint, VisualElement>();

        // Merekam titik mana yang menggunakan hint beserta besar penaltinya
        private Dictionary<Facility.HazardPoint, int> hintsUsedOn = new Dictionary<Facility.HazardPoint, int>();

        private float timeRemaining;
        private bool isGameActive = false;
        private bool isPaused = false; // Status untuk pause timer saat buka info

        // Referensi UI Toolkit
        private Label timerLabel;
        private VisualElement sidePanel;
        private Button[] optionButtons = new Button[4];
        private Hazard[] currentOptions = new Hazard[4];
        private Button btnHint;
        private Button btnSubmit;

        // Referensi Pop-up Hint
        private VisualElement hintModal;
        private Label hintMsg;
        private Button btnHintConfirm;
        private Button btnHintCancel;

        // Referensi Info Modal
        private Button btnInfo;
        private VisualElement infoModal;
        private Button btnInfoClose;
        private Button btnInfoOk;

        // Referensi Result Modal
        private VisualElement resultModal;
        private Label resultTitleText;
        private Label starsLabel;
        private Label scorePctText;
        private Label statusLabel;
        private Label summaryBaseText;
        private Label summaryBonusText;
        private Label scoreFinalText;
        private ScrollView detailList;
        private Button btnRetry;
        private Button btnNext;

        private void Start()
        {
            currentFacility = APDGameManager.Instance.FacilitySelectedData;
            var root = uIDocument.rootVisualElement;
            var wallpaper = root.Q<VisualElement>("wallpaper");
            wallpaper.style.backgroundImage = new StyleBackground(currentFacility.Image);

            timerLabel = root.Q<Label>("timer-label");
            sidePanel = root.Q<VisualElement>("side-panel");

            for (int i = 0; i < 4; i++)
            {
                int index = i;
                optionButtons[index] = root.Q<Button>($"btn-opt-{index}");
                optionButtons[index].clicked += () => OnOptionSelected(index);
            }

            // Bind Hint & Pop-up Hint
            btnHint = root.Q<Button>("btn-hint");
            btnHint.clicked += OnHintButtonClicked;

            hintModal = root.Q<VisualElement>("hint-modal");
            hintMsg = root.Q<Label>("hint-msg");
            btnHintConfirm = root.Q<Button>("btn-hint-confirm");
            btnHintCancel = root.Q<Button>("btn-hint-cancel");

            btnHintConfirm.clicked += ConfirmHint;
            btnHintCancel.clicked += () => hintModal.style.display = DisplayStyle.None;

            // Bind Info Modal & Tombol '?'
            btnInfo = root.Q<Button>("btn-info");
            infoModal = root.Q<VisualElement>("info-modal");
            btnInfoClose = root.Q<Button>("btn-info-close");
            btnInfoOk = root.Q<Button>("btn-info-ok");

            btnInfo.clicked += OpenInfoModal;
            btnInfoClose.clicked += CloseInfoModal;
            btnInfoOk.clicked += CloseInfoModal;

            btnSubmit = root.Q<Button>("btn-submit");
            btnSubmit.clicked += FinishGame;

            // Bind Modal Result
            resultModal = root.Q<VisualElement>("result-modal");
            resultTitleText = root.Q<Label>("result-title");
            starsLabel = root.Q<Label>("stars-label");
            scorePctText = root.Q<Label>("score-pct");
            statusLabel = root.Q<Label>("status-label");
            summaryBaseText = root.Q<Label>("summary-base");
            summaryBonusText = root.Q<Label>("summary-bonus");
            scoreFinalText = root.Q<Label>("score-final");
            detailList = root.Q<ScrollView>("detail-list");

            btnRetry = root.Q<Button>("btn-retry");
            btnNext = root.Q<Button>("btn-next");

            btnRetry.clicked += () => SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            btnNext.clicked += () => Debug.Log("Memuat Level Selanjutnya...");

            // Render Titik
            foreach (var point in currentFacility.points)
            {
                var ptElement = SetPoint(point.positionPoint);
                ptElement.RegisterCallback<ClickEvent>(evt => OnPointClicked(point, ptElement));
                ptElement.Q<Label>("number").text = (System.Array.IndexOf(currentFacility.points, point) + 1).ToString();
                pointElements.Add(point, ptElement);
                wallpaper.Add(ptElement);
            }

            UpdateHintButtonUI();
            timeRemaining = levelDurationSeconds;
            isGameActive = true;
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
                    FinishGame();
                }
            }
        }

        private void UpdateTimerUI()
        {
            int minutes = Mathf.FloorToInt(timeRemaining / 60);
            int seconds = Mathf.FloorToInt(timeRemaining % 60);
            timerLabel.text = string.Format("{0:00}:{1:00}", minutes, seconds);

            if (timeRemaining <= 30f)
                timerLabel.style.color = new StyleColor(new Color(1f, 0.3f, 0.3f));
        }

        private VisualElement SetPoint(Vector2 pointPos)
        {
            var newCN = circlePoint.Instantiate();
            var pointElement = newCN.Q<VisualElement>("point");

            pointElement.style.position = Position.Absolute;
            pointElement.style.bottom = new StyleLength(new Length(50 + pointPos.y, LengthUnit.Percent));
            pointElement.style.left = new StyleLength(new Length(50 + pointPos.x, LengthUnit.Percent));
            pointElement.style.translate = new StyleTranslate(new Translate(
                new Length(-50, LengthUnit.Percent),
                new Length(-50, LengthUnit.Percent),
                0
            ));

            return pointElement;
        }

        private void OnPointClicked(Facility.HazardPoint point, VisualElement ptElement)
        {
            if (!isGameActive || isPaused) return;
            activePoint = point;
            sidePanel.style.display = DisplayStyle.Flex;

            foreach (var el in pointElements.Values)
                el.style.scale = new StyleScale(new Scale(new Vector3(1f, 1f, 1f)));

            ptElement.style.scale = new StyleScale(new Scale(new Vector3(1.3f, 1.3f, 1f)));
            GenerateOptionsForActivePoint();
        }

        private void GenerateOptionsForActivePoint()
        {
            var correctHazard = activePoint.hazards;
            var optionsList = new List<Hazard> { correctHazard };

            var decoys = allAvailableHazards
                .Where(h => h != correctHazard)
                .OrderBy(x => Random.value)
                .Take(3).ToList();

            optionsList.AddRange(decoys);
            optionsList = optionsList.OrderBy(x => Random.value).ToList();

            bool hasAnswered = userAnswers.ContainsKey(activePoint);
            Hazard savedAnswer = hasAnswered ? userAnswers[activePoint] : null;

            for (int i = 0; i < 4; i++)
            {
                currentOptions[i] = optionsList[i];
                optionButtons[i].text = optionsList[i].Name;
                optionButtons[i].style.borderBottomColor = new StyleColor(Color.clear);
                optionButtons[i].style.borderBottomWidth = 0;

                if (hasAnswered && currentOptions[i] == savedAnswer)
                    HighlightSelectedButton(i);
            }
            btnHint.SetEnabled(!hintsUsedOn.ContainsKey(activePoint));
        }

        // --- SISTEM HINT & POP-UP ---

        private int GetNextHintPenalty()
        {
            return baseHintPenalty + (hintsUsedOn.Count * hintPenaltyIncrement);
        }

        private void UpdateHintButtonUI()
        {
            btnHint.text = $"💡 Gunakan Bantuan (-{GetNextHintPenalty()} Poin)";
        }

        private void OnHintButtonClicked()
        {
            if (activePoint == null || hintsUsedOn.ContainsKey(activePoint) || !isGameActive) return;

            int nextPenalty = GetNextHintPenalty();
            hintMsg.text = $"Bantuan akan memberikan jawaban benar, namun persentase KKM dan poin akan dikurangi sebesar {nextPenalty} Poin.\n\nLanjutkan?";
            hintModal.style.display = DisplayStyle.Flex;
        }

        private void ConfirmHint()
        {
            hintModal.style.display = DisplayStyle.None;
            if (activePoint == null || !isGameActive) return;

            int penaltyToApply = GetNextHintPenalty();
            hintsUsedOn.Add(activePoint, penaltyToApply);

            for (int i = 0; i < 4; i++)
            {
                if (currentOptions[i] == activePoint.hazards)
                {
                    OnOptionSelected(i);
                    break;
                }
            }

            UpdateHintButtonUI();
            btnHint.SetEnabled(false);
        }

        // --- SISTEM INFO (PAUSE) ---

        private void OpenInfoModal()
        {
            if (!isGameActive) return;
            isPaused = true;
            infoModal.style.display = DisplayStyle.Flex;
        }

        private void CloseInfoModal()
        {
            isPaused = false;
            infoModal.style.display = DisplayStyle.None;
        }

        // ---------------------------

        private void OnOptionSelected(int optionIndex)
        {
            if (activePoint == null || !isGameActive || isPaused) return;

            Hazard selectedHazard = currentOptions[optionIndex];
            if (userAnswers.ContainsKey(activePoint))
                userAnswers[activePoint] = selectedHazard;
            else
                userAnswers.Add(activePoint, selectedHazard);

            HighlightSelectedButton(optionIndex);
            pointElements[activePoint].style.backgroundColor = new StyleColor(new Color(0.2f, 0.8f, 0.2f));
            CheckAllAnswersFilled();
        }

        private void HighlightSelectedButton(int index)
        {
            for (int i = 0; i < 4; i++)
            {
                if (i == index)
                {
                    optionButtons[i].style.borderBottomColor = new StyleColor(Color.blue);
                    optionButtons[i].style.borderBottomWidth = 3;
                }
                else
                {
                    optionButtons[i].style.borderBottomColor = new StyleColor(Color.clear);
                    optionButtons[i].style.borderBottomWidth = 0;
                }
            }
        }

        private void CheckAllAnswersFilled()
        {
            if (userAnswers.Count >= currentFacility.points.Length)
                btnSubmit.style.display = DisplayStyle.Flex;
        }

        private void FinishGame()
        {
            isGameActive = false;
            sidePanel.style.display = DisplayStyle.None;

            int basePointsEarned = 0;
            detailList.Clear();

            int maxPossiblePoints = currentFacility.points.Length * 10;

            for (int i = 0; i < currentFacility.points.Length; i++)
            {
                var pt = currentFacility.points[i];
                bool isAnswered = userAnswers.TryGetValue(pt, out Hazard answer);
                bool isCorrect = isAnswered && answer == pt.hazards;
                bool usedHint = hintsUsedOn.TryGetValue(pt, out int hintPenalty);

                int gainedPts = isCorrect ? 10 : 0;
                int minusPts = (isAnswered && !isCorrect ? -5 : 0) + (usedHint ? -hintPenalty : 0);

                basePointsEarned += (gainedPts + minusPts);

                // ==== RENDER BARIS TABEL (Rapi dengan % width) ====
                VisualElement row = new VisualElement();
                row.AddToClassList("flex-row");
                row.AddToClassList("items-center");
                row.AddToClassList("p-3");
                row.AddToClassList("border-b");
                row.AddToClassList("border-slate-200");

                if (i % 2 != 0) row.style.backgroundColor = new StyleColor(new Color(0.96f, 0.97f, 0.98f));

                // 1. Kolom Info (40%)
                VisualElement col1 = new VisualElement();
                col1.style.width = new Length(40, LengthUnit.Percent);
                col1.AddToClassList("flex-col");

                col1.Add(new Label($"Titik {i + 1} ({pt.hazards.Name})") { style = { fontSize = 11, color = Color.gray } });
                Label ansLbl = new Label(isAnswered ? answer.Name : "- Tidak Dijawab -");
                ansLbl.AddToClassList("font-bold");
                ansLbl.AddToClassList(isCorrect ? "text-green-600" : (isAnswered ? "text-red-500" : "text-slate-400"));
                ansLbl.style.whiteSpace = WhiteSpace.Normal;
                col1.Add(ansLbl);

                // 2. Kolom Hint (15%)
                Label col2 = new Label(usedHint ? "Ya" : "-");
                col2.style.width = new Length(15, LengthUnit.Percent);
                col2.style.unityTextAlign = TextAnchor.MiddleCenter;
                col2.AddToClassList("font-bold");
                if (usedHint) col2.AddToClassList("text-amber-500"); else col2.AddToClassList("text-slate-400");

                // 3. Kolom Plus (15%)
                Label col3 = new Label(gainedPts > 0 ? $"+{gainedPts}" : "0");
                col3.style.width = new Length(15, LengthUnit.Percent);
                col3.style.unityTextAlign = TextAnchor.MiddleCenter;
                col3.AddToClassList("font-bold");
                col3.AddToClassList("text-green-600");

                // 4. Kolom Minus (15%)
                Label col4 = new Label(minusPts < 0 ? $"{minusPts}" : "0");
                col4.style.width = new Length(15, LengthUnit.Percent);
                col4.style.unityTextAlign = TextAnchor.MiddleCenter;
                col4.AddToClassList("font-bold");
                col4.AddToClassList(minusPts < 0 ? "text-red-500" : "text-slate-400");

                // 5. Kolom Status (15%)
                Label col5 = new Label(isCorrect ? "✅" : (isAnswered ? "❌" : "⚠️"));
                col5.style.width = new Length(15, LengthUnit.Percent);
                col5.style.unityTextAlign = TextAnchor.MiddleCenter;
                col5.style.fontSize = 16;

                row.Add(col1); row.Add(col2); row.Add(col3); row.Add(col4); row.Add(col5);
                detailList.Add(row);
            }

            // --- KALKULASI KKM BERBASIS POIN ---
            int finalBaseScore = Mathf.Max(0, basePointsEarned);
            float percentage = maxPossiblePoints > 0 ? ((float)finalBaseScore / maxPossiblePoints) * 100f : 0f;
            bool isPassed = percentage >= KKM;

            // Logika Bintang
            string starText = "☆ ☆ ☆";
            if (percentage >= 90f) starText = "★ ★ ★";
            else if (percentage >= 70f) starText = "★ ★ ☆";
            else if (percentage >= 40f) starText = "★ ☆ ☆";
            starsLabel.text = starText;

            // Hitung Bonus Waktu
            int timeBonus = (isPassed && timeRemaining > 0) ? Mathf.FloorToInt(timeRemaining) : 0;
            int finalTotal = basePointsEarned + timeBonus;

            APDGameManager.Instance.SavePlayerScore("level_1", finalTotal, percentage >= 90f? 3: percentage >= 70f? 2: 1, percentage);

            // Update UI Result Modal
            resultModal.style.display = DisplayStyle.Flex;
            resultTitleText.text = timeRemaining <= 0 ? "WAKTU HABIS!" : "EVALUASI LEVEL 1";

            scorePctText.text = $"{Mathf.RoundToInt(percentage)}%";
            scorePctText.style.color = isPassed ? new StyleColor(new Color(0.1f, 0.4f, 0.8f)) : new StyleColor(new Color(0.9f, 0.2f, 0.2f));

            statusLabel.text = isPassed ? "(LULUS)" : "(TIDAK LULUS)";
            statusLabel.style.color = isPassed ? new StyleColor(new Color(0.1f, 0.7f, 0.1f)) : new StyleColor(new Color(0.9f, 0.2f, 0.2f));

            summaryBaseText.text = $"Total Poin Jawaban: {basePointsEarned} / {maxPossiblePoints}";
            summaryBonusText.text = $"Bonus Waktu ({Mathf.FloorToInt(timeRemaining)} detik): +{timeBonus}";
            scoreFinalText.text = $"SKOR AKHIR: {finalTotal}";

            btnNext.style.display = isPassed ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}