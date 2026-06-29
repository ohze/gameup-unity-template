using System;
using System.Collections.Generic;
using System.Reflection;
using GameUp.Core;
using UnityEngine;
using UnityEngine.UI;

namespace GameUp.Core.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    [RequireComponent(typeof(CustomSelectButton))]
    public class CustomButtonOpenScreen : MonoBehaviour
    {
        [SerializeField] private Button btn;
        [SerializeField] private CustomSelectButton _selectButton;
        [SerializeField] private bool rememberInHistory = true;
        [SerializeField] private string screenTypeName;

        private static readonly Dictionary<string, Type> ScreenTypeCache = new(StringComparer.Ordinal);
        private static readonly List<CustomButtonOpenScreen> AllInstances = new();

        private void OnEnable()
        {
            AllInstances.Add(this);

            if (btn != null)
                btn.onClick.AddListener(OpenSelectedScreen);

            UIScreen.OnCurrentScreenChanged += OnCurrentScreenChanged;
            RefreshSelectState();
        }

        private void OnDisable()
        {
            AllInstances.Remove(this);

            if (btn != null)
                btn.onClick.RemoveListener(OpenSelectedScreen);

            UIScreen.OnCurrentScreenChanged -= OnCurrentScreenChanged;
        }

        private void OnCurrentScreenChanged(UIScreen screen)
        {
            if (_selectButton == null) return;
            var type = ResolveUIScreenType(screenTypeName);
            _selectButton.IsSelect = type != null && screen != null && screen.GetType() == type;
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

        public void RefreshSelectState()
        {
            if (_selectButton == null || UIScreen.currentScreen == null) return;

            var type = ResolveUIScreenType(screenTypeName);
            _selectButton.IsSelect = type != null && UIScreen.currentScreen.GetType() == type;
        }

        public static void RefreshAllSelectStates()
        {
            if (UIScreen.currentScreen == null) return;

            var currentType = UIScreen.currentScreen.GetType();
            foreach (var instance in AllInstances)
            {
                if (instance == null || instance._selectButton == null) continue;

                var type = ResolveUIScreenType(instance.screenTypeName);
                instance._selectButton.IsSelect = type != null && type == currentType;
            }
        }

        private static Type ResolveUIScreenType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName)) return null;

            if (ScreenTypeCache.TryGetValue(typeName, out var cached)) return cached;

            Type found = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.Name != typeName) continue;
                        if (!typeof(UIScreen).IsAssignableFrom(type) || type.IsAbstract) continue;

                        found = type;
                        break;
                    }
                }
                catch (ReflectionTypeLoadException)
                {
                    continue;
                }

                if (found != null) break;
            }

            ScreenTypeCache[typeName] = found;
            return found;
        }

        private void OnValidate()
        {
            _selectButton = GetComponent<CustomSelectButton>();
            if (btn != null) return;
            btn = GetComponent<Button>();
            if (btn == null) btn = GetComponentInChildren<Button>(true);
        }
    }
}
