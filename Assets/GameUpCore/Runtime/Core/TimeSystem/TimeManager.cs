using System.Collections.Generic;
using UnityEngine;

namespace GameUp.Core
{
    /// <summary>
    /// Manages custom time layers independent of Time.timeScale.
    /// Allows pausing individual layers (game, UI, effects) without affecting others.
    /// </summary>
    public sealed class TimeManager : PersistentSingleton<TimeManager>
    {
        readonly Dictionary<string, TimeLayer> _layers = new();

        public const string GameLayer = "Game";
        public const string UILayer = "UI";
        public const string EffectLayer = "Effect";

        protected override void OnSingletonAwake()
        {
            RegisterLayer(GameLayer);
            RegisterLayer(UILayer);
            RegisterLayer(EffectLayer);
        }

        void Update()
        {
            foreach (var layer in _layers.Values)
                layer.Update(Time.unscaledDeltaTime);
        }

        public void RegisterLayer(string layerName)
        {
            if (!_layers.ContainsKey(layerName))
                _layers[layerName] = new TimeLayer(layerName);
        }

        /// <summary>Get custom delta time for a specific layer.</summary>
        public float GetDeltaTime(string layerName = GameLayer)
            => _layers.TryGetValue(layerName, out var layer) ? layer.DeltaTime : 0f;

        /// <summary>Get custom time scale for a specific layer.</summary>
        public float GetTimeScale(string layerName = GameLayer)
            => _layers.TryGetValue(layerName, out var layer) ? layer.TimeScale : 1f;

        public void SetTimeScale(string layerName, float scale)
        {
            if (_layers.TryGetValue(layerName, out var layer))
                layer.TimeScale = scale;
        }

        public void PauseLayer(string layerName)
            => SetTimeScale(layerName, 0f);

        public void ResumeLayer(string layerName)
            => SetTimeScale(layerName, 1f);

        public bool IsLayerPaused(string layerName)
            => _layers.TryGetValue(layerName, out var layer) && layer.TimeScale == 0f;

        sealed class TimeLayer
        {
            public string Name { get; }
            public float TimeScale { get; set; } = 1f;
            public float DeltaTime { get; private set; }
            public float TotalTime { get; private set; }

            public TimeLayer(string name) => Name = name;

            public void Update(float unscaledDeltaTime)
            {
                DeltaTime = unscaledDeltaTime * TimeScale;
                TotalTime += DeltaTime;
            }
        }
    }
}
