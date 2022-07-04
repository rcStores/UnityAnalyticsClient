using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using System.Threading.Tasks;


public class UpdateAnalyticsFromGit : MonoBehaviour
{
    private const string REPOSITORY_PATH = "https://github.com/rcStores/UnityAnalyticsClient.git";

	static AddRequest s_AddRequest;
	
    [MenuItem("Tools/Advant Analytics/Update SDK")]
    public static void UpdatePackage()
    {
        s_AddRequest = Client.Add(REPOSITORY_PATH);
		EditorApplication.update += PackageRemovalProgress;
		//EditorApplication.LockReloadAssemblies();
    }
	
	static void PackageRemovalProgress() 
	{
		if (s_AddRequest?.IsCompleted) {
			switch (s_RemRequest.Status) {
				case StatusCode.Failure:
					throw new UnityException("Error while updating analytics SDK: " + addRequest.Error.message);
					break;
 
				case StatusCode.InProgress:
				break;
 
				case StatusCode.Success:
					Debug.Log(addRequest.Result.name + " was updated");
					EditorApplication.update -= PackageRemovalProgress;
					//EditorApplication.UnlockReloadAssemblies();
					s_AddRequest = null;
					break;
        }
    }
}
	//public static void Foo()
	//{}
}
