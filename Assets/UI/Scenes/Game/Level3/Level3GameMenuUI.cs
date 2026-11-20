using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

namespace Game.APD
{
    public class Level3GameMenuUI : MonoBehaviour
    {
        [SerializeField] private UIDocument uIDocument;

        [Header("Pengaturan Game")]
        [SerializeField] private float levelDurationSeconds = 300f;
        private const float KKM = 70f;
        [SerializeField] private int hintPenalty = 5;

        private class WornItem
        {
            public PPEZone Zone;
            public PersonalProtectiveEquipment Equipment;
            public bool IsActuallyWrong;
            public bool IsMarkedByUser;
        }

        //private class InspectionRecord
        //{
        //    public int RoundNumber;
        //    public string ItemName;
        //    public bool IsActuallyWrong;
        //    public bool IsMarkedByUser;
        //    public int PointsGained;
        //}

        private Facility currentFacility;
        private List<Facility> roundFacilities = new List<Facility>();
        private int currentRoundIndex = 0;

        private List<WornItem> currentWornPPE = new List<WornItem>();
        private List<InspectionRecord> inspectionHistory = new List<InspectionRecord>();

        private Dictionary<PPEZone, VisualElement> listRows = new Dictionary<PPEZone, VisualElement>();

        private int currentScore = 0;
        private float timeRemaining;
        private bool isGameActive = false;
        private bool isPaused = false;

        private Label timerLabel, lblScore, lblRound, lblFacilityName, lblScenario, lblMarkedCount;
        private VisualElement hazardTagsContainer;
        private Button btnSubmit, btnHint, btnInfo;
        private ScrollView inspectionList;

        // Modals
        private VisualElement hintModal, infoModal, resultModal, ppeModal;
        private ScrollView detailList;
        private VisualElement ppeModalImg;
        private Label ppeModalName, ppeModalDesc;

        private void Start()
        {
            // Asumsi: Ambil data fasilitas yang dipilih (Untuk production, gunakan array/list dari Plant)
            if (APDGameManager.Instance != null && APDGameManager.Instance.FacilitySelectedData != null)
                roundFacilities.Add(APDGameManager.Instance.FacilitySelectedData);

            var root = uIDocument.rootVisualElement;
            timerLabel = root.Q<Label>("timer-label");
            lblScore = root.Q<Label>("lbl-score");
            lblRound = root.Q<Label>("lbl-round");
            lblFacilityName = root.Q<Label>("lbl-facility-name");
            lblScenario = root.Q<Label>("lbl-scenario");
            lblMarkedCount = root.Q<Label>("lbl-marked-count");
            hazardTagsContainer = root.Q<VisualElement>("hazard-tags-container");
            inspectionList = root.Q<ScrollView>("inspection-list");

            btnSubmit = root.Q<Button>("btn-submit");
            btnSubmit.clicked += CheckInspectionResults;

            hintModal = root.Q<VisualElement>("hint-modal");
            infoModal = root.Q<VisualElement>("info-modal");
            resultModal = root.Q<VisualElement>("result-modal");
            detailList = root.Q<ScrollView>("detail-list");

            // --- BINDING PPE MODAL ---
            ppeModal = root.Q<VisualElement>("ppe-modal");
            ppeModalImg = root.Q<VisualElement>("ppe-modal-img");
            ppeModalName = root.Q<Label>("ppe-modal-name");
            ppeModalDesc = root.Q<Label>("ppe-modal-desc");

            // 2. Fitur Tutup saat Klik Area Kosong (Background Gelap)
            ppeModal.RegisterCallback<ClickEvent>(evt =>
            {
                // Pastikan yang diklik benar-benar background 'ppeModal', BUKAN elemen di dalamnya (seperti kotak putih/teks)
                if (evt.target == ppeModal)
                {
                    ClosePPEModal();
                }
            });

            // Events Modal Lainnya
            btnInfo = root.Q<Button>("btn-info");
            btnInfo.clicked += OpenInfoModal;
            root.Q<Button>("btn-info-ok").clicked += CloseInfoModal;

            btnHint = root.Q<Button>("btn-hint");
            btnHint.clicked += OpenHintModal;
            root.Q<Button>("btn-hint-cancel").clicked += CloseHintModal;
            root.Q<Button>("btn-hint-confirm").clicked += ConfirmHint;

            root.Q<Button>("btn-retry").clicked += () => SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            root.Q<Button>("btn-next").clicked += () => Debug.Log("Melanjutkan ke modul berikutnya...");

            currentScore = 0;
            inspectionHistory.Clear();
            timeRemaining = levelDurationSeconds;
            isGameActive = true;
            isPaused = false;

            if (roundFacilities.Count > 0)
                LoadRound(0);
        }

