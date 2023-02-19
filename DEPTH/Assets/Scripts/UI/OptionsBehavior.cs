using System.Collections;
using System.Collections.Generic;
using System.IO;

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class OptionsBehavior : MonoBehaviour {
	//TODO: remove wrappers in _mainBehav if it's not needed and generalize inputs

	public GameObject ScrollView;

	public TMP_InputField SpeedMultInputField;
	public TMP_Text SpeedMultStatusText;

	public TMP_InputField OutputDirInputField;
	public TMP_Text OutputDirStatusText;

	public TMP_InputField LightInputField;
	public TMP_Text LightStatusText;

	public Slider PCSizeSlider;

	public Toggle UseOrtToggle;
	public Toggle OrtCudaToggle;
	public TMP_Dropdown OnnxDropdown;
	public TMP_Text CurrentModelText;

	public TMP_Dropdown ParamDropdown;
	public TMP_InputField ParamMinInputField;
	public TMP_InputField ParamMaxInputField;
	public TMP_Text ParamMinMaxStatusText;
	private Slider _targetSlider {get {return MeshSliderParents.Get(ParamDropdown.captionText.text).Slider;}}

	private MainBehavior _mainBehav;

	private const string _onnxdir = "./onnx";

	void Start() {
		ScrollView.SetActive(false);

		_mainBehav = GameObject.Find("MainManager").GetComponent<MainBehavior>();

		LoadOnnxModelList();
		OnParamDropdownValueChanged(); //init
	}

	public void TogglePanel() =>
		WindowManager.SetCurrentWindow(ScrollView);

	public void SetVideoSpeed() {
		float mult;

		try {
			mult = float.Parse(SpeedMultInputField.text);
		} catch (System.FormatException) {
			SpeedMultStatusText.text = "!";
			return;
		}

		_mainBehav.SetVideoSpeed(mult);
		SpeedMultStatusText.text = "O";
	}

	public void SetOutputDir() {
		string outputdir = OutputDirInputField.text;

		//remove trailing '/'
		if (outputdir.EndsWith('/') || outputdir.EndsWith('\\'))
			outputdir = outputdir.Substring(0, outputdir.Length-1);

		//Check if it a directory
		if (!Directory.Exists(outputdir)) {
			OutputDirStatusText.text = "!";
			return;
		}

		DepthFileUtils.SaveDir = outputdir;
		OutputDirStatusText.text = "O";
	}

	public void SetLightIntesity() {
		float val;

		try {
			val = float.Parse(LightInputField.text);
		} catch (System.FormatException) {
			LightStatusText.text = "!";
			return;
		}

		_mainBehav.SetLightIntensity(val);
		LightStatusText.text = "O";
	}

	public void ResetMinMax() =>
		MeshSliderParents.ResetMinMax();

	public void OnShaderButtonClicked(string shadername) {
		//Set the PC Size Slider's value to the default value.
		//This also activates when the standard shader is selected, where it has no effect...
		PCSizeSlider.value = MeshShaders.DefaultPointCloudSize;

		_mainBehav.SetMeshShader(shadername);
	}

	public void OnPCSizeSliderValueChanged() =>
		_mainBehav.SetPointCloudSize(PCSizeSlider.value);

	public void OpenRepoPage() =>
		Application.OpenURL("https://github.com/parkchamchi/DepthViewer#models");

	public void LoadBuiltin() {
		_mainBehav.LoadBuiltIn();

		CurrentModelText.text = "Built-in";
	}

	public void LoadModel() {
		string path = OnnxDropdown.captionText.text;
		path = Path.Join(_onnxdir, path+".onnx");

		bool useOrt = UseOrtToggle.isOn;

		_mainBehav.LoadModel(path, useOrt);

		CurrentModelText.text = _mainBehav.GetCurrentModelType();
	}

	public void OnOrtCudaToggleValueChanged() =>
		_mainBehav.SetOnnxRuntimeParams(OrtCudaToggle.isOn, 0);

	public void LoadOnnxModelList() {
		string[] onnxfiles = Directory.GetFiles(_onnxdir, "*.onnx");
		List<string> onnxBasenames = new List<string>();
		foreach (string onnxfile in onnxfiles)
			onnxBasenames.Add(Path.GetFileNameWithoutExtension(onnxfile));

		OnnxDropdown.ClearOptions();
		OnnxDropdown.AddOptions(onnxBasenames);
	}

	public void OnParamDropdownValueChanged() {
		ParamMinInputField.text = _targetSlider.minValue.ToString();
		ParamMaxInputField.text = _targetSlider.maxValue.ToString();

		ParamMinMaxStatusText.text = "";
	}

	public void OnParamMinMaxSet() {
		float min;
		float max;

		try {
			min = float.Parse(ParamMinInputField.text);
			max = float.Parse(ParamMaxInputField.text);
		}
		catch (System.FormatException) {
			ParamMinMaxStatusText.text = "!";
			return;
		}

		if (Utils.IsNaNInf(min) || Utils.IsNaNInf(max)) {
			Debug.LogWarning($"OnParamMinMaxSet(): Got value ({min}, {max})");
			ParamMinMaxStatusText.text = "?";
			return;
		}

		_targetSlider.minValue = min;
		_targetSlider.maxValue = max;

		ParamMinMaxStatusText.text = "O";
	}
}
