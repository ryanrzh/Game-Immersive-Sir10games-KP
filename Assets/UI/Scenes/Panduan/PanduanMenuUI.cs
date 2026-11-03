using UnityEngine;
using UnityEngine.UIElements;

namespace Game.APD
{
    public class PanduanMenuUI : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        private void Start()
        {
            var root = uiDocument.rootVisualElement;
            var btnBack = root.Q<Button>("btn-back");
            btnBack.RegisterCallback<ClickEvent>(ev =>
            {
                GameManager.BackToPreviousScene();
            });
        }
    }

}