        private void Update()
        {
            if (!isGameActive || isPaused) return;

            if (timeRemaining > 0)
            {
                timeRemaining -= Time.deltaTime;
                UpdateTimerUI();
                if (timeRemaining <= 0) FinishGame();
            }
        }

        private void UpdateTimerUI()
        {
            int m = Mathf.FloorToInt(timeRemaining / 60);
            int s = Mathf.FloorToInt(timeRemaining % 60);
            timerLabel.text = string.Format("{0:00}:{1:00}", m, s);
        }

        private void OpenInfoModal() { isPaused = true; infoModal.style.display = DisplayStyle.Flex; }
        private void CloseInfoModal() { isPaused = false; infoModal.style.display = DisplayStyle.None; }
        private void OpenHintModal() { if (!isGameActive) return; isPaused = true; hintModal.style.display = DisplayStyle.Flex; }
        private void CloseHintModal() { isPaused = false; hintModal.style.display = DisplayStyle.None; }

        private void ConfirmHint()
        {
            CloseHintModal();
            var itemToHint = currentWornPPE.FirstOrDefault(w => w.IsActuallyWrong && !w.IsMarkedByUser);

            if (itemToHint != null)
            {
                itemToHint.IsMarkedByUser = true;
                currentScore -= hintPenalty;
                lblScore.text = $"Total Skor: {currentScore}";

                BuildInspectionList();
                UpdateMarkedCount();
            }
        }

        // === FUNGSI BUKA TUTUP PPE MODAL (TANPA PAUSE) ===
        private void OpenPPEModal(PersonalProtectiveEquipment ppe)
        {
            if (ppe == null) return;

            ppeModalName.text = ppe.Name;
            ppeModalDesc.text = string.IsNullOrEmpty(ppe.Description) ? "Tidak ada deskripsi detail." : ppe.Description;

            if (ppe.Image != null)
                ppeModalImg.style.backgroundImage = new StyleBackground(ppe.Image);
            else
                ppeModalImg.style.backgroundImage = null;

            // Timer dibiarkan tetap berjalan
            ppeModal.style.display = DisplayStyle.Flex;
        }

        private void ClosePPEModal()
        {
            ppeModal.style.display = DisplayStyle.None;
        }

        private void LoadRound(int index)
        {
            currentRoundIndex = index;
            currentFacility = roundFacilities[index];

            lblRound.text = $"Titik {index + 1}/{roundFacilities.Count}";
            lblScore.text = $"Total Skor: {currentScore}";
            lblFacilityName.text = currentFacility.Name;
            lblScenario.text = currentFacility.InspectionScenario;

            RenderHazardTags();
            GenerateRandomizedWornEquipment();
            BuildInspectionList();
            UpdateMarkedCount();
        }

        private void RenderHazardTags()
        {
            hazardTagsContainer.Clear();
            if (currentFacility.points == null) return;

            var hazards = currentFacility.points.Select(p => p.hazards).Distinct().ToList();

            foreach (var h in hazards)
            {
                if (h == null) continue;
                Label tag = new Label(h.Name);
                tag.style.backgroundColor = new StyleColor(new Color32(234, 179, 8, 255));
                tag.style.color = new StyleColor(Color.black);
                tag.style.unityFontStyleAndWeight = FontStyle.Bold;
                tag.style.paddingLeft = 12; tag.style.paddingRight = 12;
                tag.style.paddingTop = 4; tag.style.paddingBottom = 4;
                tag.style.borderTopLeftRadius = 6; tag.style.borderBottomLeftRadius = 6;
                tag.style.borderTopRightRadius = 6; tag.style.borderBottomRightRadius = 6;
                tag.style.fontSize = 12;
                tag.style.marginBottom = 4;
                hazardTagsContainer.Add(tag);
            }
        }

