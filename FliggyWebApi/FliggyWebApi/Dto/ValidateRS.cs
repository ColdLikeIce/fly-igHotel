using System.Xml.Serialization;

namespace FliggyWebApi.Dto
{
    [XmlRoot("Result")]
    public class ValidateRS : FliggyResult
    {
        public string? CreateOrderValidateKey { get; set; }
        public string? InventoryPrice { get; set; }
        public string? CurrencyCode { get; set; }
    }
}