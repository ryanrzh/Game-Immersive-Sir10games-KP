using UnityEngine;
using UnityEngine.UIElements;

namespace Game.APD
{
    public class StartScreenUI : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;

        private void Start()
        {
            var root = uiDocument.rootVisualElement;
            var btnStart = root.Q<Button>("btn-start");
            var btnLeaderboard = root.Q<Button>("btn-leaderboard");
            btnLeaderboard.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);


            btnStart.RegisterCallback<ClickEvent>(evt =>
            {
                GameManager.LoadScene("PilihPlantMenu");
            });

            btnLeaderboard.RegisterCallback<ClickEvent>(evt =>
            {
                GameManager.LoadScene("LeaderboardMenu");
            });
        }
    }
}
