using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace GameUp.Core.Serializer
{
    public class XmlSerializer : ISerializer
    {
        public Encoding Encoding { get => encoding; set => encoding = value; }
        private Encoding encoding = Encoding.UTF8;

        public string Serialize<T>(T obj)
        {
            Stream stream = new MemoryStream();
            try
            {
                System.Xml.Serialization.XmlSerializer serializer = new System.Xml.Serialization.XmlSerializer(typeof(T));
                serializer.Serialize(stream, obj);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            var result = encoding.GetString(((MemoryStream)stream).ToArray());
            stream.Dispose();

            return result;
        }

        public T Deserialize<T>(string data)
        {
            Stream stream = new MemoryStream(encoding.GetBytes(data));
            T result = default(T);

            try
            {
                System.Xml.Serialization.XmlSerializer serializer = new System.Xml.Serialization.XmlSerializer(typeof(T));
                result = (T)serializer.Deserialize(stream);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            stream.Dispose();

            return result;
        }
    }
}
