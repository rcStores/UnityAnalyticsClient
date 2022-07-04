using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using System.Threading.Tasks;


public class UpdateAnalyticsFromGit : MonoBehaviour
{
    private const string REPOSITORY_PATH = "https://github.com/rcStores/UnityAnalyticsClient.git";

	static AddRequest _addRequest;
	
    [MenuItem("Tools/Advant Analytics/Update SDK")]
    public static void UpdatePackage()
    {
        _addRequest = Client.Add(REPOSITORY_PATH);
		EditorApplication.update += PackageRemovalProgress;
		EditorApplication.LockReloadAssemblies();
    }
	
	private static void PackageRemovalProgress() 
	{
		if (_addRequest.IsCompleted) 
		{
			switch (_addRequest.Status) 
			{
				case StatusCode.Failure:
					throw new UnityException("Error while updating analytics SDK: " + _addRequest.Error.message);
					break;
 
				case StatusCode.InProgress:
					break;
 
				case StatusCode.Success:
					Debug.Log(_addRequest.Result.name + " was updated");
					EditorApplication.update -= PackageRemovalProgress;
					EditorApplication.UnlockReloadAssemblies();
					_addRequest = null;
					break;
			}        
		}
    }
}

