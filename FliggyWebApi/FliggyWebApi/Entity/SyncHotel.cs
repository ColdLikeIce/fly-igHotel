using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace FliggyWebApi.Entity
{
    [Table("sync_XHotel")]
    public class SyncHotel
    {
        [Key]
        public string HotelId { get; set; }

        public string? Name { get; set; }
        public string? CityCode { get; set; }
        public string? CityName { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? StarRate { get; set; }
        public string? Category { get; set; }
        public string? BaiduLat { get; set; }
        public string? BaiduLon { get; set; }
        public DateTime? ctime { get; set; }
        public DateTime? utime { get; set; }
        public DateTime? roomtypeTime { get; set; }
        public DateTime? rateplanTime { get; set; }
        public DateTime? stockTime { get; set; }
        public int isdeleted { get; set; }
        public int status { get; set; }
        public long? Hid { get; set; }
    }
}