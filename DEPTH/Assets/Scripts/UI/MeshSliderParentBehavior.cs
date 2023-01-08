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

		//Add itself to mesh's ParamChanged event
		_dmesh.ParamChanged += OnParamChanged;
	}

	private void OnValueChanged() =>
		_dmesh.SetParam(_paramname, Slider.value);

	private void OnParamChanged(string paramname, float value) {
		/* This method is invoked when the mesh's property is changed, either by this slider or by external actor */

		if (paramname != _paramname)
			return;

		if (value == Slider.value)
			return;
		
		//Value changed by other object
		Slider.value = value;
	}
}