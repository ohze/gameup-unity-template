using UnityEngine;
using TMPro;
using System.Collections;

namespace GameUp.Core.Tutorial
{
    public class TalkTutorial : MonoBehaviour
    {
        [SerializeField] private TMP_Text destinationTalkTxt;
        [SerializeField] private RectTransform boxDestinationTalk;

        private Coroutine _coroutineText;

        public void ShowTalk(string fullText)
        {
            RunRevealText(fullText);
        }

        public void HideTalk()
        {
            boxDestinationTalk.Hide();
            StopRevealText();
            destinationTalkTxt.text = string.Empty;
            destinationTalkTxt.maxVisibleCharacters = 0;
        }

        public void SetPosition(Vector2 position)
        {
            boxDestinationTalk.anchoredPosition = position;
        }

        private void RunRevealText(string fullText)
        {
            boxDestinationTalk.Show();
            StopRevealText();

            string normalizedText = string.IsNullOrEmpty(fullText) ? string.Empty : fullText.Replace("\\n", "\n");
            destinationTalkTxt.text = normalizedText;

            if (normalizedText.Length == 0)
            {
                destinationTalkTxt.maxVisibleCharacters = 0;
                return;
            }

            destinationTalkTxt.ForceMeshUpdate();
            destinationTalkTxt.maxVisibleCharacters = 0;
            _coroutineText = StartCoroutine(RevealText());
        }

        private IEnumerator RevealText()
        {
            int totalVisibleCharacters = destinationTalkTxt.textInfo.characterCount;
            for (int i = 0; i <= totalVisibleCharacters; i++)
            {
                destinationTalkTxt.maxVisibleCharacters = i;
                yield return null;
            }

            _coroutineText = null;
        }

        private void StopRevealText()
        {
            if (_coroutineText == null)
            {
                return;
            }

            StopCoroutine(_coroutineText);
            _coroutineText = null;
        }

        private void OnDisable()
        {
            StopRevealText();
        }
    }
}