namespace FliggyWebApi.Dto
{
    public class TaoBaoToken
    {
        public string? refresh_token { get; set; }
        public string? access_token { get; set; }
        public long? refresh_token_valid_time { get; set; }
    }
}