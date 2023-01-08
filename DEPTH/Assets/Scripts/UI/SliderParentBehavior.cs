using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SliderParentBehavior : MonoBehaviour {
	public Slider Slider;
	public TMP_Text LabelText;
	public TMP_Text ValueText;

	void Start() {
		ChangeValueText(); //init
	}

	public void ChangeValueText() {
		//_valueText.text = $"{_slider.value:f2}";
		ValueText.text = Slider.value.ToString("0.##");
	}
}