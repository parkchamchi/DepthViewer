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

	public Toggle OrtCudaToggle;

	private MainBehavior _mainBehav;

	void Start() {
		ScrollView.SetActive(false);

		_mainBehav = GameObject.Find("MainManager").GetComponent<MainBehavior>();
	}

	public void TogglePanel() {
		ScrollView.SetActive(!ScrollView.activeSelf);
	}

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

	public void LoadBuiltin() =>
		_mainBehav.LoadBuiltIn();
	
	public void LoadMidasV21() =>
		_mainBehav.LoadModel("./onnx/model-f6b98070.onnx", true);

	public void OnOrtCudaToggleValueChanged() =>
		_mainBehav.SetOnnxRuntimeParams(OrtCudaToggle.isOn, 0);
}
