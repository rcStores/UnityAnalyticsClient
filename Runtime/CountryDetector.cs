internal class CountryDetector
{
	private string _country;
	
	private readonly Backend _backend;
	
	public CountryDetector(Backend backend)
	{
		_backend = backend;
	}
	
	public async UniTask<string> GetCountryAsync(int timeout)
	{
		_country = _backend.GetCountry(timeout);
	}
}