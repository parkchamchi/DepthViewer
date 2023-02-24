using SFB;

using System.Collections;
using System.Runtime.InteropServices; //Dllimport
using UnityEngine.Networking; //UnityWebRequest

public class WebGLFileSelecter : FileSelecter {

	[DllImport("__Internal")]
	private static extern void UploadFile(string gameObjectName, string methodName, string filter, bool multiple);

	public void SelectFile(OnPathSelected callback) =>
		UploadFile("MainManager", "OnFileUpload", Exts.WebGLExts(FileTypes.Img), false);
}