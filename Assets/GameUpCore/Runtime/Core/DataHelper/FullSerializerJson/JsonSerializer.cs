using FullSerializer;
using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace GameUp.Core.Serializer
{
    public class JsonSerializer : ISerializer
    {
        public Encoding Encoding { get => encoding; set => encoding = value; }
        private Encoding encoding = Encoding.UTF8;

        public string Serialize<T>(T obj)
        {
            Stream stream = new MemoryStream();
#if !UNITY_WSA || !UNITY_WINRT
            try
            {
                StreamWriter writer = new StreamWriter(stream, encoding);
                fsSerializer serializer = new fsSerializer();
                fsData data = new fsData();
                serializer.TrySerialize(obj, out data);
                writer.Write(fsJsonPrinter.CompressedJson(data));
                writer.Dispose();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
#else
            StreamWriter writer = new StreamWriter(stream, encoding);
            writer.Write(JsonUtility.ToJson(obj));
            writer.Dispose();
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
                StreamReader reader = new StreamReader(stream, encoding);
                fsSerializer serializer = new fsSerializer();
                fsData fsdata = fsJsonParser.Parse(reader.ReadToEnd());
                serializer.TryDeserialize(fsdata, ref result);
                if (result == null)
                {
                    result = default(T);
                }
                reader.Dispose();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
#else
            StreamReader reader = new StreamReader(stream, encoding);
            result = JsonUtility.FromJson<T>(reader.ReadToEnd());
            reader.Dispose();
#endif
            stream.Dispose();

            return result;
        }
    }
}
