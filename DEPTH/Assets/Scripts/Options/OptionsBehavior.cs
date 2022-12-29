using System.Collections;
using System.Collections.Generic;
using System.IO;

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class OptionsBehavior : MonoBehaviour {
	//TODO: codes below repeats itself, make a seperate script to attach to each options & make them emit events or something

	public GameObject ScrollView;

	public TMP_InputField SpeedMultInputField;
	public TMP_Text SpeedMultStatusText;

	public TMP_InputField OutputDirInputField;
	public TMP_Text OutputDirStatusText;

	public TMP_InputField PythonPathInputField;

	public TMP_InputField MeshXInputField;
	public TMP_Text MeshXStatusText;

	public TMP_InputField MeshYInputField;
	public TMP_Text MeshYStatusText;

	public TMP_InputField LightInputField;
	public TMP_Text LightStatusText;

	public GameObject ModelSettingsGO;

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

	public void SetPythonPath() {
		string pythonpath = PythonPathInputField.text;
		Utils.PythonPath = pythonpath;
	}

	public void SetMeshX() {
		float val;

		try {
			val = float.Parse(MeshXInputField.text);
		} catch (System.FormatException) {
			MeshXStatusText.text = "!";
			return;
		}

		_mainBehav.SetMeshX(val);
		MeshXStatusText.text = "O";
	}

	public void SetMeshY() {
		float val;

		try {
			val = float.Parse(MeshYInputField.text);
		} catch (System.FormatException) {
			MeshYStatusText.text = "!";
			return;
		}

		_mainBehav.SetMeshY(val);
		MeshYStatusText.text = "O";
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

	public void ShowModelSettings() =>
		ModelSettingsGO.SetActive(true);
}
