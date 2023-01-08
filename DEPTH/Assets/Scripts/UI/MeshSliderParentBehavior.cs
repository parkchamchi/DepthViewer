using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class MeshSliderParents {
	private static Dictionary<string, MeshSliderParentBehavior> _dict;

	static MeshSliderParents() {
		_dict = new Dictionary<string, MeshSliderParentBehavior>();
	}

	public static void Add(string paramname, MeshSliderParentBehavior behav) =>
		_dict.Add(paramname, behav);

	public static MeshSliderParentBehavior Get(string paramname) =>
		_dict[paramname];
}

public class MeshSliderParentBehavior : SliderParentBehavior {
	private IDepthMesh _dmesh;
	private string _paramname;

	void Start() {
		_dmesh = GameObject.Find("DepthPlane").GetComponent<MeshBehavior>();
		_paramname = LabelText.text;

		Slider.onValueChanged.AddListener(delegate {ChangeValueText();});
		Slider.onValueChanged.AddListener(delegate {OnValueChanged();});

		ChangeValueText();

		//Add itself to MeshSlidersParents
		MeshSliderParents.Add(_paramname, this);
	}

	private void OnValueChanged() =>
		_dmesh.SetParam(_paramname, Slider.value);
}