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

	public TMP_InputField LightInputField;
	public TMP_Text LightStatusText;

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

	////////////////////////////////////////////////////////////////////////////////////////
	// SKYBOX
	////////////////////////////////////////////////////////////////////////////////////////

	public Toggle SkyboxToggle;
	public Toggle SkyboxPanoramicToggle;
	public Slider SkyboxTintSlider;
	public Slider SkyboxExposureSlider;
	public Toggle SkyboxBlurToggle;
	public Slider SkyboxBlurIterSlider;

	[SerializeField] private float _skyboxTint = 0.5f;
	[SerializeField] private float _skyboxExposure = 0.5f;
	[SerializeField] private bool _skyboxPanoramic = false;
	[SerializeField] private bool _skyboxBlur = false;
	[SerializeField, Range(0, 8)] private int _skyboxBlurIter = 4;
	private RenderTexture _skyboxRt = null;

	private void SkyboxCallback(Texture tex) {
		//Has a room for optimization

		//Check if current `skyboxRt` is compatible
		int w = tex.width;
		int h = tex.height;
		if (_skyboxRt == null || _skyboxRt.width != w || _skyboxRt.height != h) {
			_skyboxRt?.Release();
			_skyboxRt = new RenderTexture(w, h, 16);
		}

		//tex to _skyboxRt
		Graphics.Blit(tex, _skyboxRt);

		//Blur if it should
		if (_skyboxBlur)
			GaussianFilter.Filter(_skyboxRt, _skyboxRt, iteration: _skyboxBlurIter);

		//The shader has to be under "Always Included Shaders" list in ProjectSettings/Graphics (see MeshShaders)
		string shadername = (_skyboxPanoramic) ? "Skybox/Panoramic" : "Skybox/6 Sided";
		Material skyboxMat = new Material(Shader.Find(shadername));

		skyboxMat.SetVector("_Tint", new Vector4(_skyboxTint, _skyboxTint, _skyboxTint, _skyboxTint));
		skyboxMat.SetFloat("_Exposure", _skyboxExposure);
		if (_skyboxPanoramic) {
			skyboxMat.SetFloat("_ImageType", 1); //180 proj
			skyboxMat.SetFloat("_MirrorOnBack", 1);
		}

		string[] targets = (_skyboxPanoramic) ? 
			new string[] {"_MainTex"}
			: new string[] {"_FrontTex", "_LeftTex", "_RightTex", "_UpTex", "_DownTex", "_BackTex"};
		foreach (string target in targets) //BackTex can be omitted, but I think it's funnier this way
			skyboxMat.SetTexture(target, _skyboxRt);

		RenderSettings.skybox = skyboxMat;
	}

	public void OnSkyboxToggleValueChanged() =>
		_mainBehav.SetMeshTextureSetCallback(SkyboxToggle.isOn, SkyboxCallback);

	public void OnSkyboxPanoramicToggleValueChanged() =>
		_skyboxPanoramic = SkyboxPanoramicToggle.isOn;

	public void OnSkyboxTintSliderValueChanged() =>
		_skyboxTint = SkyboxTintSlider.value;

	public void OnSkyboxExposureSliderValueChanged() =>
		_skyboxExposure = SkyboxExposureSlider.value;

	public void OnSkyboxBlurToggleValueChanged() =>
		_skyboxBlur = SkyboxBlurToggle.isOn;

	public void OnSkyboxBlurIterSliderValueChanged() =>
		_skyboxBlurIter = (int) SkyboxBlurIterSlider.value;

	////////////////////////////////////////////////////////////////////////////////////////
	// SKYBOX END
	////////////////////////////////////////////////////////////////////////////////////////
}
