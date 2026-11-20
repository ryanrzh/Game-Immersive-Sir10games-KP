
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.APD
{
    public class LeaderboardMenuUI : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;

        // ==========================================
        // DATA
        // ==========================================

        private string currentUserId;
        private Leaderboard leaderboardData;
        private GameDatabase db;

        private const string AllPlantsOption = "Semua Plant";
        private const string AllLevelsOption = "Semua Level";

        private const int TopLimit = 50;
        private const float CompactBreakpoint = 950f;

        // ==========================================
        // MAIN UI
        // ==========================================

        private Button backButton;

        private VisualElement tableBody;
        private VisualElement leaderboardContent;
        private VisualElement leaderboardTable;

        private ScrollView tableScroll;

        private DropdownField dropdownPlant;
        private DropdownField dropdownLevel;

        private Label resultCount;

        // ==========================================
        // TOP 3
        // ==========================================

        private Label top1Name;
        private Label top1Score;
        private Label top1Stars;

        private Label top2Name;
        private Label top2Score;
        private Label top2Stars;

        private Label top3Name;
        private Label top3Score;
        private Label top3Stars;

        // ==========================================
        // CURRENT USER
        // ==========================================

        private Label myRank;
        private Label myName;
        private Label myScore;
        private Label myStars;
        private Label myStatus;

        // ==========================================
        // SCROLL FADE ANIMATION
        // ==========================================

        private VisualElement scrollFadeLeft;
        private VisualElement scrollFadeRight;

        private Label scrollArrowLeft;
        private Label scrollArrowRight;

        private bool showLeftHint;
        private bool showRightHint;

        private IVisualElementScheduledItem scrollHintAnimation;

        private const float ScrollEdgeTolerance = 2f;

        // ==========================================
        // INITIALIZATION
        // ==========================================

        private void Start()
        {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();

            if (uiDocument == null ||
                APDGameManager.Instance == null)
            {
                Debug.LogError(
                    "LeaderboardMenuUI: UIDocument atau " +
                    "APDGameManager tidak tersedia.");
                return;
            }

            db = APDGameManager.Instance.Database;

            leaderboardData =
                APDGameManager.Instance.LeaderboardData;

            currentUserId =
                APDGameManager.Instance.CurrentUserId;

            if (db == null || leaderboardData == null)
            {
                Debug.LogError(
                    "LeaderboardMenuUI: Database atau " +
                    "LeaderboardData tidak tersedia.");
                return;
            }

            VisualElement root =
                uiDocument.rootVisualElement;

            // GENERAL UI

            backButton =
                root.Q<Button>("btn-back");

            leaderboardContent =
                root.Q<VisualElement>("leaderboard-content");

            tableBody =
                root.Q<VisualElement>("table-body");

            leaderboardTable =
                root.Q<VisualElement>("leaderboard-table");

            tableScroll =
                root.Q<ScrollView>("table-scroll");

            dropdownPlant =
                root.Q<DropdownField>("dropdown-plant");

            dropdownLevel =
                root.Q<DropdownField>("dropdown-level");

            resultCount =
                root.Q<Label>("result-count");

            // TOP 1

            top1Name = root.Q<Label>("top1-name");
            top1Score = root.Q<Label>("top1-score");
            top1Stars = root.Q<Label>("top1-stars");

            // TOP 2

            top2Name = root.Q<Label>("top2-name");
            top2Score = root.Q<Label>("top2-score");
            top2Stars = root.Q<Label>("top2-stars");

            // TOP 3

            top3Name = root.Q<Label>("top3-name");
            top3Score = root.Q<Label>("top3-score");
            top3Stars = root.Q<Label>("top3-stars");

            // CURRENT USER

            myRank = root.Q<Label>("my-rank");
            myName = root.Q<Label>("my-name");
            myScore = root.Q<Label>("my-score");
            myStars = root.Q<Label>("my-stars");
            myStatus = root.Q<Label>("my-status");

            // SCROLL HINTS

            scrollFadeLeft =
                root.Q<VisualElement>("scroll-fade-left");

            scrollFadeRight =
                root.Q<VisualElement>("scroll-fade-right");

            scrollArrowLeft =
                root.Q<Label>("scroll-arrow-left");

            scrollArrowRight =
                root.Q<Label>("scroll-arrow-right");

            if (!ValidateElements())
            {
                Debug.LogError(
                    "LeaderboardMenuUI: Ada elemen " +
                    "UXML yang belum ditemukan.");
                return;
            }

            // BUTTON EVENT

            backButton.clicked += OnBackClicked;

            // FILTER

            SetupDropdowns();

            dropdownPlant.RegisterValueChangedCallback(
                OnFilterChanged);

            dropdownLevel.RegisterValueChangedCallback(
                OnFilterChanged);

            // RESPONSIVE LAYOUT

            leaderboardContent
                .RegisterCallback<GeometryChangedEvent>(
                    OnContentGeometryChanged);

            // SCROLL EFFECT

            SetupScrollEffects();

            // LOAD DATA

            RefreshLeaderboard();
        }

        // ==========================================
        // VALIDATION
        // ==========================================

        private bool ValidateElements()
        {
            return
                backButton != null &&
                leaderboardContent != null &&
                tableBody != null &&
                leaderboardTable != null &&
                tableScroll != null &&
                dropdownPlant != null &&
                dropdownLevel != null &&
                resultCount != null &&

                top1Name != null &&
                top1Score != null &&
                top1Stars != null &&

                top2Name != null &&
                top2Score != null &&
                top2Stars != null &&

                top3Name != null &&
                top3Score != null &&
                top3Stars != null &&

                myRank != null &&
                myName != null &&
                myScore != null &&
                myStars != null &&
                myStatus != null &&

                scrollFadeLeft != null &&
                scrollFadeRight != null &&
                scrollArrowLeft != null &&
                scrollArrowRight != null;
        }

        // ==========================================
        // NAVIGATION
        // ==========================================

        private void OnBackClicked()
        {
            GameManager.UnloadScene("LeaderboardMenu");
        }

        // ==========================================
        // RESPONSIVE LAYOUT
        // ==========================================

        private void OnContentGeometryChanged(
            GeometryChangedEvent evt)
        {
            float width = evt.newRect.width;

            if (width <= 0f)
                return;

            leaderboardContent.EnableInClassList(
                "leaderboard-compact",
                width < CompactBreakpoint);

            UpdateScrollIndicators();
        }

        // ==========================================
        // FILTER EVENTS
        // ==========================================

        private void OnFilterChanged(
            ChangeEvent<string> evt)
        {
            RefreshLeaderboard();
        }

        // ==========================================
        // DROPDOWNS
        // ==========================================

        private void SetupDropdowns()
        {
            var plantNames = new List<string>
            {
                AllPlantsOption
            };

            if (db.Plants != null)
            {
                foreach (var plant in db.Plants)
                {
                    if (plant == null ||
                        string.IsNullOrEmpty(plant.Name))
                        continue;

                    if (!plantNames.Contains(plant.Name))
                        plantNames.Add(plant.Name);
                }
            }

            var levelNames = new List<string>
            {
                AllLevelsOption
            };

            if (db.Levels != null)
            {
                foreach (var level in db.Levels)
                {
                    if (level == null ||
                        string.IsNullOrEmpty(level.Name))
                        continue;

                    if (!levelNames.Contains(level.Name))
                        levelNames.Add(level.Name);
                }
            }

            dropdownPlant.choices = plantNames;
            dropdownLevel.choices = levelNames;

            dropdownPlant.SetValueWithoutNotify(
                AllPlantsOption);

            dropdownLevel.SetValueWithoutNotify(
                AllLevelsOption);
        }

        // ==========================================
        // REFRESH ALL DATA
        // ==========================================

        private void RefreshLeaderboard()
        {
            if (leaderboardData == null ||
                tableBody == null)
                return;

            string plantId =
                MapDropdownToPlantId(
                    dropdownPlant.value);

            string levelId =
                MapDropdownToLevelId(
                    dropdownLevel.value);

            /*
             * GetTop is assumed to return
             * score-sorted players.
             *
             * Retrieve all entries to determine
             * current user rank outside Top 50.
             */

            List<LeaderboardItem> players =
                leaderboardData.GetTop(
                    plantId,
                    levelId,
                    int.MaxValue);

            if (players == null)
                players = new List<LeaderboardItem>();

            int visibleCount =
                Math.Min(TopLimit, players.Count);

            RefreshTable(players, visibleCount);

            RefreshTopPlayers(players);

            RefreshCurrentUser(players);

            // Reset horizontal scroll on filter change.
            tableScroll.scrollOffset = Vector2.zero;

            // Layout/viewport may update after rebuilding.
            tableScroll.schedule
                .Execute(UpdateScrollIndicators)
                .ExecuteLater(0);
        }

        // ==========================================
        // LEADERBOARD TABLE
        // ==========================================

        private void RefreshTable(
            List<LeaderboardItem> players,
            int visibleCount)
        {
            tableBody.Clear();

            resultCount.text =
                visibleCount + " pemain (Top 50)";

            if (visibleCount == 0)
            {
                var empty = new Label(
                    "Belum ada data untuk kategori ini.");

                empty.AddToClassList(
                    "leaderboard-empty");

                tableBody.Add(empty);
                return;
            }

            for (int i = 0; i < visibleCount; i++)
            {
                VisualElement row = CreateTableRow(
                    players[i],
                    i + 1,
                    i % 2 != 0);

                tableBody.Add(row);
            }
        }

        // ==========================================
        // CREATE TABLE ROW - ALL 8 COLUMNS
        // ==========================================

        private VisualElement CreateTableRow(
            LeaderboardItem data,
            int rank,
            bool alternate)
        {
            bool isCurrentUser = string.Equals(
                data.UserId,
                currentUserId,
                StringComparison.Ordinal);

            var row = new VisualElement();

            row.AddToClassList("leaderboard-row");

            if (alternate)
                row.AddToClassList(
                    "leaderboard-row-alt");

            if (isCurrentUser)
                row.AddToClassList(
                    "leaderboard-row-current");

            // RANK

            AddCell(
                row,
                "#" + rank,
                "lb-rank",
                isCurrentUser,
                true);

            // NAME / USER ID

            AddCell(
                row,
                data.UserId,
                "lb-name",
                isCurrentUser,
                true,
                true);

            // PLAY COUNT

            AddCell(
                row,
                data.PlayCount.ToString("N0"),
                "lb-play",
                isCurrentUser);

            // TOTAL SCORE

            AddCell(
                row,
                data.TotalScore.ToString("N0"),
                "lb-total",
                isCurrentUser,
                true);

            // AVERAGE ACCURACY

            string accuracy =
                data.HasAccuracy
                    ? data.AverageAccuracy.ToString("F1") + "%"
                    : "—";

            AddCell(
                row,
                accuracy,
                "lb-accuracy",
                isCurrentUser);

            // BEST SCORE

            AddCell(
                row,
                data.BestScore.ToString("N0"),
                "lb-best",
                isCurrentUser);

            // AVERAGE SCORE

            AddCell(
                row,
                data.AverageScore.ToString("N1"),
                "lb-average",
                isCurrentUser);

            // STARS

            Label starLabel = AddCell(
                row,
                data.Star + " ★",
                "lb-stars",
                isCurrentUser,
                true);

            starLabel.AddToClassList(
                "lb-stars-text");

            return row;
        }

        // ==========================================
        // ADD CELL
        // ==========================================

        private static Label AddCell(
            VisualElement row,
            string value,
            string columnClass,
            bool currentUser,
            bool strong = false,
            bool leftAlign = false)
        {
            var label = new Label(value ?? "—");

            label.AddToClassList("lb-cell");

            label.AddToClassList(columnClass);

            if (currentUser)
                label.AddToClassList("lb-current-text");

            if (strong)
                label.AddToClassList("lb-strong");

            if (leftAlign)
                label.AddToClassList("lb-align-left");

            row.Add(label);

            return label;
        }

        // ==========================================
        // TOP 3 PLAYERS
        // ==========================================

        private void RefreshTopPlayers(
            List<LeaderboardItem> players)
        {
            SetTopPlayer(
                players, 0,
                top1Name, top1Score, top1Stars);

            SetTopPlayer(
                players, 1,
                top2Name, top2Score, top2Stars);

            SetTopPlayer(
                players, 2,
                top3Name, top3Score, top3Stars);
        }

        private static void SetTopPlayer(
            List<LeaderboardItem> players,
            int index,
            Label nameLabel,
            Label scoreLabel,
            Label starsLabel)
        {
            if (index < players.Count)
            {
                LeaderboardItem player =
                    players[index];

                nameLabel.text =
                    player.UserId;

                scoreLabel.text =
                    player.TotalScore.ToString("N0") +
                    " Poin";

                starsLabel.text =
                    player.Star + " ★";
            }
            else
            {
                nameLabel.text =
                    "Belum ada pemain";

                scoreLabel.text = "—";
                starsLabel.text = "—";
            }
        }

        // ==========================================
        // CURRENT USER RANK
        // ==========================================

        private void RefreshCurrentUser(
            List<LeaderboardItem> players)
        {
            if (string.IsNullOrEmpty(currentUserId))
            {
                SetEmptyCurrentUser(
                    "Pengguna belum teridentifikasi.");

                return;
            }

            for (int i = 0; i < players.Count; i++)
            {
                LeaderboardItem player = players[i];

                if (!string.Equals(
                    player.UserId,
                    currentUserId,
                    StringComparison.Ordinal))
                {
                    continue;
                }

                int rank = i + 1;

                myRank.text = "#" + rank;

                myName.text = player.UserId;

                myScore.text =
                    player.TotalScore.ToString("N0") +
                    " Poin";

                myStars.text =
                    player.Star + " ★";

                if (rank == 1)
                {
                    myStatus.text =
                        "Kamu berada di peringkat pertama!";
                }
                else if (rank <= 3)
                {
                    myStatus.text =
                        "Kamu masuk Top 3!";
                }
                else if (rank <= TopLimit)
                {
                    myStatus.text =
                        "Kamu masuk Top 50!";
                }
                else
                {
                    myStatus.text =
                        "Peringkat berdasarkan kategori terpilih";
                }

                return;
            }

            SetEmptyCurrentUser(
                "Belum memiliki skor pada kategori ini.");
        }

        private void SetEmptyCurrentUser(
            string message)
        {
            myRank.text = "—";

            myName.text =
                string.IsNullOrEmpty(currentUserId)
                    ? "—"
                    : currentUserId;

            myScore.text = "—";
            myStars.text = "—";
            myStatus.text = message;
        }

        // ==========================================
        // SCROLL HINT SETUP
        // ==========================================

        private void SetupScrollEffects()
        {
            // Ensure the overlays never intercept
            // mouse, touch, or pointer interaction.

            DisablePickingRecursive(scrollFadeLeft);
            DisablePickingRecursive(scrollFadeRight);

            // Hints are hidden until a layout
            // measurement confirms scrolling.

            scrollFadeLeft.style.display =
                DisplayStyle.None;

            scrollFadeRight.style.display =
                DisplayStyle.None;

            // Listen to horizontal scrolling.

            tableScroll.horizontalScroller.slider
                .RegisterValueChangedCallback(
                    OnHorizontalScrollChanged);

            // Listen to viewport size changes.

            tableScroll.contentViewport
                .RegisterCallback<GeometryChangedEvent>(
                    OnScrollGeometryChanged);

            // Listen to table size changes.

            leaderboardTable
                .RegisterCallback<GeometryChangedEvent>(
                    OnScrollGeometryChanged);

            // Lightweight pulsing animation.
            // Executes approximately every 60ms,
            // not in MonoBehaviour.Update().

            scrollHintAnimation =
                tableScroll.schedule
                    .Execute(AnimateScrollHints)
                    .Every(60);

            // Re-evaluate after the first layout.

            tableScroll.schedule
                .Execute(UpdateScrollIndicators)
                .ExecuteLater(0);
        }

        // ==========================================
        // DISABLE POINTER PICKING
        // ==========================================

        private static void DisablePickingRecursive(
            VisualElement element)
        {
            if (element == null)
                return;

            element.pickingMode = PickingMode.Ignore;

            for (int i = 0; i < element.childCount; i++)
            {
                DisablePickingRecursive(element[i]);
            }
        }

        // ==========================================
        // SCROLL EVENTS
        // ==========================================

        private void OnHorizontalScrollChanged(
            ChangeEvent<float> evt)
        {
            UpdateScrollIndicators();
        }

        private void OnScrollGeometryChanged(
            GeometryChangedEvent evt)
        {
            UpdateScrollIndicators();
        }

        // ==========================================
        // DETERMINE WHICH HINT TO DISPLAY
        // ==========================================

        private void UpdateScrollIndicators()
        {
            if (tableScroll == null ||
                leaderboardTable == null ||
                scrollFadeLeft == null ||
                scrollFadeRight == null)
                return;

            float tableWidth =
                leaderboardTable.resolvedStyle.width;

            float viewportWidth =
                tableScroll.contentViewport.resolvedStyle.width;

            if (float.IsNaN(tableWidth) ||
                float.IsNaN(viewportWidth) ||
                tableWidth <= 0f ||
                viewportWidth <= 0f)
                return;

            float maxScrollX =
                Mathf.Max(0f, tableWidth - viewportWidth);

            float currentScrollX =
                tableScroll.scrollOffset.x;

            bool canScroll =
                maxScrollX > ScrollEdgeTolerance;

            // LEFT appears if some content
            // is hidden on the left side.

            showLeftHint =
                canScroll &&
                currentScrollX > ScrollEdgeTolerance;

            // RIGHT appears if some content
            // is hidden on the right side.

            showRightHint =
                canScroll &&
                currentScrollX <
                maxScrollX - ScrollEdgeTolerance;

            scrollFadeLeft.style.display =
                showLeftHint
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;

            scrollFadeRight.style.display =
                showRightHint
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
        }

        // ==========================================
        // ANIMATED ARROWS
        // ==========================================

        private void AnimateScrollHints()
        {
            if (scrollArrowLeft == null ||
                scrollArrowRight == null)
                return;

            // Smooth periodic motion.

            float time = Time.realtimeSinceStartup;

            float wave =
                Mathf.Sin(time * 3.0f);

            // Opacity between 0.55 and 1.

            float opacity =
                0.775f + wave * 0.225f;

            // Small horizontal movement.

            float movement =
                wave * 4f;

            if (showLeftHint)
            {
                scrollArrowLeft.style.opacity = opacity;

                scrollArrowLeft.transform.position =
                    new Vector3(
                        -movement,
                        0f,
                        0f);
            }

            if (showRightHint)
            {
                scrollArrowRight.style.opacity = opacity;

                scrollArrowRight.transform.position =
                    new Vector3(
                        movement,
                        0f,
                        0f);
            }
        }

        // ==========================================
        // PLANT FILTER MAPPING
        // ==========================================

        private string MapDropdownToPlantId(
            string dropdownValue)
        {
            if (dropdownValue == AllPlantsOption)
                return Leaderboard.TotalCategory;

            if (db.Plants != null)
            {
                foreach (var plant in db.Plants)
                {
                    if (plant == null)
                        continue;

                    if (string.Equals(
                        plant.Name,
                        dropdownValue,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return plant.Id;
                    }
                }
            }

            return dropdownValue;
        }

        // ==========================================
        // LEVEL FILTER MAPPING
        // ==========================================

        private string MapDropdownToLevelId(
            string dropdownValue)
        {
            if (dropdownValue == AllLevelsOption)
                return Leaderboard.TotalCategory;

            if (db.Levels != null)
            {
                foreach (var level in db.Levels)
                {
                    if (level == null)
                        continue;

                    if (string.Equals(
                        level.Name,
                        dropdownValue,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return level.Id;
                    }
                }
            }

            return dropdownValue;
        }

        // ==========================================
        // CLEANUP
        // ==========================================

        private void OnDestroy()
        {
            if (backButton != null)
            {
                backButton.clicked -= OnBackClicked;
            }

            if (dropdownPlant != null)
            {
                dropdownPlant.UnregisterValueChangedCallback(
                    OnFilterChanged);
            }

            if (dropdownLevel != null)
            {
                dropdownLevel.UnregisterValueChangedCallback(
                    OnFilterChanged);
            }

            if (leaderboardContent != null)
            {
                leaderboardContent
                    .UnregisterCallback<GeometryChangedEvent>(
                        OnContentGeometryChanged);
            }

            if (tableScroll != null)
            {
                tableScroll.horizontalScroller.slider
                    .UnregisterValueChangedCallback(
                        OnHorizontalScrollChanged);

                tableScroll.contentViewport
                    .UnregisterCallback<GeometryChangedEvent>(
                        OnScrollGeometryChanged);
            }

            if (leaderboardTable != null)
            {
                leaderboardTable
                    .UnregisterCallback<GeometryChangedEvent>(
                        OnScrollGeometryChanged);
            }

            if (scrollHintAnimation != null)
            {
                scrollHintAnimation.Pause();
                scrollHintAnimation = null;
            }
        }
    }
}
