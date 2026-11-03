using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Game.APD
{
    public class SelectPlantMenuUI : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private VisualTreeAsset card;
        [SerializeField] private Plant[] items;

        private void Start()
        {
            var root = uiDocument.rootVisualElement;
            var cardContainer = root.Q<VisualElement>("CardContainer");

            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                var newCard = card.Instantiate();
                newCard.Q<VisualElement>("image").style.backgroundImage = new StyleBackground(item.VectorImage);
                newCard.Q<Label>("item-name").text = item.Name;
                newCard.Q<Label>("description").text = item.Description;
                newCard.RegisterCallback<ClickEvent>(ev =>
                {
                    APDGameManager.Instance.SetPlantSelectedData(item);
                    GameManager.LoadScene("PlantMenu");
                });

                cardContainer.Add(newCard);

                if (items[i].IsLocked)
                {
                    newCard.SetEnabled(false);
                    newCard.style.opacity = 0.5f;
                }
            }

            var btnPanduan = root.Q<Button>("btn-panduan");
            btnPanduan.RegisterCallback<ClickEvent>(ev =>
            {
                GameManager.LoadScene("PanduanMenu");
                print("Clicked on: Panduan");
            });

            var btnLeaderboard = root.Q<Button>("btn-leaderboard");
            btnLeaderboard.RegisterCallback<ClickEvent>(ev =>
            {
                GameManager.LoadScene("LeaderboardMenu");
                print("Clicked on: Leaderboard");
            });

            var btnBack = root.Q<Button>("btn-back");
            btnBack.RegisterCallback<ClickEvent>(ev =>
            {
                GameManager.LoadScene("StartScreen");
            });
        }
    }
}
