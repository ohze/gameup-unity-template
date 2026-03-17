using System.Text;

namespace GameUp.Core.Serializer
{
    public interface ISerializer
    {
        Encoding Encoding { get; set; }
        string Serialize<T>(T obj);
        T Deserialize<T>(string data);
    }
}
