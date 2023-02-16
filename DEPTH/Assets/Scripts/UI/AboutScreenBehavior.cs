using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using TMPro;

public class AboutScreenBehavior : MonoBehaviour {

	public GameObject AboutScreen;
	public TMP_Text AboutText;
	public TextAsset AboutTextAsset;

	void Start() {
		AboutScreen.SetActive(false);
		AboutText.text = AboutTextAsset.text;
	}

	public void ToggleScreen() =>
		WindowManager.SetCurrentWindow(AboutScreen);
}
