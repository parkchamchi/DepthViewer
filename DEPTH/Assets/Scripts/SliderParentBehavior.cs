using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SliderParentBehavior : MonoBehaviour {
	public Slider _slider;
	public TMP_Text _labelText;
	public TMP_Text _valueText;

	void Start() {
		ChangeValueText(); //init
	}

	public void ChangeValueText() {
		/*if (_slider.value == System.Single.Epsilon)
			_valueText.text = ">0";
		else*/
			_valueText.text = _slider.value.ToString();
	}
}
