using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

namespace Game.APD
{
    public class Level2GameMenuUI : MonoBehaviour
    {
        [SerializeField] private UIDocument uIDocument;
        [SerializeField] private float levelDurationSeconds = 300f;
        private const float KKM = 70f;
        [SerializeField] private int hintPenalty = 5;

        // Struktur untuk histori tabel evaluasi akhir
        private class Level2Record
        {
            public int RoundNumber;
            public string ZoneName;
            public PersonalProtectiveEquipment CorrectPPE;
            public PersonalProtectiveEquipment PlayerPPE;
            public int PointsGained;
        }

        private Facility currentFacility;
        private List<Facility> roundFacilities = new List<Facility>();
        private int currentRoundIndex = 0;

        private PPEZone activeTabZone = PPEZone.Head;
        private Dictionary<PPEZone, PersonalProtectiveEquipment> playerEquipped = new Dictionary<PPEZone, PersonalProtectiveEquipment>();
        private Dictionary<PPEZone, PersonalProtectiveEquipment> correctMap = new Dictionary<PPEZone, PersonalProtectiveEquipment>();
        private Dictionary<PPEZone, List<PersonalProtectiveEquipment>> optionsMap = new Dictionary<PPEZone, List<PersonalProtectiveEquipment>>();

        private List<Level2Record> roundHistory = new List<Level2Record>();

        private int currentScore = 0;
        private float timeRemaining;
        private bool isGameActive = false;
        private bool isPaused = false;

        private Label timerLabel, lblScore, lblRound, lblActiveZone, lblZoneHazard, lblProgress;
        private VisualElement zoneTabsContainer, ppeOptionsGrid, calloutsContainer;
        private Button btnSubmit, btnHint, btnInfo;

        // Modals
        private VisualElement hintModal, infoModal, resultModal, ppeModal;
        private ScrollView detailList;
        private VisualElement ppeModalImg;
        private Label ppeModalName, ppeModalDesc;

        private readonly PPEZone[] orderedZones = {
            PPEZone.Head, PPEZone.Eye, PPEZone.Respiratory, PPEZone.Ear,
            PPEZone.Body, PPEZone.Hand, PPEZone.Foot, PPEZone.FallProtection
        };

        private void Start()
        {
            if (APDGameManager.Instance != null && APDGameManager.Instance.FacilitySelectedData != null)
                roundFacilities.Add(APDGameManager.Instance.FacilitySelectedData);

            var root = uIDocument.rootVisualElement;
            timerLabel = root.Q<Label>("timer-label");
            lblScore = root.Q<Label>("lbl-score");
            lblRound = root.Q<Label>("lbl-round");
            lblActiveZone = root.Q<Label>("lbl-active-zone");
            lblZoneHazard = root.Q<Label>("lbl-zone-hazard");
            lblProgress = root.Q<Label>("lbl-progress");

            zoneTabsContainer = root.Q<VisualElement>("zone-tabs-container");
            ppeOptionsGrid = root.Q<VisualElement>("ppe-options-grid");
            calloutsContainer = root.Q<VisualElement>("callouts-container");
            detailList = root.Q<ScrollView>("detail-list");

            // --- INFO MODAL ---
            infoModal = root.Q<VisualElement>("info-modal");
            btnInfo = root.Q<Button>("btn-info");
            btnInfo.clicked += () => { isPaused = true; infoModal.style.display = DisplayStyle.Flex; };
            root.Q<Button>("btn-info-ok").clicked += () => { isPaused = false; infoModal.style.display = DisplayStyle.None; };

            // --- HINT MODAL ---
            hintModal = root.Q<VisualElement>("hint-modal");
            btnHint = root.Q<Button>("btn-hint");
            btnHint.clicked += () => { if (isGameActive) { isPaused = true; hintModal.style.display = DisplayStyle.Flex; } };
            root.Q<Button>("btn-hint-cancel").clicked += () => { isPaused = false; hintModal.style.display = DisplayStyle.None; };
            root.Q<Button>("btn-hint-confirm").clicked += ConfirmHint;

            // --- PPE DETAIL MODAL ---
            ppeModal = root.Q<VisualElement>("ppe-modal");
            ppeModalImg = root.Q<VisualElement>("ppe-modal-img");
            ppeModalName = root.Q<Label>("ppe-modal-name");
            ppeModalDesc = root.Q<Label>("ppe-modal-desc");

            var btnClosePpe = root.Q<Button>("btn-close-ppe");
            if (btnClosePpe != null) { btnClosePpe.clicked -= ClosePPEModal; btnClosePpe.clicked += ClosePPEModal; }

            ppeModal.RegisterCallback<ClickEvent>(evt => {
                if (evt.target == ppeModal) ClosePPEModal();
            });

            // --- RESULT MODAL ---
            resultModal = root.Q<VisualElement>("result-modal");
            btnSubmit = root.Q<Button>("btn-submit");
            btnSubmit.clicked += CheckResults;

            root.Q<Button>("btn-retry").clicked += () => SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            root.Q<Button>("btn-next").clicked += () => Debug.Log("Lanjut ke modul berikutnya...");

            currentScore = 0;
            roundHistory.Clear();
            timeRemaining = levelDurationSeconds;
            isGameActive = true;
            isPaused = false;

            if (roundFacilities.Count > 0) LoadRound(0);
        }

