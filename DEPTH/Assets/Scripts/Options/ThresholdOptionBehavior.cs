using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ThresholdOptionBehavior : MonoBehaviour {

	public Slider ThresholdSlider;
	public Slider TargetValSlider;
	
	private MeshBehavior _meshBehav;

	void Start() {
		_meshBehav = GameObject.Find("DepthPlane").GetComponent<MeshBehavior>();
	}

	public void SetThreshold() {
		float val = ThresholdSlider.value;
		_meshBehav.Threshold = val;

		if (_meshBehav.TargetVal > val)
			TargetValSlider.value = val;
	}

	public void SetTargetVal() {
		float val = TargetValSlider.value;
		_meshBehav.TargetVal = val;
	}

	public void SetTargetValToNaN() {
		_meshBehav.TargetVal = System.Single.NaN;
	}
}
