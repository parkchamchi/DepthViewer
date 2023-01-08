using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ParamMinMaxBehavior : MonoBehaviour {
	public TMP_Text ParamnameText;

	public TMP_InputField MinInputField;
	public TMP_InputField MaxInputField;
	public TMP_Text StatusText;

	public void Set() {
		/* Get the target slider from the label */
		Slider targetSlider = MeshSliderParents.Get(ParamnameText.text).Slider;

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

		targetSlider.minValue = min;
		targetSlider.maxValue = max;

		StatusText.text = "O";
	}
}