        private void Update()
        {
            if (!isGameActive || isPaused) return;
            if (timeRemaining > 0)
            {
                timeRemaining -= Time.deltaTime;
                timerLabel.text = string.Format("{0:00}:{1:00}", Mathf.FloorToInt(timeRemaining / 60), Mathf.FloorToInt(timeRemaining % 60));
                if (timeRemaining <= 0) FinishGame();
            }
        }

        // ==========================================
        // FUNGSI MODAL PPE & HINT
        // ==========================================
        private void OpenPPEModal(PersonalProtectiveEquipment ppe)
        {
            if (ppe == null) return;
            ppeModalName.text = ppe.Name;
            ppeModalDesc.text = string.IsNullOrEmpty(ppe.Description) ? "Tidak ada deskripsi detail." : ppe.Description;
            if (ppe.Image != null)
                ppeModalImg.style.backgroundImage = new StyleBackground(ppe.Image);
            else
                ppeModalImg.style.backgroundImage = null;

            ppeModal.style.display = DisplayStyle.Flex;
        }

        private void ClosePPEModal() { ppeModal.style.display = DisplayStyle.None; }

        private void ConfirmHint()
        {
            isPaused = false;
            hintModal.style.display = DisplayStyle.None;

            var correctAns = correctMap[activeTabZone];

            // 1. Cek jika pilihan pemain SAAT INI sudah sama dengan kunci jawaban
            if (playerEquipped[activeTabZone] == correctAns)
            {
                lblZoneHazard.text = "Info: Pilihan Anda di zona ini sudah benar! (Poin tidak dipotong)";
                lblZoneHazard.style.color = new StyleColor(new Color32(34, 197, 94, 255)); // Warna Hijau
                return;
            }

            // 2. Jika belum benar, pakaikan (atau kosongkan) sesuai kunci jawaban
            playerEquipped[activeTabZone] = correctAns;
            currentScore -= hintPenalty;
            lblScore.text = $"Total Skor: {currentScore}";

            // 3. Render Ulang UI
            RenderActiveZoneContent();
            RenderZoneTabs();
            RenderAvatarCallouts();
            UpdateProgress();

            // 4. Beri informasi tertulis di layar tentang apa yang dilakukan Hint
            if (correctAns == null)
            {
                lblZoneHazard.text = "Kunci Jawaban: Zona ini TIDAK MEMERLUKAN APD (Otomatis dikosongkan).";
                lblZoneHazard.style.color = new StyleColor(new Color32(239, 68, 68, 255)); // Merah
            }
            else
            {
                lblZoneHazard.text = $"Kunci Jawaban: APD yang benar adalah {correctAns.Name}.";
                lblZoneHazard.style.color = new StyleColor(new Color32(37, 99, 235, 255)); // Biru
            }
        }

