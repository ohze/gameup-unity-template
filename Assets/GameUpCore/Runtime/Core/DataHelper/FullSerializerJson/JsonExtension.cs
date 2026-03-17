using UnityEngine;

namespace GameUp.Core.Serializer
{
    public static class JsonExtension
    {
        public static void Dump(this object obj)
        {
            Debug.Log(obj.Serialize());
        }

        public static string Serialize(this object obj)
        {
            return new JsonSerializer().Serialize(obj);
        }

        public static T Deserialize<T>(this string data)
        {
            return new JsonSerializer().Deserialize<T>(data);
        }
    }
}
