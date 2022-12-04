using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;
using TMPro;

/* There has to be a better way for this */

public static class UITextSet {
	public static TMP_Text FilepathResultText;
	public static TMP_Text StatusText;
	public static TMP_Text OutputSaveText;
}

public static class ImgVidDepthGOs {
	public static GameObject DepthFilePanel;
	public static TMP_Text DepthFileCompareText;
	public static GameObject CallPythonObjectParent; //Only visible when the hashval is set
	public static Toggle CallServerOnPauseToggle;
}

public class UIStaticClassSetter : MonoBehaviour {

	public TMP_Text FilepathResultText;
	public TMP_Text StatusText;
	public TMP_Text OutputSaveText;

	public GameObject DepthFilePanel;
	public TMP_Text DepthFileCompareText;
	public GameObject CallPythonObjectParent; //Only visible when the hashval is set
	public Toggle CallServerOnPauseToggle;

	void Start() {
		UITextSet.FilepathResultText = FilepathResultText;
		UITextSet.StatusText = StatusText;
		UITextSet.OutputSaveText = OutputSaveText;

		ImgVidDepthGOs.DepthFilePanel = DepthFilePanel;
		ImgVidDepthGOs.DepthFileCompareText = DepthFileCompareText;
		ImgVidDepthGOs.CallPythonObjectParent = CallPythonObjectParent;
		ImgVidDepthGOs.CallServerOnPauseToggle = CallServerOnPauseToggle;
	}
}