        // ==========================================
        // FUNGSI UTAMA GAMEPLAY (LEVEL 2)
        // ==========================================
        private void LoadRound(int index)
        {
            currentRoundIndex = index;
            currentFacility = roundFacilities[index];
            lblRound.text = $"Titik {index + 1}/{roundFacilities.Count}";
            lblScore.text = $"Total Skor: {currentScore}";

            playerEquipped.Clear();
            correctMap.Clear();
            optionsMap.Clear();

            foreach (var z in orderedZones)
            {
                playerEquipped[z] = null;
                correctMap[z] = null;
                optionsMap[z] = new List<PersonalProtectiveEquipment>();
            }

            if (currentFacility.CorrectEquipment != null)
            {
                foreach (var eq in currentFacility.CorrectEquipment)
                {
                    if (eq != null)
                    {
                        foreach (var z in orderedZones)
                        {
                            if (eq.IsForZone(z)) correctMap[z] = eq;
                        }
                    }
                }
            }

            foreach (var z in orderedZones)
            {
                if (correctMap[z] != null) optionsMap[z].Add(correctMap[z]);

                if (currentFacility.DecoyEquipment != null)
                {
                    var decoys = currentFacility.DecoyEquipment.Where(d => d != null && d.IsForZone(z)).ToList();
                    optionsMap[z].AddRange(decoys);
                }
                optionsMap[z] = optionsMap[z].OrderBy(x => Random.value).ToList();
            }

            activeTabZone = PPEZone.Head;
            RenderZoneTabs();
            RenderActiveZoneContent();
            RenderAvatarCallouts();
            UpdateProgress();
        }

        // ==========================================
        // 1. RENDER TAB ZONA DI ATAS
        // ==========================================
        private void RenderZoneTabs()
        {
            zoneTabsContainer.Clear();

            // PAKSA ATURAN LAYOUT DI C# AGAR RAPI (Grid 4x2)
            zoneTabsContainer.style.flexDirection = FlexDirection.Row;
            zoneTabsContainer.style.flexWrap = Wrap.Wrap;
            zoneTabsContainer.style.justifyContent = Justify.SpaceBetween;

            foreach (var zone in orderedZones)
            {
                bool isActive = (zone == activeTabZone);
                bool hasEquipped = (playerEquipped[zone] != null);

                Button tabBtn = new Button();
                tabBtn.text = zone.DisplayName();

                // PERBAIKAN: Ubah width menjadi 23% agar pas 4 kolom 
                // (4 * 23% = 92%, sisa 8% akan dibagi rata otomatis sebagai jarak)
                tabBtn.style.width = new Length(23, LengthUnit.Percent);

                tabBtn.style.paddingTop = 12; tabBtn.style.paddingBottom = 12;
                tabBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
                SetBorderRadius(tabBtn, 8);
                SetBorderWidth(tabBtn, 2);
                tabBtn.style.marginBottom = 8;

                if (isActive)
                {
                    tabBtn.style.backgroundColor = new StyleColor(new Color32(253, 224, 71, 255));
                    SetBorderColor(tabBtn, new Color32(234, 179, 8, 255));
                    tabBtn.style.color = new StyleColor(new Color32(15, 23, 42, 255));
                }
                else
                {
                    tabBtn.style.backgroundColor = new StyleColor(Color.white);
                    SetBorderColor(tabBtn, new Color32(226, 232, 240, 255));
                    tabBtn.style.color = new StyleColor(new Color32(71, 85, 105, 255));

                    if (hasEquipped)
                    {
                        tabBtn.style.backgroundColor = new StyleColor(new Color32(240, 253, 244, 255));
                        SetBorderColor(tabBtn, new Color32(134, 239, 172, 255));
                    }
                }

                tabBtn.clicked += () =>
                {
                    activeTabZone = zone;
                    RenderZoneTabs();
                    RenderActiveZoneContent();
                };

                zoneTabsContainer.Add(tabBtn);
            }
        }

        private void RenderActiveZoneContent()
        {
            lblActiveZone.text = activeTabZone.DisplayName().ToUpper();

            // Kembalikan warna teks hazard ke normal (abu-abu) tiap ganti tab
            lblZoneHazard.style.color = new StyleColor(new Color32(71, 85, 105, 255));

            if (currentFacility.points != null)
            {
                var hazards = currentFacility.points.Select(p => p.hazards).Distinct().Where(h => h != null).ToList();
                if (hazards.Count > 0)
                    lblZoneHazard.text = "Perhatikan potensi bahaya: " + string.Join(", ", hazards.Select(h => h.Name));
                else
                    lblZoneHazard.text = "Tidak teridentifikasi bahaya khusus untuk zona ini.";
            }

            ppeOptionsGrid.Clear();
            // KARTU "TANPA APD" TELAH DIHAPUS DARI SINI

            foreach (var ppe in optionsMap[activeTabZone])
            {
                if (ppe != null) ppeOptionsGrid.Add(CreatePPECard(ppe));
            }
        }

