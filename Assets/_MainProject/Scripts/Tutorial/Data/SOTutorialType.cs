using UnityEngine;
using System.Collections.Generic;
using System;

namespace GameUp.Core.Tutorial
{
    [CreateAssetMenu(fileName = "TutorialType", menuName = "Data/Tutorial/TutorialType")]
    public class SOTutorialType : ScriptableObject
    {
        public TutorialType tutorialType;
        public List<SOTutorialStep> tutorialSteps;
        [NonSerialized] private bool? _isComplete;
        private string Key => $"Tut_{tutorialType}";

        public bool IsComplete
        {
            get
            {
                _isComplete ??= LocalStorageUtils.GetBoolean(Key);
                return _isComplete.Value;
            }
        }

        public void SetComplete()
        {
            _isComplete = true;
            LocalStorageUtils.SetBoolean(Key, true);
        }

        public void UnComplete()
        {
            _isComplete = false;
            LocalStorageUtils.SetBoolean(Key, false);
        }
    }

    public enum TutorialType
    {
        TutorialTest_1,
        TutorialTest_2,
        TutorialTest_3,
        TutorialTest_4,
        TutorialTest_5,
        TutorialTest_6,
        TutorialTest_7,
        TutorialTest_8,
        TutorialTest_9,
    }
}