        // === ENGINE RANDOMIZER APD (8 ZONA BERURUTAN) ===
        private void GenerateRandomizedWornEquipment()
        {
            currentWornPPE.Clear();

            PPEZone[] orderedZones = new PPEZone[] {
                PPEZone.Head, PPEZone.Eye, PPEZone.Respiratory, PPEZone.Ear,
                PPEZone.Body, PPEZone.Hand, PPEZone.Foot, PPEZone.FallProtection
            };

            Dictionary<PPEZone, PersonalProtectiveEquipment> correctMap = new Dictionary<PPEZone, PersonalProtectiveEquipment>();
            foreach (var z in orderedZones) correctMap[z] = null;

            if (currentFacility.CorrectEquipment != null)
            {
                foreach (var eq in currentFacility.CorrectEquipment)
                {
                    if (eq == null) continue;
                    foreach (var z in orderedZones)
                    {
                        if (eq.IsForZone(z)) correctMap[z] = eq;
                    }
                }
            }

            int mistakesCount = Random.Range(1, 4);
            var sabotagedZones = orderedZones.OrderBy(x => Random.value).Take(mistakesCount).ToList();

            foreach (var zone in orderedZones)
            {
                var correctItem = correctMap[zone];
                WornItem worn = new WornItem { Zone = zone, IsMarkedByUser = false };

                var validDecoys = new List<PersonalProtectiveEquipment>();
                if (currentFacility.DecoyEquipment != null)
                {
                    validDecoys = currentFacility.DecoyEquipment.Where(d => d != null && d.IsForZone(zone)).ToList();
                }

                if (sabotagedZones.Contains(zone))
                {
                    worn.IsActuallyWrong = true;

                    if (correctItem != null)
                    {
                        if (Random.value > 0.5f && validDecoys.Count > 0)
                            worn.Equipment = validDecoys[Random.Range(0, validDecoys.Count)];
                        else
                            worn.Equipment = null;
                    }
                    else
                    {
                        if (validDecoys.Count > 0)
                            worn.Equipment = validDecoys[Random.Range(0, validDecoys.Count)];
                        else
                        {
                            worn.IsActuallyWrong = false;
                            worn.Equipment = null;
                        }
                    }
                }
                else
                {
                    worn.IsActuallyWrong = false;
                    worn.Equipment = correctItem;
                }

                currentWornPPE.Add(worn);
            }
        }

        private void BuildInspectionList()
        {
            inspectionList.Clear();
            listRows.Clear();

            for (int i = 0; i < currentWornPPE.Count; i++)
            {
                int index = i;
                var wornItem = currentWornPPE[index];

                VisualElement row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.justifyContent = Justify.SpaceBetween;
                row.style.borderBottomWidth = 1;
                row.style.borderBottomColor = new StyleColor(new Color32(226, 232, 240, 255));
                row.style.paddingLeft = 16; row.style.paddingRight = 16;
                row.style.paddingTop = 12; row.style.paddingBottom = 12;
                row.style.marginBottom = 8;
                row.style.borderTopLeftRadius = 8; row.style.borderBottomLeftRadius = 8;
                row.style.borderTopRightRadius = 8; row.style.borderBottomRightRadius = 8;

                if (wornItem.IsMarkedByUser)
                    row.style.backgroundColor = new StyleColor(new Color32(254, 226, 226, 255));
                else
                    row.style.backgroundColor = new StyleColor(new Color32(248, 250, 252, 255));

                listRows[wornItem.Zone] = row;

                VisualElement infoBox = new VisualElement();
                infoBox.style.flexDirection = FlexDirection.Row;
                infoBox.style.alignItems = Align.Center;
                infoBox.style.flexGrow = 1;

                VisualElement icon = new VisualElement();
                icon.style.width = 40; icon.style.height = 40;

                string itemName = "TIDAK MEMAKAI APD";
                if (wornItem.Equipment != null)
                {
                    itemName = wornItem.Equipment.Name;
                    if (wornItem.Equipment.Image != null)
                        icon.style.backgroundImage = new StyleBackground(wornItem.Equipment.Image);

                    // ICON DIKLIK -> BUKA MODAL FOTO
                    icon.RegisterCallback<ClickEvent>(ev => OpenPPEModal(wornItem.Equipment));
                }
                else
                {
                    icon.style.backgroundColor = new StyleColor(new Color32(203, 213, 225, 255));
                    Label qMark = new Label("?");
                    qMark.style.unityTextAlign = TextAnchor.MiddleCenter;
                    qMark.style.unityFontStyleAndWeight = FontStyle.Bold;
                    icon.Add(qMark);
                }

                infoBox.Add(icon);

                VisualElement textCol = new VisualElement();
                textCol.style.flexDirection = FlexDirection.Column;
                textCol.style.marginLeft = 16;

                Label zoneLbl = new Label(wornItem.Zone.ToLabel());
                zoneLbl.style.fontSize = 11; zoneLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
                zoneLbl.style.color = new StyleColor(new Color32(59, 130, 246, 255));

                Label nameLbl = new Label(itemName);
                nameLbl.style.fontSize = 14; nameLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
                nameLbl.style.color = (wornItem.Equipment == null) ? new StyleColor(new Color32(239, 68, 68, 255)) : new StyleColor(new Color32(15, 23, 42, 255));

                textCol.Add(zoneLbl); textCol.Add(nameLbl);
                infoBox.Add(textCol);
                row.Add(infoBox);

                Button toggleBtn = new Button();
                toggleBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
                toggleBtn.style.borderTopLeftRadius = 6; toggleBtn.style.borderBottomLeftRadius = 6;
                toggleBtn.style.borderTopRightRadius = 6; toggleBtn.style.borderBottomRightRadius = 6;
                toggleBtn.style.paddingLeft = 16; toggleBtn.style.paddingRight = 16;
                toggleBtn.style.paddingTop = 10; toggleBtn.style.paddingBottom = 10;

                if (wornItem.IsMarkedByUser)
                {
                    toggleBtn.style.backgroundColor = new StyleColor(new Color32(239, 68, 68, 255));
                    toggleBtn.style.color = new StyleColor(Color.white);
                    toggleBtn.text = "✖ Ditandai";
                }
                else
                {
                    toggleBtn.style.backgroundColor = new StyleColor(new Color32(226, 232, 240, 255));
                    toggleBtn.style.color = new StyleColor(new Color32(100, 116, 139, 255));
                    toggleBtn.text = "Tandai Salah";
                }

                toggleBtn.clicked += () =>
                {
                    currentWornPPE[index].IsMarkedByUser = !currentWornPPE[index].IsMarkedByUser;
                    BuildInspectionList();
                    UpdateMarkedCount();
                };

                row.Add(toggleBtn);
                inspectionList.Add(row);
            }

            RenderAvatarCallouts();
        }

