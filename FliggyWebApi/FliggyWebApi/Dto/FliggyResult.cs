using System.Xml.Serialization;

namespace FliggyWebApi.Dto
{
    [XmlRoot("Result")]
    public class FliggyResult
    {
        public string? Message { get; set; }
        public int? ResultCode { get; set; }
    }
}