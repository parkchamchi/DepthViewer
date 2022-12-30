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

public static class StaticGOs {
	public static Texture2D PlaceholderTexture;
}

public class UIStaticClassSetter : MonoBehaviour {

	public TMP_Text FilepathResultText;
	public TMP_Text StatusText;
	public TMP_Text OutputSaveText;

	public GameObject DepthFilePanel;
	public TMP_Text DepthFileCompareText;
	public GameObject CallPythonObjectParent; //Only visible when the hashval is set
	public Toggle CallServerOnPauseToggle;

	public Texture2D PlaceholderTexture;

	void Start() {
		UITextSet.FilepathResultText = FilepathResultText;
		UITextSet.StatusText = StatusText;
		UITextSet.OutputSaveText = OutputSaveText;

		ImgVidDepthGOs.DepthFilePanel = DepthFilePanel;
		ImgVidDepthGOs.DepthFileCompareText = DepthFileCompareText;
		ImgVidDepthGOs.CallPythonObjectParent = CallPythonObjectParent;
		ImgVidDepthGOs.CallServerOnPauseToggle = CallServerOnPauseToggle;

		/*
		Why is this null on Builds?
		Interestingly it's not null on VR
		*/
		if (PlaceholderTexture != null)
			StaticGOs.PlaceholderTexture = PlaceholderTexture;
		else {
			Debug.LogWarning("PlaceholderTexture is not loaded! Something wrong with UIStaticClassSetter.cs");

			/* Manually loading the alternative placeholder */
			StaticGOs.PlaceholderTexture = Resources.Load<Texture2D>("placeholder_alt");
			if (StaticGOs.PlaceholderTexture != null)
				Debug.LogWarning("PlaceholderTexture loaded manually.");
			else
				Debug.LogWarning("Couldn't load the alternative placeholder manually.");
		}
	}
}
