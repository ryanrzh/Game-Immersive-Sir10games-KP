using UnityEngine;
using UnityEngine.UIElements;

namespace Game.APD
{
    public class  PlantMenuUI: MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;

        private void Start()
        {
            var data = APDGameManager.Instance.PlantSelectedData;
            var root = uiDocument.rootVisualElement;
            var wallpaper = root.Q<VisualElement>("plant-image");
            var plantNameLabel = root.Q<Label>("plant-name");
            var backButton = root.Q<Button>("btn-back");
            var infoButton = root.Q<Button>("btn-info");
            var leaderboardButton = root.Q<Button>("btn-leaderboard");
            var startButton = root.Q<Button>("btn-start");

            wallpaper.style.backgroundImage = new StyleBackground(data.VectorImage);
            plantNameLabel.text = $"{data.Name}: {data.Description}";

            backButton.RegisterCallback<ClickEvent>(ev =>
            {
                GameManager.BackToPreviousScene();
            });

            infoButton.RegisterCallback<ClickEvent>(ev =>
            {
                GameManager.LoadScene("InfoBahayaMenu", false);
            });

            leaderboardButton.RegisterCallback<ClickEvent>(ev =>
            {
                GameManager.LoadScene("LeaderboardMenu", false);
            });
        }
    }
}