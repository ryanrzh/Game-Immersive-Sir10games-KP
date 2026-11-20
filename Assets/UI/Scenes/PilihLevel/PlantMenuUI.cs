using UnityEngine;
using UnityEngine.UIElements;

namespace Game.APD
{
    public class PlantMenuUI : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private VisualTreeAsset cardTemplate;

        [Header("Data Level Tersedia")]
        [Tooltip("Masukkan 4 asset Level di sini (Level 1, 2, 3, 4)")]
        public Level[] cards;

        private void Start()
        {
            var data = APDGameManager.Instance.PlantSelectedData;
            var root = uiDocument.rootVisualElement;
            var backButton = root.Q<Button>("btn-back");
            var txtInfoPlant = root.Q<Label>("lbl-info-plant");
            txtInfoPlant.text = $"{data.Name}\n{data.Description}";

            backButton.RegisterCallback<ClickEvent>(ev =>
            {
                GameManager.LoadScene("02_PilihPlantMenu");
            });

            var cardContainer = root.Q<VisualElement>("card-container");

            for (int i = 0; i < cards.Length; i++)
            {
                var levelData = cards[i];
                var levelNumber = i + 1;

                // 1. Buat instance dari template kartu
                var newCard = cardTemplate.Instantiate();

                // 2. Isi Teks (Badge, Judul, Deskripsi)
                var badgeLabel = newCard.Q<Label>("card-badge");
                if (badgeLabel != null) badgeLabel.text = $"LEVEL {levelNumber}";

                var titleLabel = newCard.Q<Label>("card-title");
                if (titleLabel != null) titleLabel.text = levelData.Name;

                var descLabel = newCard.Q<Label>("card-desc");
                if (descLabel != null) descLabel.text = levelData.Description;

                // 3. Isi Gambar Background Kartu (jika ada VectorImage)
                var imageArea = newCard.Q<VisualElement>("card-image");
                if (imageArea != null && levelData.VectorImage != null)
                {
                    imageArea.style.backgroundImage = new StyleBackground(levelData.VectorImage);
                }

                // 4. Atur Bintang (Contoh Simulasi. Nanti ganti dengan data Save/PlayerPrefs)
                // Misalnya: Fungsi GetStarsForLevel(levelNumber) mereturn 0, 1, 2, atau 3.
                int playerStars = 0; // Ganti ini dengan data asli pemain

                if (playerStars >= 1) newCard.Q<Label>("star-1")?.AddToClassList("tint-amber-500");
                if (playerStars >= 2) newCard.Q<Label>("star-2")?.AddToClassList("tint-amber-500");
                if (playerStars >= 3) newCard.Q<Label>("star-3")?.AddToClassList("tint-amber-500");

                // 5. Register Event Klik untuk Memulai Level
                // Karena newCard bertipe TemplateContainer, kita cari elemen tombol/kartu utama di dalamnya
                var cardRootButton = newCard.Q<VisualElement>("card");
                if (cardRootButton != null)
                {
                    cardRootButton.RegisterCallback<ClickEvent>(ev =>
                    {
                        Debug.Log($"Memulai: {levelData.Name} (Tipe: {levelData.Type})");
                        APDGameManager.Instance.StartGame(levelNumber);
                    });
                }

                // 6. Masukkan kartu yang sudah dirakit ke dalam container di layar
                cardContainer.Add(newCard);
            }
        }
    }
}