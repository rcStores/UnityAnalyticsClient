namespace Advant.Data.Models
{
	
public class DataSendingResult
{
	public bool IsSuccess { get; set; }
	public int StatusCode { get; set; }
	public string RequestError { get; set; }
	public string ExceptionMessage { get; set; }
	public string DownloadHandler { get; set; }
}

}