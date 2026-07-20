using System;
using System.Collections;
using GameUp.Core;
using UnityEngine;

namespace GameUp.SDK
{
    public class TimerHelper : MonoSingleton<TimerHelper>
    {
        public static void Schedule(float time, Action callback)
        {
            Instance.StartCoroutine(Instance.IESchedule(time, callback));
        }

        private IEnumerator IESchedule(float time, Action callback)
        {
            yield return new WaitForSeconds(time);
            callback?.Invoke();
        }
    }
}