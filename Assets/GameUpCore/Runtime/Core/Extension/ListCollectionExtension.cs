using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace GameUp.Core
{
    public static class ListCollectionExtension
    {

        private static readonly System.Random Rng = new();

        public static List<T> Split<T>(this List<T> ls, int start, int length)
        {
            if (ls.Count < start + length)
            {
                GULogger.Error("ListCollectionExtension", "Chiều dài list không đủ");
                return null;
            }
            var l = new List<T>();
            for (var i = start; i < start + length; i++) l.Add(ls[i]);
            return l;
        }

        public static List<T> Splice<T>(this List<T> list, int offset, int count)
        {
            var startIdx = offset < 0 ? list.Count + offset : offset;
            var result = list.Skip(startIdx).Take(count).ToList();
            list.RemoveRange(startIdx, count);
            return result;
        }

        public static List<T> SpliceGetLast<T>([NotNull] this List<T> ls, int count)
        {
            var temp = new List<T>();
            for (var i = 0; i < count; i++)
            {
                var x = ls.Count - (count - i);
                if (x >= 0) temp.Add(ls[x]);
            }
            return temp;
        }

        public static void SetCount<T>(this List<T> list, int count, T defaulValue)
        {
            for (var i = 0; i < count; i++) list.Add(defaulValue);
        }

        public static List<T> Clone<T>(this List<T> list) where T : struct
        {
            var temp = new List<T>();
            for (var i = 0; i < list.Count; i++) temp.Add(list[i]);
            return temp;
        }

        public static List<T> GetClone<T>(this List<T> list)
        {
            var newList = new List<T>();
            for (var index = 0; index < list.Count; index++)
            {
                var o = list[index];
                newList.Add(o);
            }
            return newList;
        }

        public static int SumRange(this List<int> ls, int index)
        {
            if (index > ls.Count)
            {
                GULogger.Error("ListCollectionExtension", "index out list");
                return 0;
            }
            var t = 0;
            for (var i = 0; i < index; i++) t += ls[i];
            return t;
        }

        public static T[] Clone<T>(this T[] list) where T : struct
        {
            var temp = new T[list.Length];
            for (var i = 0; i < list.Length; i++) temp[i] = list[i];
            return temp;
        }

        public static void Shuffle<T>(this IList<T> list)
        {
            var n = list.Count;
            while (n > 1)
            {
                n--;
                var k = Rng.Next(n + 1);
                (list[k], list[n]) = (list[n], list[k]);
            }
        }

        public static void Shuffle<T>(this List<T> list)
        {
            for (var i = 0; i < list.Count; i++)
            {
                var randomIndex = Random.Range(i, list.Count);
                (list[i], list[randomIndex]) = (list[randomIndex], list[i]);
            }
        }

        public static T GetRandom<T>(this List<T> list)
        {
            return list[Random.Range(0, list.Count)];
        }

        public static T GetRandom<T>(this T[] arr)
        {
            return arr[Random.Range(0, arr.Length)];
        }

        public static List<T> GetRandomListWithoutDuplicate<T>(this List<T> list, int count)
        {
            if (list.Count <= 0) return null;
            if (list.Count < count) GULogger.Warning("ListCollectionExtension", $"WARNING: list{typeof(T)} co so phan tu:{list.Count} nho hon {count}");
            var index = new List<int>();
            for (var i = 0; i < list.Count; i++) index.Add(i);
            var n = Mathf.Min(list.Count, count);
            var newList = new List<T>();
            for (var i = 0; i < n; i++)
            {
                var ranIndex = Random.Range(0, index.Count);
                newList.Add(list[index[ranIndex]]);
                index.Remove(index[ranIndex]);
            }
            return newList;
        }

        public static List<T> GetRandomUniqueElements<T>(List<T> list, int numberOfElements)
        {
            var randomElements = new List<T>();
            var tempList = new List<T>();
            tempList.AddRange(list);
            for (int i = 0; i < numberOfElements; i++)
            {
                var index = Random.Range(0, tempList.Count);
                randomElements.Add(tempList[index]);
                tempList.RemoveAt(index);
            }
            return randomElements;
        }
    }
}