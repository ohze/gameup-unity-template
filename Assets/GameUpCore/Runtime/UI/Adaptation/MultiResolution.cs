using UnityEngine;
using UnityEngine.UI;

namespace GameUp.Core.UI
{
    public class MultiResolution : MonoBehaviour
    {
        [SerializeField] private CanvasScaler canvasScaler;

        private void OnValidate()
        {
            canvasScaler = GetComponent<CanvasScaler>();
        }

        private void Start()
        {
            ChangeMatchScreen();
        }

        private void ChangeMatchScreen()
        {
            var baseWith = canvasScaler.referenceResolution.x;
            var baseHeight = canvasScaler.referenceResolution.y;
            var currentWidth = Screen.width;
            var currentHeight = Screen.height;

            var ratio = (currentWidth / (float)currentHeight) / (baseWith / baseHeight);
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = ratio >= 1 ? 1 : 0;
        }
    }
}