        private void RenderAvatarCallouts()
        {
            var root = uIDocument.rootVisualElement;
            var calloutsContainer = root.Q<VisualElement>("callouts-container");
            if (calloutsContainer == null) return;

            calloutsContainer.Clear();

            Dictionary<PPEZone, Vector2> zoneAnchors = new Dictionary<PPEZone, Vector2>
            {
                { PPEZone.Head, new Vector2(50f, 15f) },
                { PPEZone.Eye, new Vector2(50f, 22f) },
                { PPEZone.Respiratory, new Vector2(50f, 28f) },
                { PPEZone.Ear, new Vector2(40f, 25f) },
                { PPEZone.Body, new Vector2(50f, 50f) },
                { PPEZone.Hand, new Vector2(25f, 65f) },
                { PPEZone.Foot, new Vector2(40f, 90f) },
                { PPEZone.FallProtection, new Vector2(50f, 40f) },
                { PPEZone.Other, new Vector2(75f, 50f) }
            };

            foreach (var wornItem in currentWornPPE)
            {
                if (!zoneAnchors.ContainsKey(wornItem.Zone)) continue;

                Vector2 anchorPos = zoneAnchors[wornItem.Zone];
                bool isLeftSide = anchorPos.x <= 50f;

                VisualElement callout = new VisualElement();
                callout.style.position = Position.Absolute;
                callout.style.top = Length.Percent(anchorPos.y);
                if (isLeftSide)
                {
                    callout.style.left = Length.Percent(anchorPos.x - 25f);
                    callout.style.flexDirection = FlexDirection.Row;
                }
                else
                {
                    callout.style.left = Length.Percent(anchorPos.x + 5f);
                    callout.style.flexDirection = FlexDirection.RowReverse;
                }
                callout.style.alignItems = Align.Center;

                Label lblZone = new Label(wornItem.Zone.DisplayName());
                lblZone.style.backgroundColor = Color.white;
                lblZone.style.color = new StyleColor(new Color32(15, 23, 42, 255));
                lblZone.style.unityFontStyleAndWeight = FontStyle.Bold;
                lblZone.style.fontSize = 12;
                lblZone.style.paddingLeft = 12; lblZone.style.paddingRight = 12;
                lblZone.style.paddingTop = 6; lblZone.style.paddingBottom = 6;
                lblZone.style.borderTopLeftRadius = 15; lblZone.style.borderBottomLeftRadius = 15;
                lblZone.style.borderTopRightRadius = 15; lblZone.style.borderBottomRightRadius = 15;
                lblZone.style.borderTopWidth = 2; lblZone.style.borderBottomWidth = 2;
                lblZone.style.borderLeftWidth = 2; lblZone.style.borderRightWidth = 2;

                StyleColor targetColor = wornItem.IsMarkedByUser
                    ? new StyleColor(new Color32(239, 68, 68, 255))
                    : new StyleColor(new Color32(203, 213, 225, 255));

                lblZone.style.borderTopColor = targetColor;
                lblZone.style.borderBottomColor = targetColor;
                lblZone.style.borderLeftColor = targetColor;
                lblZone.style.borderRightColor = targetColor;

                VisualElement dot = new VisualElement();
                dot.style.width = 12; dot.style.height = 12;
                dot.style.borderTopLeftRadius = 6; dot.style.borderTopRightRadius = 6;
                dot.style.borderBottomLeftRadius = 6; dot.style.borderBottomRightRadius = 6;

                Label dotIcon = new Label();
                dotIcon.style.unityTextAlign = TextAnchor.MiddleCenter;
                dotIcon.style.fontSize = 8;
                dotIcon.style.color = Color.white;
                dotIcon.style.marginTop = 1;

                if (wornItem.IsMarkedByUser)
                {
                    dot.style.backgroundColor = new StyleColor(new Color32(239, 68, 68, 255));
                    dotIcon.text = "✖";
                }
                else
                {
                    dot.style.backgroundColor = new StyleColor(new Color32(34, 197, 94, 255));
                    dotIcon.text = "✔";
                }

                dot.Add(dotIcon);

                VisualElement line = new VisualElement();
                line.style.height = 2;
                line.style.width = 30;
                line.style.backgroundColor = dot.style.backgroundColor;

                callout.Add(lblZone);
                callout.Add(line);
                callout.Add(dot);

                // KLIK CALLOUT UNTUK SCROLL DAN HIGHLIGHT
                callout.RegisterCallback<ClickEvent>(ev =>
                {
                    if (listRows.ContainsKey(wornItem.Zone))
                    {
                        VisualElement targetRow = listRows[wornItem.Zone];
                        inspectionList.ScrollTo(targetRow);

                        StyleColor originalColor = targetRow.style.backgroundColor;
                        targetRow.style.backgroundColor = new StyleColor(new Color32(253, 230, 138, 255));

                        targetRow.schedule.Execute(() =>
                        {
                            targetRow.style.backgroundColor = originalColor;
                        }).StartingIn(500);
                    }
                });

                calloutsContainer.Add(callout);
            }
        }

