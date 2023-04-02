using System.Collections;
using System.Collections.Generic;
using System.IO;

using UnityEngine;
using UnityEngine.UI;
using TMPro;

using IngameDebugConsole;

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

	public TMP_Dropdown OnnxDropdown;
	public Toggle UseOrtToggle;
	public Toggle UseGpuForOrtToggle;
	public TMP_Dropdown OrtGpuProvidersDropdown;
	public TMP_Text CurrentModelText;

	public TMP_Dropdown ParamDropdown;
	public TMP_InputField ParamMinInputField;
	public TMP_InputField ParamMaxInputField;
	public TMP_Text ParamMinMaxStatusText;
	private Slider _targetSlider {get {return MeshSliderParents.Get(ParamDropdown.captionText.text).Slider;}}

	public TMP_Dropdown DepthMapTypeDropdown;
	public TMP_Dropdown DepthMapFormatDropdown;
	public Toggle DepthMapResizeToggle;
	private bool _exrExportUse32Bit = true; //Whether the exr export will use 32bit fp (instead of 16bit)

	public GameObject DesktopRenderGO;

	public Toggle Dof6Toggle;
	public Toggle MouseMoveToggle;

	public Toggle SkyboxToggle;
	public Slider SkyboxTintSlider;
	public Slider SkyboxExposureSlider;
	public Toggle SkyboxBlurToggle;
	public Slider SkyboxBlurIterSlider;

	[SerializeField] private float _skyboxTint = 0.5f;
	[SerializeField] private float _skyboxExposure = 0.5f;
	[SerializeField] private bool _skyboxBlur = false;
	[SerializeField, Range(0, 8)] private int _skyboxBlurIter = 4;
	private RenderTexture _skyboxRt = null;

	private MainBehavior _mainBehav;

	private const string _onnxdir = "./onnx";

	void Start() {
		ScrollView.SetActive(false);

		_mainBehav = GameObject.Find("MainManager").GetComponent<MainBehavior>();

		LoadOnnxModelList();
		//OnParamDropdownValueChanged();
		OutputDirInputField.text = DepthFileUtils.SaveDir;

		DebugLogConsole.AddCommandInstance("set_exr32bit", "Set whether exr files will be exported using 32bit fp not 16bit", "SetExrExportUse32Bit", this);
	}

	public void TogglePanel() =>
		WindowManager.SetCurrentWindow(ScrollView);

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

	public void OnOrtGpuToggleOrDropdownValueChanged() {
		/*
		If "Use GPU for OnnxRuntime" toggle is on, pass the selected GPU EP. Else, pass null
		Should be called when the toggle or the dropdown alters
		*/

		string target = null;

		if (UseGpuForOrtToggle.isOn)
			target = OrtGpuProvidersDropdown.captionText.text;

		_mainBehav.SetOrtGpuProvider(target);
	}

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

	public void OnDepthMapFormatDropdownValueChanged() {
		//Disable the resize operation for pgm files

		string format = DepthMapFormatDropdown.captionText.text;

		if (format == ".pgm") {
			DepthMapResizeToggle.isOn = false;
			DepthMapResizeToggle.interactable = false;
		}
		else {
			DepthMapResizeToggle.interactable = true;
		}
	}

	public void ExportDepthMap() {
		string typestr = DepthMapTypeDropdown.captionText.text;
		string format = DepthMapFormatDropdown.captionText.text;
		bool resize = DepthMapResizeToggle.isOn;

		typestr = typestr.ToLower();
		DepthMapType type;
		switch (typestr) {
		case "inverse":
			type = DepthMapType.Inverse;
			break;
		case "linear":
			type = DepthMapType.Linear;
			break;
		default:
			Debug.Log($"ExportDepthMap(): Got unknown typestr: {typestr}");
			return;
		}

		//int x, y;
		Depth depth = _mainBehav.GetCurrentDepth(type);

		if (depth == null) {
			Debug.LogWarning("ExportDepthMap(): depths == null");
			return;
		}

		byte[] data = null;

		switch (format) {
		case ".pgm":
			data = DepthFileUtils.WritePGM(depth);
			break;

		case ".png":
		case ".exr":
			Texture2D tex = Utils.DepthToTex(depth);

			/* makes artifacts
			if (resize) {
				int w, h;
				_mainBehav.GetCurrentTextureSize(out w, out h);
				
				if (w != 0 && h != 0) {
					Texture2D newtex = Utils.ResizeTexture(tex, w, h);
					Destroy(tex);
					tex = newtex;
				}
			}
			*/

			if (format == ".png")
				data = tex.EncodeToPNG();
			else if (format == ".exr") {
				Texture2D.EXRFlags exrflag = Texture2D.EXRFlags.None;

				if (_exrExportUse32Bit) {
					Debug.Log("EXR: using 32bit fp");
					exrflag = Texture2D.EXRFlags.OutputAsFloat;
				}
				else {
					Debug.Log("EXR: using 16bit fp");
					exrflag = Texture2D.EXRFlags.None;
				}

				data = tex.EncodeToEXR(exrflag);
			}

			Destroy(tex);
			break;

		default:
			Debug.LogError($"ExportDepthMap(): Got unsupported format {format}");
			return;
		}

		string exportdir = $"{DepthFileUtils.SaveDir}/exports";
		Utils.CreateDirectory(exportdir);

		string filename = $"{exportdir}/{Utils.GetTimestamp()}{format}";
		File.WriteAllBytes(filename, data);

		Debug.Log($"Exported: {filename}");
	}

	public void SetExrExportUse32Bit(bool val) {
		Debug.Log($"_exrExportUse32Bit: {val}");
		_exrExportUse32Bit = val;
	}

	public void ToggleDesktopRender() =>
		DesktopRenderGO.SetActive(!DesktopRenderGO.activeSelf);

	public void OnDof6ToggleValueChanged() =>
		_mainBehav.SetDof((Dof6Toggle.isOn) ? 6 : 3);

	public void OnMouseMoveToggleValueChanged() =>
		_mainBehav.SetMoveMeshByMouse(MouseMoveToggle.isOn);

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
		Material skyboxMat = new Material(Shader.Find("Skybox/6 Sided"));

		skyboxMat.SetVector("_Tint", new Vector4(_skyboxTint, _skyboxTint, _skyboxTint, _skyboxTint));
		skyboxMat.SetFloat("_Exposure", _skyboxExposure);

		foreach (string target in new string[] {"_FrontTex", "_LeftTex", "_RightTex", "_UpTex", "_DownTex", "_BackTex"}) //BackTex can be omitted, but I think it's funnier this way
			skyboxMat.SetTexture(target, _skyboxRt);

		RenderSettings.skybox = skyboxMat;
	}

	public void OnSkyboxToggleValueChanged() =>
		_mainBehav.SetMeshTextureSetCallback(SkyboxToggle.isOn, SkyboxCallback);

	public void OnSkyboxTintSliderValueChanged() =>
		_skyboxTint = SkyboxTintSlider.value;

	public void OnSkyboxExposureSliderValueChanged() =>
		_skyboxExposure = SkyboxExposureSlider.value;
	
	public void OnSkyboxBlurToggleValueChanged() =>
		_skyboxBlur = SkyboxBlurToggle.isOn;

	public void OnSkyboxBlurIterSliderValueChanged() =>
		_skyboxBlurIter = (int) SkyboxBlurIterSlider.value;
}
