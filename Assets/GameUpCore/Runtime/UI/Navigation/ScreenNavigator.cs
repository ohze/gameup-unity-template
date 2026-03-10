using System;
using System.Collections.Generic;

namespace GameUp.UI
{
    /// <summary>
    /// Stack-based screen navigation. Push/Pop screens with history tracking.
    /// </summary>
    public sealed class ScreenNavigator
    {
        readonly Stack<UIScreen> _history = new();

        public UIScreen CurrentScreen { get; private set; }
        public int HistoryCount => _history.Count;

        public event Action<UIScreen> OnScreenChanged;

        /// <summary>Push a new screen, hiding the current one.</summary>
        public void Push(UIScreen screen)
        {
            if (screen == null) return;

            if (CurrentScreen != null)
            {
                _history.Push(CurrentScreen);
                CurrentScreen.Hide();
            }

            CurrentScreen = screen;
            CurrentScreen.Show();
            OnScreenChanged?.Invoke(CurrentScreen);
        }

        /// <summary>Pop the current screen and show the previous one. Returns false if no history.</summary>
        public bool Pop()
        {
            if (_history.Count == 0) return false;

            CurrentScreen?.Hide();
            CurrentScreen = _history.Pop();
            CurrentScreen.Show();
            OnScreenChanged?.Invoke(CurrentScreen);
            return true;
        }

        /// <summary>Replace the current screen without pushing to history.</summary>
        public void Replace(UIScreen screen)
        {
            if (screen == null) return;
            CurrentScreen?.Hide();
            CurrentScreen = screen;
            CurrentScreen.Show();
            OnScreenChanged?.Invoke(CurrentScreen);
        }

        /// <summary>Pop all screens and show the root (first pushed).</summary>
        public void PopToRoot()
        {
            while (_history.Count > 0)
            {
                CurrentScreen?.Hide();
                CurrentScreen = _history.Pop();
            }
            CurrentScreen?.Show();
            OnScreenChanged?.Invoke(CurrentScreen);
        }

        /// <summary>Clear all history and hide current screen.</summary>
        public void Clear()
        {
            CurrentScreen?.Hide();
            CurrentScreen = null;
            _history.Clear();
        }
    }
}