        private void UpdateMarkedCount()
        {
            int count = currentWornPPE.Count(w => w.IsMarkedByUser);
            lblMarkedCount.text = $"{count} Ditandai";
        }

        //private void CheckInspectionResults()
        //{
        //    foreach (var worn in currentWornPPE)
        //    {
        //        int pts = 0;
        //        if (worn.IsMarkedByUser == worn.IsActuallyWrong) pts = 10;
        //        else pts = -5;

        //        currentScore += pts;

        //        string itemName = worn.Equipment != null ? worn.Equipment.Name : "TIDAK PAKAI APD";
        //        inspectionHistory.Add(new InspectionRecord
        //        {
        //            RoundNumber = currentRoundIndex + 1,
        //            ItemName = itemName,
        //            IsActuallyWrong = worn.IsActuallyWrong,
        //            IsMarkedByUser = worn.IsMarkedByUser,
        //            PointsGained = pts
        //        });
        //    }

        //    currentRoundIndex++;
        //    if (currentRoundIndex < roundFacilities.Count)
        //        LoadRound(currentRoundIndex);
        //    else
        //        FinishGame();
        //}

        private void FinishGame()
        {
            isGameActive = false;
            ShowResult();
        }

        //private void ShowResult()
        //{
        //    var root = uIDocument.rootVisualElement;
        //    detailList.Clear();

        //    int maxPossibleScore = inspectionHistory.Count * 10;
        //    int finalBaseScore = Mathf.Max(0, currentScore);
        //    float percentage = maxPossibleScore > 0 ? ((float)finalBaseScore / maxPossibleScore) * 100f : 0f;
        //    bool isPassed = percentage >= KKM;

        //    for (int i = 0; i < inspectionHistory.Count; i++)
        //    {
        //        var record = inspectionHistory[i];
        //        bool isCorrect = (record.PointsGained > 0);

