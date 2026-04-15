using UnityEngine;
using System.Collections.Generic;
using System;
namespace GameUp.Core.Tutorial
{
    [CreateAssetMenu(fileName = "TutorialController", menuName = "Data/Tutorial/TutorialController")]
    public class SOTutorialController : ScriptableObjectSingleton<SOTutorialController>
    {
        public List<SOTutorialType> tutorialTypes;
        [NonSerialized] private Dictionary<TutorialType, SOTutorialType> _cacheTutorials;

        public bool IsCompleteAll
        {
            get
            {
                foreach (var tutorial in Instance.tutorialTypes)
                {
                    if (!tutorial.IsComplete) return false;
                }
                return true;
            }
        }
        public static Dictionary<TutorialType, SOTutorialType> Tutorials
        {
            get
            {
                if (Instance._cacheTutorials != null) return Instance._cacheTutorials;
                Instance._cacheTutorials = new Dictionary<TutorialType, SOTutorialType>();
                for (var i = 0; i < Instance.tutorialTypes.Count; i++)
                {
                    Instance._cacheTutorials.Add(Instance.tutorialTypes[i].tutorialType, Instance.tutorialTypes[i]);
                }
                return Instance._cacheTutorials;
            }
        }

        public void SetComplete(TutorialType tutorialType)
        {
            if (Tutorials.TryGetValue(tutorialType, out var tutorial))
            {
                tutorial.SetComplete();
            }
        }

        public void UnComplete(TutorialType tutorialType)
        {
            if (Tutorials.TryGetValue(tutorialType, out var tutorial))
            {
                tutorial.UnComplete();
            }
        }
    }
}