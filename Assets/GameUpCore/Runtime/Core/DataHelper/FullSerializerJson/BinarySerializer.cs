using System;
using System.IO;
using System.Text;
using UnityEngine;

#if !UNITY_WSA || !UNITY_WINRT
using System.Runtime.Serialization.Formatters.Binary;
#endif

namespace GameUp.Core.Serializer
{
    public class BinarySerializer : ISerializer
    {
        public Encoding Encoding { get => encoding; set => encoding = value; }
        private Encoding encoding = Encoding.UTF8;

        public string Serialize<T>(T obj)
        {
            Stream stream = new MemoryStream();
#if !UNITY_WSA || !UNITY_WINRT
            try
            {
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(stream, obj);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
#else
            Debug.LogError("Isn't supported");
#endif
            var result = encoding.GetString(((MemoryStream)stream).ToArray());
            stream.Dispose();

            return result;
        }

        public T Deserialize<T>(string data)
        {
            Stream stream = new MemoryStream(encoding.GetBytes(data));
            T result = default(T);
#if !UNITY_WSA || !UNITY_WINRT
            try
            {
                BinaryFormatter formatter = new BinaryFormatter();
                result = (T)formatter.Deserialize(stream);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
#else
            Debug.LogError("Isn't supported");
#endif
            stream.Dispose();

            return result;
        }
    }
}
