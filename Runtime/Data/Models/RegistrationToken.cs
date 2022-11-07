namespace Advant.Data.Models
{
    internal struct RegistrationToken
    {
        public RegistrationToken(string platform, 
						  string idfv, 
						  string idfa,
						  string abMode,
						  string gameVersion,
						  bool initializedBefore)
        {
            Platform = platform;
            DeviceId = idfv;
            IdForAdvertising = idfa;
			AbMode = abMode;
			GameVersion = gameVersion;
			InitializedBefore = initializedBefore;
        }

        public string ToJson()
        {
            return $"{{\"Platform\": \"{Platform}\", \"DeviceId\":\"{DeviceId}\", " + 
				$"\"IdForAdvertising\":\"{IdForAdvertising}\", \"AbMode\":\"{AbMode}\", " + 
				$"\"GameVersion\":\"{GameVersion}\", \"InitializedBefore\":{InitializedBefore.ToString().ToLower()}}}";
        }

        public string 	Platform { get; set; }
        public string 	DeviceId { get; set; }
        public string 	IdForAdvertising { get; set; }
		public string 	AbMode { get; set; }
		public string	GameVersion { get; set; }
		public bool 	InitializedBefore { get; set; }
    }
}

