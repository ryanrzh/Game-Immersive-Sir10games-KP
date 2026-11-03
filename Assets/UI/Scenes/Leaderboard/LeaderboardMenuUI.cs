using UnityEngine;
using UnityEngine.UIElements;

namespace Game.APD
{
    public class LeaderboardMenuUI : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;

        private void Start()
        {
            var root = uiDocument.rootVisualElement;
            var btnBack = root.Q<Button>("btn-back");
            btnBack.RegisterCallback<ClickEvent>(evt =>
            {
                GameManager.LoadScene("PlantMenu", false);
            });
        }
    }
}