        private VisualElement CreatePPECard(PersonalProtectiveEquipment ppe)
        {
            if (ppe == null) return new VisualElement(); // Mencegah error jika data kosong

            bool isEquipped = (playerEquipped[activeTabZone] == ppe);

            VisualElement card = new VisualElement();
            card.style.width = 220;
            card.style.flexDirection = FlexDirection.Column;
            card.style.backgroundColor = Color.white;
            SetBorderRadius(card, 12);
            SetBorderWidth(card, 2);

            if (isEquipped)
                SetBorderColor(card, new Color32(37, 99, 235, 255)); // Biru jika Dipakai
            else
                SetBorderColor(card, new Color32(226, 232, 240, 255)); // Abu jika Normal

            VisualElement imgBox = new VisualElement();
            imgBox.style.height = 140;
            imgBox.style.backgroundColor = new StyleColor(new Color32(248, 250, 252, 255));
            imgBox.style.borderTopLeftRadius = 10; imgBox.style.borderTopRightRadius = 10;

            if (ppe.Image != null)
            {
                imgBox.style.backgroundImage = new StyleBackground(ppe.Image);
                imgBox.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);

                // JADIKAN GAMBAR BISA DIKLIK UNTUK MODAL
                imgBox.RegisterCallback<ClickEvent>(ev => OpenPPEModal(ppe));
                imgBox.style.cursor = new StyleCursor(StyleKeyword.Initial);
            }
            card.Add(imgBox);

            VisualElement content = new VisualElement();
            content.style.paddingLeft = 12; content.style.paddingRight = 12;
            content.style.paddingTop = 12; content.style.paddingBottom = 12;
            content.style.flexGrow = 1;
            content.style.justifyContent = Justify.SpaceBetween;

            Label titleLbl = new Label(ppe.Name);
            titleLbl.style.fontSize = 14;
            titleLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLbl.style.whiteSpace = WhiteSpace.Normal;
            titleLbl.style.marginBottom = 8;
            titleLbl.style.color = new StyleColor(new Color32(15, 23, 42, 255));
            content.Add(titleLbl);

            Label descLbl = new Label(string.IsNullOrEmpty(ppe.Description) ? "-" : ppe.Description);
            descLbl.style.fontSize = 11;
            descLbl.style.color = new StyleColor(new Color32(100, 116, 139, 255));
            descLbl.style.whiteSpace = WhiteSpace.Normal;
            descLbl.style.marginBottom = 12;
            content.Add(descLbl);

            Button btnEquip = new Button();
            btnEquip.style.paddingTop = 10; btnEquip.style.paddingBottom = 10;
            btnEquip.style.unityFontStyleAndWeight = FontStyle.Bold;
            SetBorderRadius(btnEquip, 8);

            if (isEquipped)
            {
                btnEquip.text = "✔ Terpakai";
                btnEquip.style.backgroundColor = new StyleColor(new Color32(37, 99, 235, 255));
                btnEquip.style.color = new StyleColor(Color.white);
            }
            else
            {
                btnEquip.text = "Pakai Ini";
                btnEquip.style.backgroundColor = new StyleColor(new Color32(241, 245, 249, 255));
                btnEquip.style.color = new StyleColor(new Color32(37, 99, 235, 255));
            }

            // LOGIKA TOGGLE (Pakai / Lepas)
            btnEquip.clicked += () =>
            {
                if (playerEquipped[activeTabZone] == ppe)
                {
                    // Jika diklik lagi saat sudah terpakai, maka Lepas APD (kosongkan zona)
                    playerEquipped[activeTabZone] = null;
                }
                else
                {
                    // Jika belum terpakai, pakaikan APD
                    playerEquipped[activeTabZone] = ppe;
                }

                RenderActiveZoneContent();
                RenderZoneTabs();
                RenderAvatarCallouts();
                UpdateProgress();
            };

            content.Add(btnEquip);
            card.Add(content);

            return card;
        }

        private void RenderAvatarCallouts()
        {
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
                { PPEZone.FallProtection, new Vector2(50f, 40f) }
            };