        //        VisualElement row = new VisualElement();
        //        row.style.flexDirection = FlexDirection.Row;
        //        row.style.alignItems = Align.Center;
        //        row.style.paddingLeft = 12; row.style.paddingRight = 12;
        //        row.style.paddingTop = 12; row.style.paddingBottom = 12;
        //        row.style.borderBottomWidth = 1;
        //        row.style.borderBottomColor = new StyleColor(new Color32(226, 232, 240, 255));
        //        if (i % 2 != 0) row.style.backgroundColor = new StyleColor(new Color32(248, 250, 252, 255));

        //        VisualElement col1 = new VisualElement(); col1.style.width = new Length(35, LengthUnit.Percent); col1.style.flexDirection = FlexDirection.Column;
        //        col1.Add(new Label($"Titik {record.RoundNumber}") { style = { fontSize = 11, color = Color.gray, unityFontStyleAndWeight = FontStyle.Bold } });
        //        Label ansLbl = new Label(record.ItemName);
        //        ansLbl.style.unityFontStyleAndWeight = FontStyle.Bold; ansLbl.style.color = new StyleColor(new Color32(15, 23, 42, 255));
        //        col1.Add(ansLbl);

        //        string systemText = record.IsActuallyWrong ? "Salah" : "Sesuai";
        //        Label col2 = new Label(systemText);
        //        col2.style.width = new Length(20, LengthUnit.Percent); col2.style.unityTextAlign = TextAnchor.MiddleCenter; col2.style.unityFontStyleAndWeight = FontStyle.Bold;
        //        col2.style.color = record.IsActuallyWrong ? new StyleColor(new Color32(239, 68, 68, 255)) : new StyleColor(new Color32(34, 197, 94, 255));

        //        string playerText = record.IsMarkedByUser ? "Ditandai Salah" : "Dibiarkan (Sesuai)";
        //        Label col3 = new Label(playerText);
        //        col3.style.width = new Length(20, LengthUnit.Percent); col3.style.unityTextAlign = TextAnchor.MiddleCenter; col3.style.unityFontStyleAndWeight = FontStyle.Bold;
        //        col3.style.color = new StyleColor(new Color32(15, 23, 42, 255));

        //        Label col4 = new Label(record.PointsGained > 0 ? $"+{record.PointsGained}" : record.PointsGained.ToString());
        //        col4.style.width = new Length(15, LengthUnit.Percent); col4.style.unityTextAlign = TextAnchor.MiddleCenter; col4.style.unityFontStyleAndWeight = FontStyle.Bold;
        //        col4.style.color = record.PointsGained > 0 ? new StyleColor(new Color32(34, 197, 94, 255)) : new StyleColor(new Color32(239, 68, 68, 255));

        //        Label col5 = new Label(isCorrect ? "✅" : "❌");
        //        col5.style.width = new Length(10, LengthUnit.Percent); col5.style.unityTextAlign = TextAnchor.MiddleCenter; col5.style.fontSize = 16;

        //        row.Add(col1); row.Add(col2); row.Add(col3); row.Add(col4); row.Add(col5);
        //        detailList.Add(row);
        //    }

        //    int timeBonus = (isPassed && timeRemaining > 0) ? Mathf.FloorToInt(timeRemaining) : 0;
        //    int finalTotal = currentScore + timeBonus;

        //    string starText = "☆ ☆ ☆";
        //    if (percentage >= 90f) starText = "★ ★ ★";
        //    else if (percentage >= 70f) starText = "★ ★ ☆";
        //    else if (percentage >= 40f) starText = "★ ☆ ☆";

        //    root.Q<Label>("stars-label").text = starText;
        //    root.Q<Label>("score-pct").text = $"{Mathf.RoundToInt(percentage)}%";
        //    root.Q<Label>("score-pct").style.color = isPassed ? new StyleColor(new Color32(37, 99, 235, 255)) : new StyleColor(new Color32(239, 68, 68, 255));

        //    root.Q<Label>("status-label").text = isPassed ? "(LULUS)" : "(TIDAK LULUS)";
        //    root.Q<Label>("status-label").style.color = isPassed ? new StyleColor(new Color32(34, 197, 94, 255)) : new StyleColor(new Color32(239, 68, 68, 255));

        //    root.Q<Label>("summary-base").text = $"Total Poin Jawaban: {currentScore} / {maxPossibleScore}";
        //    root.Q<Label>("summary-bonus").text = $"Bonus Waktu ({Mathf.FloorToInt(timeRemaining)} detik): +{timeBonus}";
        //    root.Q<Label>("score-final").text = $"SKOR AKHIR: {finalTotal}";

