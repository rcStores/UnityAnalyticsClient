namespace Advant.Data.Models
{
    internal struct Identifier
    {
        public Identifier(string platform, string idfv, string idfa)
        {
			UserId = -1;
            Platform = platform;
            DeviceId = idfv;
            IdForAdvertising = idfa;
        }

        public string ToJson()
        {
            return $"{{\"UserId\": {UserId}, \"Platform\": \"{Platform}\", \"DeviceId\":\"{DeviceId}\", \"IdForAdvertising\":\"{IdForAdvertising}\"}}";
        }

		public long UserId { get; set; }
        public string Platform { get; set; }
        public string DeviceId { get; set; }
        public string IdForAdvertising { get; set; }
    }
}

