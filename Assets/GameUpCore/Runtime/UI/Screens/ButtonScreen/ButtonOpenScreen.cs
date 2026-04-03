using System;
using System.Collections.Generic;
using System.Reflection;
using GameUp.Core;
using UnityEngine;
using UnityEngine.UI;

namespace GameUp.Core.UI
{
    [DisallowMultipleComponent]
    public class ButtonOpenScreen : MonoBehaviour
    {
        [SerializeField] private Button btn;
        [SerializeField] private bool rememberInHistory = true;
        [SerializeField] private string screenTypeName;

        private static readonly Dictionary<string, Type> ScreenTypeCache = new(StringComparer.Ordinal);

        private void OnEnable()
        {
            if (btn != null)
            {
                btn.onClick.AddListener(OpenSelectedScreen);
            }
        }

        private void OnDisable()
        {
            if (btn != null)
            {
                btn.onClick.RemoveListener(OpenSelectedScreen);
            }
        }

        public void OpenSelectedScreen()
        {
            if (string.IsNullOrWhiteSpace(screenTypeName))
            {
                GULogger.Warning("ButtonOpenScreen", $"{name}: No UIScreen type selected.");
                return;
            }

            var type = ResolveUIScreenType(screenTypeName);
            if (type == null)
            {
                GULogger.Warning("ButtonOpenScreen", $"{name}: UIScreen type `{screenTypeName}` not found.");
                return;
            }

            UIScreen.OpenScreenByTypeAsync(type, rememberInHistory);
        }

        private static Type ResolveUIScreenType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            if (ScreenTypeCache.TryGetValue(typeName, out var cached))
            {
                return cached;
            }

            Type found = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.Name != typeName)
                        {
                            continue;
                        }

                        if (!typeof(UIScreen).IsAssignableFrom(type) || type.IsAbstract)
                        {
                            continue;
                        }

                        found = type;
                        break;
                    }
                }
                catch (ReflectionTypeLoadException)
                {
                    continue;
                }

                if (found != null)
                {
                    break;
                }
            }

            ScreenTypeCache[typeName] = found;
            return found;
        }

        private void OnValidate()
        {
            if (btn == null)
            {
                btn = GetComponent<Button>() ?? GetComponentInChildren<Button>(true);
            }
        }
    }
}