        //    root.Q<Button>("btn-next").style.display = isPassed ? DisplayStyle.Flex : DisplayStyle.None;
        //    resultModal.style.display = DisplayStyle.Flex;
        //}



        // 1. UPDATE STRUKTUR DATA HISTORI
        private class InspectionRecord
        {
            public int RoundNumber;
            public string ZoneName; // Menyimpan teks seperti "Kepala"
            public PersonalProtectiveEquipment Equipment; // Untuk merender Icon
            public bool IsActuallyWrong;
            public bool IsMarkedByUser;
            public int PointsGained;
        }

        // 2. UPDATE FUNGSI PENGECEKAN
        private void CheckInspectionResults()
        {
            foreach (var worn in currentWornPPE)
            {
                int pts = 0;
                if (worn.IsMarkedByUser == worn.IsActuallyWrong) pts = 10;
                else pts = -5;

                currentScore += pts;

                // Simpan histori lengkap dengan zona dan objek equipment
                inspectionHistory.Add(new InspectionRecord
                {
                    RoundNumber = currentRoundIndex + 1,
                    ZoneName = worn.Zone.DisplayName(), // Menghasilkan teks: "Kepala", "Kaki", dll.
                    Equipment = worn.Equipment,         // Menyimpan APD yang dipakai
                    IsActuallyWrong = worn.IsActuallyWrong,
                    IsMarkedByUser = worn.IsMarkedByUser,
                    PointsGained = pts
                });
            }

            currentRoundIndex++;
            if (currentRoundIndex < roundFacilities.Count)
                LoadRound(currentRoundIndex);
            else
                FinishGame();
        }

