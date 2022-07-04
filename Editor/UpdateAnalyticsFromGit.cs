using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using System.Threading.Tasks;


public class UpdateAnalyticsFromGit : MonoBehaviour
{
    private const string REPOSITORY_PATH = "https://github.com/rcStores/UnityAnalyticsClient.git";

    [MenuItem("Tools/Advant Analytics/Update SDK")]
    public static void UpdatePackage()
    {
        //Client.Remove(REPOSITORY_PATH);
        var addRequest = Client.Add(REPOSITORY_PATH);
        while (addRequest.Status == StatusCode.InProgress)
        {
            Task.Yield();
        }

        if (addRequest.Status == StatusCode.Failure)
        {
            throw new UnityException("Error while updating analytics SDK: " + addRequest.Error.message);
        }
        else if (addRequest.Status == StatusCode.Success)
        {
            Debug.Log(addRequest.Result.name + " was updated");
        }
    }
	
	public static void Foo()
	{}
    public static void Bar()
    { }
}
