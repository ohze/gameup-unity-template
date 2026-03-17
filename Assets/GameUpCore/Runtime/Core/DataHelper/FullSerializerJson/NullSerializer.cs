using System.Text;

namespace GameUp.Core.Serializer
{
    public class NullSerializer : ISerializer
    {
        public Encoding Encoding { get => encoding; set => encoding = value; }
        private Encoding encoding = Encoding.UTF8;

        public string Serialize<T>(T obj)
        {
            return obj.ToString();
        }

        public T Deserialize<T>(string data)
        {
            return default(T);
        }
    }
}