        // 3. UPDATE FUNGSI TAMPILAN RESULT MODAL
        private void ShowResult()
        {
            var root = uIDocument.rootVisualElement;
            detailList.Clear();

            int maxPossibleScore = inspectionHistory.Count * 10;
            int finalBaseScore = Mathf.Max(0, currentScore);
            float percentage = maxPossibleScore > 0 ? ((float)finalBaseScore / maxPossibleScore) * 100f : 0f;
            bool isPassed = percentage >= KKM;

            for (int i = 0; i < inspectionHistory.Count; i++)
            {
                var record = inspectionHistory[i];
                bool isCorrect = (record.PointsGained > 0);

                VisualElement row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.paddingLeft = 12; row.style.paddingRight = 12;
                row.style.paddingTop = 8; row.style.paddingBottom = 8; // Dibuat lebih rapat
                row.style.borderBottomWidth = 1;
                row.style.borderBottomColor = new StyleColor(new Color32(226, 232, 240, 255));
                if (i % 2 != 0) row.style.backgroundColor = new StyleColor(new Color32(248, 250, 252, 255));

                // KOLOM 1: ICON APD & TEKS (ZONA + NAMA APD) - 35%
                VisualElement col1 = new VisualElement();
                col1.style.width = new Length(35, LengthUnit.Percent);
                col1.style.flexDirection = FlexDirection.Row;
                col1.style.alignItems = Align.Center;

                // Buat Ikon Gambar APD
                VisualElement icon = new VisualElement();
                icon.style.width = 36; icon.style.height = 36;
                icon.style.marginRight = 12;

                string itemName = "TIDAK MEMAKAI APD";
                if (record.Equipment != null)
                {
                    itemName = record.Equipment.Name;
                    if (record.Equipment.Image != null)
                        icon.style.backgroundImage = new StyleBackground(record.Equipment.Image);

                    // ICON DIKLIK -> BUKA MODAL FOTO
                    icon.RegisterCallback<ClickEvent>(ev => OpenPPEModal(record.Equipment));
                    icon.style.cursor = new StyleCursor(StyleKeyword.Initial); // Pointer
                }
                else
                {
                    icon.style.backgroundColor = new StyleColor(new Color32(203, 213, 225, 255));
                    Label qMark = new Label("?");
                    qMark.style.unityTextAlign = TextAnchor.MiddleCenter;
                    qMark.style.unityFontStyleAndWeight = FontStyle.Bold;
                    icon.Add(qMark);
                }

                // Kolom Teks di sebelah gambar (Atas: Zona, Bawah: Nama APD)
                VisualElement textContainer = new VisualElement();
                textContainer.style.flexDirection = FlexDirection.Column;

                // Tulisan Zona (Contoh: "Kepala")
                Label zoneLbl = new Label(record.ZoneName);
                zoneLbl.style.fontSize = 10;
                zoneLbl.style.color = new StyleColor(new Color32(59, 130, 246, 255)); // blue-500
                zoneLbl.style.unityFontStyleAndWeight = FontStyle.Bold;

                // Tulisan Nama APD
                Label ansLbl = new Label(itemName);
                ansLbl.style.fontSize = 12;
                ansLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
                ansLbl.style.color = new StyleColor(new Color32(15, 23, 42, 255));
                ansLbl.style.whiteSpace = WhiteSpace.Normal; // Biar kalau panjang bisa turun (wrap)

                textContainer.Add(zoneLbl);
                textContainer.Add(ansLbl);

                col1.Add(icon);
                col1.Add(textContainer);

                // KOLOM 2: KONDISI ASLI SISTEM (20%)
                string systemText = record.IsActuallyWrong ? "Salah" : "Sesuai";
                Label col2 = new Label(systemText);
                col2.style.width = new Length(20, LengthUnit.Percent); col2.style.unityTextAlign = TextAnchor.MiddleCenter; col2.style.unityFontStyleAndWeight = FontStyle.Bold;
                col2.style.color = record.IsActuallyWrong ? new StyleColor(new Color32(239, 68, 68, 255)) : new StyleColor(new Color32(34, 197, 94, 255));

                // KOLOM 3: JAWABAN PLAYER (20%)
                string playerText = record.IsMarkedByUser ? "Ditandai Salah" : "Dibiarkan";
                Label col3 = new Label(playerText);
                col3.style.width = new Length(20, LengthUnit.Percent); col3.style.unityTextAlign = TextAnchor.MiddleCenter; col3.style.unityFontStyleAndWeight = FontStyle.Bold;
                col3.style.color = new StyleColor(new Color32(15, 23, 42, 255));

                // KOLOM 4: POIN (15%)
                Label col4 = new Label(record.PointsGained > 0 ? $"+{record.PointsGained}" : record.PointsGained.ToString());
                col4.style.width = new Length(15, LengthUnit.Percent); col4.style.unityTextAlign = TextAnchor.MiddleCenter; col4.style.unityFontStyleAndWeight = FontStyle.Bold;
                col4.style.color = record.PointsGained > 0 ? new StyleColor(new Color32(34, 197, 94, 255)) : new StyleColor(new Color32(239, 68, 68, 255));

                // KOLOM 5: STATUS (10%)
                Label col5 = new Label(isCorrect ? "✅" : "❌");
                col5.style.width = new Length(10, LengthUnit.Percent); col5.style.unityTextAlign = TextAnchor.MiddleCenter; col5.style.fontSize = 16;

                row.Add(col1); row.Add(col2); row.Add(col3); row.Add(col4); row.Add(col5);
                detailList.Add(row);
            }

            // --- Bagian Kalkulasi Tetap Sama ---
            int timeBonus = (isPassed && timeRemaining > 0) ? Mathf.FloorToInt(timeRemaining) : 0;
            int finalTotal = currentScore + timeBonus;

            string starText = "☆ ☆ ☆";
            if (percentage >= 90f) starText = "★ ★ ★";
            else if (percentage >= 70f) starText = "★ ★ ☆";
            else if (percentage >= 40f) starText = "★ ☆ ☆";
            APDGameManager.Instance.SavePlayerScore("level_1", finalTotal, percentage >= 90f ? 3 : percentage >= 70f ? 2 : 1, percentage);

            root.Q<Label>("stars-label").text = starText;
            root.Q<Label>("score-pct").text = $"{Mathf.RoundToInt(percentage)}%";
            root.Q<Label>("score-pct").style.color = isPassed ? new StyleColor(new Color32(37, 99, 235, 255)) : new StyleColor(new Color32(239, 68, 68, 255));

            root.Q<Label>("status-label").text = isPassed ? "(LULUS)" : "(TIDAK LULUS)";
            root.Q<Label>("status-label").style.color = isPassed ? new StyleColor(new Color32(34, 197, 94, 255)) : new StyleColor(new Color32(239, 68, 68, 255));

            root.Q<Label>("summary-base").text = $"Total Poin Jawaban: {currentScore} / {maxPossibleScore}";
            root.Q<Label>("summary-bonus").text = $"Bonus Waktu ({Mathf.FloorToInt(timeRemaining)} detik): +{timeBonus}";
            root.Q<Label>("score-final").text = $"SKOR AKHIR: {finalTotal}";

            root.Q<Button>("btn-next").style.display = isPassed ? DisplayStyle.Flex : DisplayStyle.None;
            resultModal.style.display = DisplayStyle.Flex;
        }
    }
}