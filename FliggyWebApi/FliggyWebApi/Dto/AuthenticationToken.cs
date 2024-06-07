using System.Xml.Serialization;

namespace FliggyWebApi.Dto
{
    [XmlRoot("ValidateRQ")]
    public class ValidateRQAuth
    {
        [XmlElement("AuthenticationToken")]
        public AuthenticationToken? AuthenticationToken { get; set; }
    }

    public class AuthenticationToken
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? CreateToken { get; set; }
    }
}