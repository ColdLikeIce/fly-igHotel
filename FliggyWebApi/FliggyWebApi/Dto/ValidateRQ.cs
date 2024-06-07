using System.Xml.Serialization;
using Xiwan.Shared;

namespace FliggyWebApi.Dto
{
    [XmlRoot("ValidateRQ")]
    public class ValidateRQ
    {
        [XmlElement("AuthenticationToken")]
        public AuthenticationToken? AuthenticationToken { get; set; }

        public string? TaoBaoHotelId { get; set; }
        public string? HotelId { get; set; }
        public string? TaoBaoRoomTypeId { get; set; }
        public string? RoomTypeId { get; set; }
        public string? TaoBaoRatePlanId { get; set; }
        public string? RatePlanCode { get; set; }
        public string? TaoBaoGid { get; set; }
        public DateTime? CheckIn { get; set; }
        public DateTime? CheckOut { get; set; }
        public int? RoomNum { get; set; }
        public int? CustomerNumber { get; set; }

        [XmlElement("Occupancy")]
        public Occupancy? Occupancy { get; set; }

        public int? PaymentType { get; set; }
        public string? Extensions { get; set; }
        public decimal? TotalPrice { get; set; }

        [XmlArray("DailyInfos")]
        [XmlArrayItem("DailyInfo")]
        public List<DailyInfo>? DailyInfos { get; set; }

        public string? CurrencyCode { get; set; }
    }

    public class Occupancy
    {
        public int? AdultNumber { get; set; }
        public int? ChildrenNumber { get; set; }

        [XmlArray("ChildrenAge")]
        [XmlArrayItem("Age")]
        public List<int>? ChildrenAge { get; set; }
    }
}