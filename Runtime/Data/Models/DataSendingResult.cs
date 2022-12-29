namespace Advant.Data.Models
{
	
public class DataSendingResult
{
	public bool IsSuccess { get; set; }
	public long StatusCode { get; set; }
	public string RequestError { get; set; }
	public string ExceptionMessage { get; set; }
	public string DownloadHandler { get; set; }
	public string Age { get; set; }
}

}