            foreach (var zone in orderedZones)
            {
                if (!zoneAnchors.ContainsKey(zone)) continue;

                var equippedItem = playerEquipped[zone];
                Vector2 anchorPos = zoneAnchors[zone];
                bool isLeftSide = anchorPos.x <= 50f;

                VisualElement callout = new VisualElement();
                callout.style.position = Position.Absolute;
                callout.style.top = Length.Percent(anchorPos.y);
                if (isLeftSide)
                {
                    callout.style.left = Length.Percent(anchorPos.x - 20f);
                    callout.style.flexDirection = FlexDirection.Row;
                }
                else
                {
                    callout.style.left = Length.Percent(anchorPos.x + 5f);
                    callout.style.flexDirection = FlexDirection.RowReverse;
                }
                callout.style.alignItems = Align.Center;

                Label lblName = new Label();
                lblName.style.backgroundColor = Color.white;
                lblName.style.unityFontStyleAndWeight = FontStyle.Bold;
                lblName.style.fontSize = 11;
                lblName.style.paddingLeft = 10; lblName.style.paddingRight = 10;
                lblName.style.paddingTop = 6; lblName.style.paddingBottom = 6;
                SetBorderRadius(lblName, 15);
                SetBorderWidth(lblName, 2);

                if (equippedItem != null)
                {
                    lblName.text = equippedItem.Name;
                    lblName.style.color = new StyleColor(new Color32(15, 23, 42, 255));
                    SetBorderColor(lblName, new Color32(59, 130, 246, 255));
                }
                else
                {
                    lblName.text = "KOSONG (" + zone.DisplayName() + ")";
                    lblName.style.color = new StyleColor(new Color32(239, 68, 68, 255));
                    SetBorderColor(lblName, new Color32(254, 202, 202, 255));
                }

                VisualElement dot = new VisualElement();
                dot.style.width = 10; dot.style.height = 10;
                SetBorderRadius(dot, 5);
                dot.style.backgroundColor = equippedItem != null ? new StyleColor(new Color32(59, 130, 246, 255)) : new StyleColor(new Color32(239, 68, 68, 255));

                VisualElement line = new VisualElement();
                line.style.height = 2; line.style.width = 20;
                line.style.backgroundColor = dot.style.backgroundColor;

                callout.Add(lblName);
                callout.Add(line);
                callout.Add(dot);

                callout.RegisterCallback<ClickEvent>(ev => {
                    activeTabZone = zone;
                    RenderZoneTabs(); RenderActiveZoneContent();
                });

                calloutsContainer.Add(callout);
            }
        }

        private void UpdateProgress()
        {
            int count = playerEquipped.Count(x => x.Value != null);
            lblProgress.text = $"{count}/8 APD Dipakai";
        }

        // ==========================================
        // PENILAIAN & RESULT MODAL
        // ==========================================
        private void CheckResults()
        {
            foreach (var zone in orderedZones)
            {
                var playerAns = playerEquipped[zone];
                var correctAns = correctMap[zone];

                int pts = 0;
                if (playerAns == correctAns)
                    pts = 10;
                else
                    pts = -5;

                currentScore += pts;

                // Simpan histori pilihan pemain vs kunci jawaban
                roundHistory.Add(new Level2Record
                {
                    RoundNumber = currentRoundIndex + 1,
                    ZoneName = zone.DisplayName(),
                    CorrectPPE = correctAns,
                    PlayerPPE = playerAns,
                    PointsGained = pts
                });
            }

            currentRoundIndex++;
            if (currentRoundIndex < roundFacilities.Count)
                LoadRound(currentRoundIndex);
            else
                FinishGame();
        }

        private void FinishGame()
        {
            isGameActive = false;
            ShowResult();
        }

        private void ShowResult()
        {
            detailList.Clear();

            int maxPossibleScore = roundHistory.Count * 10;
            int finalBaseScore = Mathf.Max(0, currentScore);
            float percentage = maxPossibleScore > 0 ? ((float)finalBaseScore / maxPossibleScore) * 100f : 0f;
            bool isPassed = percentage >= KKM;

            for (int i = 0; i < roundHistory.Count; i++)
            {
                var record = roundHistory[i];
                bool isCorrect = (record.PointsGained > 0);

                VisualElement row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.paddingLeft = 12; row.style.paddingRight = 12;
                row.style.paddingTop = 8; row.style.paddingBottom = 8;
                row.style.borderBottomWidth = 1;
                row.style.borderBottomColor = new StyleColor(new Color32(226, 232, 240, 255));
                if (i % 2 != 0) row.style.backgroundColor = new StyleColor(new Color32(248, 250, 252, 255));

                // 1. ZONA (35%)
                VisualElement col1 = new VisualElement(); col1.style.width = new Length(35, LengthUnit.Percent); col1.style.flexDirection = FlexDirection.Column;
                col1.Add(new Label($"Titik {record.RoundNumber}") { style = { fontSize = 11, color = Color.gray, unityFontStyleAndWeight = FontStyle.Bold } });
                Label zoneLbl = new Label(record.ZoneName);
                zoneLbl.style.unityFontStyleAndWeight = FontStyle.Bold; zoneLbl.style.color = new StyleColor(new Color32(15, 23, 42, 255));
                col1.Add(zoneLbl);

                // 2. KUNCI JAWABAN (20%)
                VisualElement col2 = CreateResultItemBlock(record.CorrectPPE, 20);

                // 3. PILIHAN PLAYER (20%)
                VisualElement col3 = CreateResultItemBlock(record.PlayerPPE, 20);

                // 4. POIN (15%)
                Label col4 = new Label(record.PointsGained > 0 ? $"+{record.PointsGained}" : record.PointsGained.ToString());
                col4.style.width = new Length(15, LengthUnit.Percent); col4.style.unityTextAlign = TextAnchor.MiddleCenter; col4.style.unityFontStyleAndWeight = FontStyle.Bold;
                col4.style.color = record.PointsGained > 0 ? new StyleColor(new Color32(34, 197, 94, 255)) : new StyleColor(new Color32(239, 68, 68, 255));

                // 5. STATUS (10%)
                Label col5 = new Label(isCorrect ? "✅" : "❌");
                col5.style.width = new Length(10, LengthUnit.Percent); col5.style.unityTextAlign = TextAnchor.MiddleCenter; col5.style.fontSize = 16;

                row.Add(col1); row.Add(col2); row.Add(col3); row.Add(col4); row.Add(col5);
                detailList.Add(row);
            }

            int timeBonus = (isPassed && timeRemaining > 0) ? Mathf.FloorToInt(timeRemaining) : 0;
            int finalTotal = currentScore + timeBonus;

            string starText = "☆ ☆ ☆";
            if (percentage >= 90f) starText = "★ ★ ★";
            else if (percentage >= 70f) starText = "★ ★ ☆";
            else if (percentage >= 40f) starText = "★ ☆ ☆";
            APDGameManager.Instance.SavePlayerScore("level_1", finalTotal, percentage >= 90f ? 3 : percentage >= 70f ? 2 : 1, percentage);

            var root = uIDocument.rootVisualElement;
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

        // Helper untuk membuat Ikon+Teks APD di Result Modal
        private VisualElement CreateResultItemBlock(PersonalProtectiveEquipment ppe, float widthPercent)
        {
            VisualElement container = new VisualElement();
            container.style.width = new Length(widthPercent, LengthUnit.Percent);
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.justifyContent = Justify.Center;

            VisualElement icon = new VisualElement();
            icon.style.width = 28; icon.style.height = 28;
            icon.style.marginRight = 8;

            Label textLbl = new Label();
            textLbl.style.fontSize = 11;
            textLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            textLbl.style.whiteSpace = WhiteSpace.Normal;
            textLbl.style.color = new StyleColor(new Color32(15, 23, 42, 255));
            textLbl.style.flexShrink = 1;

            if (ppe != null)
            {
                textLbl.text = ppe.Name;
                if (ppe.Image != null)
                {
                    icon.style.backgroundImage = new StyleBackground(ppe.Image);
                    icon.RegisterCallback<ClickEvent>(ev => OpenPPEModal(ppe));
                    icon.style.cursor = new StyleCursor(StyleKeyword.Initial);
                }
            }
            else
            {
                textLbl.text = "TANPA APD";
                textLbl.style.color = new StyleColor(new Color32(148, 163, 184, 255));
                icon.style.backgroundColor = new StyleColor(new Color32(203, 213, 225, 255));
                Label qMark = new Label("✖");
                qMark.style.unityTextAlign = TextAnchor.MiddleCenter;
                qMark.style.unityFontStyleAndWeight = FontStyle.Bold;
                qMark.style.fontSize = 12;
                icon.Add(qMark);
            }

            container.Add(icon);
            container.Add(textLbl);
            return container;
        }

        // --- Helper Methods IStyle ---
        private void SetBorderColor(VisualElement e, Color32 c) { e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = new StyleColor(c); }
        private void SetBorderRadius(VisualElement e, float r) { e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r; }
        private void SetBorderWidth(VisualElement e, float w) { e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = w; }
    }
}