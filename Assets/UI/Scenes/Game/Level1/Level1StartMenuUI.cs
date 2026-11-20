using UnityEngine;
using UnityEngine.UIElements;

namespace Game.APD
{
    public class Level1StartMenuUI : MonoBehaviour
    {
        [SerializeField] private UIDocument uIDocument;
        private void Start()
        {
            var root = uIDocument.rootVisualElement;
            var btnStart = root.Q<Button>("btn-start");
            btnStart.clicked += OnStartButtonClicked;

            var btnHome = root.Q<Button>("btn-home");
            btnHome.clicked += OnHomeButtonClicked;
        }

        public void OnStartButtonClicked()
        {
            GameManager.LoadScene("Level 1 Game Menu");
        }

        public void OnHomeButtonClicked()
        {
            GameManager.LoadScene("02_PilihPlantMenu");
        }
    }
}