namespace Advant.Data.Models
{
    internal struct Identifier
    {
        public Identifier(string platform, string idfv, string idfa)
        {
            Platform = platform;
            DeviceId = idfv;
            IdForAdvertising = idfa;
        }

        public string ToJson()
        {
            return $"{{\"Platform\": \"{Platform}\", \"DeviceId\":\"{DeviceId}\", \"IdForAdvertising\":\"{IdForAdvertising}\"}}";
        }

        public string Platform { get; set; }
        public string DeviceId { get; set; }
        public string IdForAdvertising { get; set; }
    }
}

