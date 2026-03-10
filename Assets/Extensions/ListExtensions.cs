using System.Collections.Generic;
using UnityEngine;

namespace GameUp.Extensions
{
    public static class ListExtensions
    {
        /// <summary>Return a random element from the list.</summary>
        public static T GetRandom<T>(this IList<T> list)
            => list.Count == 0 ? default : list[Random.Range(0, list.Count)];

        /// <summary>Shuffle the list in-place using Fisher-Yates algorithm.</summary>
        public static void Shuffle<T>(this IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>Returns true if the index is valid for this list.</summary>
        public static bool IsValidIndex<T>(this IList<T> list, int index)
            => index >= 0 && index < list.Count;

        /// <summary>Remove and return the last element.</summary>
        public static T PopLast<T>(this IList<T> list)
        {
            if (list.Count == 0) return default;
            int lastIndex = list.Count - 1;
            var item = list[lastIndex];
            list.RemoveAt(lastIndex);
            return item;
        }

        /// <summary>Add item only if it's not already in the list.</summary>
        public static bool AddUnique<T>(this IList<T> list, T item)
        {
            if (list.Contains(item)) return false;
            list.Add(item);
            return true;
        }
    }
}
