using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

public class UpdateAnalyticsFromGit : MonoBehaviour
{
    private const string REPOSITORY_PATH = "https://github.com/rcStores/UnityAnalyticsClient.git";

    [MenuItem("Tools/Advant Analytics/Update SDK")]
    public static void UpdatePackage()
    {
        var addRequest = Client.Add(REPOSITORY_PATH);
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
}
