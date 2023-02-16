using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;

using UnityEngine;
using UnityEngine.UI;

using IngameDebugConsole;

public static class MeshSliderParents {
	private static Dictionary<string, MeshSliderParentBehavior> _dict;

	private static string MinMaxPath {get {return $"{DepthFileUtils.SaveDir}/minmax.txt";}}

	static MeshSliderParents() {
		_dict = new Dictionary<string, MeshSliderParentBehavior>();
	}

	public static void Add(string paramname, MeshSliderParentBehavior behav) =>
		_dict.Add(paramname, behav);

	public static MeshSliderParentBehavior Get(string paramname) =>
		_dict[paramname];

	[ConsoleMethod("minmax_export", "Export current min/max values for the mesh sliders")]
	public static void ExportMinMax() {
		/*
		param1 min max
		param2 min max
		...
		*/

		StringBuilder output = new StringBuilder();

		foreach (var item in _dict) {
			string paramname = item.Key;
			
			Slider targetSlider = item.Value.Slider;
			float minValue = targetSlider.minValue;
			float maxValue = targetSlider.maxValue;

			output.Append($"{paramname} {minValue} {maxValue}\n");
		}

		File.WriteAllText(MinMaxPath, output.ToString());
	}

	[ConsoleMethod("minmax_import", "Import the min/max values for the mesh sliders")]
	public static void ImportMinMax() {
		if (!File.Exists(MinMaxPath)) return;
		string input = File.ReadAllText(MinMaxPath);

		ImportMinMax(input);
	}

	public static void ImportMinMax(string input) {
		foreach (string line in input.Split('\n')) {
			string[] tokens = line.Split(' ');
			if (tokens.Length < 3)
				continue;

			string paramname = tokens[0].Trim();
			if (paramname == "Threshold" || paramname == "TargetVal") continue; //ignore

			float minValue, maxValue;
			try {
				minValue = float.Parse(tokens[1]);
				maxValue = float.Parse(tokens[2]);
			}
			catch (System.FormatException exc) {
				Debug.LogWarning($"ImportMinMax(): Failed to parse: `{line}`, {exc}");
				continue;
			}

			if (Utils.IsNaNInf(minValue) || Utils.IsNaNInf(maxValue)) {	
				Debug.LogWarning($"ImportMinMax(): param {paramname} has ({minValue}, {maxValue}) as value");
				continue;
			}

			Slider target;

			if (!_dict.ContainsKey(paramname)) {
				//This can happen when this method is called before the _dict is prepared (i.e. at startup)
				//This only happens on the build not the editor. Why?

				//Try to find it manually
				target = GameObject.Find($"{paramname}SliderParent")?.GetComponent<MeshSliderParentBehavior>()?.Slider;

				if (target == null) {
					Debug.LogWarning($"Couldn't find the slider: {paramname}");
					continue;
				}
			}
			else {
				target = _dict[paramname].Slider;
			}

			target.minValue = minValue;
			target.maxValue = maxValue;
		}
	}

	[ConsoleMethod("minmax_reset", "Reset the min/max values for the mesh sliders")]
	public static void ResetMinMax() {
		string input = @"
			Alpha 0.01 2
			Beta 0.25 0.75
			ProjRatio 0 1
			CamDistL 0 2
			ScaleR 0.5 1.5
			DepthMultRL -1 1
			
			MeshHor -0.5 0.5
			MeshVer -0.5 0.5
		";

		ImportMinMax(input);
	}
}

public class MeshSliderParentBehavior : SliderParentBehavior {
	private IDepthMesh _dmesh;
	private string _paramname;

	private ColorBlock _normalColorBlock;
	private ColorBlock _outOfRangeColorBlock; //Set of colors for sliders whose value if out-of-range.

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

		_normalColorBlock = ColorBlock.defaultColorBlock;
		//Replace the color with the disabled one.
		_outOfRangeColorBlock = ColorBlock.defaultColorBlock;
		_outOfRangeColorBlock.normalColor = _outOfRangeColorBlock.pressedColor = _outOfRangeColorBlock.highlightedColor
			= _outOfRangeColorBlock.disabledColor;
	}

	private void OnValueChanged() {
		_dmesh.SetParam(_paramname, Slider.value);
		Slider.colors = _normalColorBlock;
	}

	private void OnParamChanged(string paramname, float value) {
		/* This method is invoked when the mesh's property is changed, either by this slider or by external actor */

		if (paramname != _paramname)
			return;

		if (value == Slider.value) {
			//The code below is for when the formal value was out-of-range
			ValueText.text = value.ToString("0.##");
			Slider.colors = _normalColorBlock;
			return;
		}
		
		//Value changed by other object
		//Check if it is in the range of the slider
		if (Slider.minValue <= value && value <= Slider.maxValue)
			Slider.value = value;
		else {
			//otherwise just change the label
			ValueText.text = value.ToString("0.##") + "*";
			Slider.colors = _outOfRangeColorBlock; //Change the color
		}
	}
}