using UnityEngine;
using System;
using GameUp.Core.Helpers;
namespace GameUp.Core.Tutorial
{
    [CreateAssetMenu(fileName = "TutorialStep", menuName = "Data/Tutorial/TutorialStep")]
    public class SOTutorialStep : ScriptableObject
    {
        public string stepName;

        // Focus
        public bool useFocus;
        public FocusType focusType;
        public DestinationType destinationPoint;
        public DestinationType destinationPoint2;

        // Talk
        public bool useTalk;
        public string talkText;

        // Arrow
        public bool useArrow;
        public bool useHandDrag;
        public ArrowDirection arrowDirection;

        [NonSerialized] private bool _isComplete;
        public bool Complete => _isComplete;
        public void MarkComplete()
        {
            _isComplete = true;
        }

        public void UnComplete()
        {
            _isComplete = false;
        }
    }

    public enum FocusType
    {
        Single,
        Multi,
    }
}