namespace FliggyWebApi.Config
{
    public class AppSetting
    {
        public string UserName { get; set; }
        public string Password { get; set; }
        public string SmileKey { get; set; }
        public string? SessionKey { get; set; }
        public int SyncMonth { get; set; } = 1;
        public PushSetting PushSetting { get; set; }

        public string GetHotelUrl { get; set; }
        public string GetPriceUrl { get; set; }
        public string GetPriceKey { get; set; }
        public string CheckUrl { get; set; }
    }

    public class PushSetting
    {
        public string pushurl { get; set; }
        public string redirectUri { get; set; }
        public string appkey { get; set; }
        public string secret { get; set; }
    }
}