using System.Xml;
using System.Xml.Serialization;

namespace FliggyWebApi.Util
{
    public class XmlHelper
    {
        public static string SerializeToXml<T>(T obj)
        {
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(T));
            using (StringWriter textWriter = new StringWriter())
            {
                xmlSerializer.Serialize(textWriter, obj);
                return textWriter.ToString();
            }
        }

  
        public static string GetNodeValue(XmlElement root, string xpath)
        {
            var node =  root.SelectSingleNode(xpath);
            return node != null ? node.InnerText : null;
        }
    }
}