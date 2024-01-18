using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using TMPro;

public class WorldTextPlaneBehavior : MonoBehaviour {
	public TMP_Text WorldText;

	void Start() {
		WorldText.text = "......";
	}

	void Update() {
		WorldText.text = getText();
	}

	private string getText() {
		string statusText = UITextSet.StatusText.text;

		return statusText;
	}
}
