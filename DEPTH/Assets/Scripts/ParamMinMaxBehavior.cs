using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ParamMinMaxBehavior : MonoBehaviour {
	public Slider TargetSlider;

	public TMP_InputField MinInputField;
	public TMP_InputField MaxInputField;
	public TMP_Text StatusText;

	public void Set() {
		float min;
		float max;

		try {
			min = float.Parse(MinInputField.text);
			max = float.Parse(MaxInputField.text);
		}
		catch (System.FormatException) {
			StatusText.text = "!";
			return;
		}

		TargetSlider.minValue = min;
		TargetSlider.maxValue = max;

		StatusText.text = "O";
	}
}
