using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using Microsoft.ML.OnnxRuntime;
//using Microsoft.ML.OnnxRuntime.Gpu;

/*
These nuget packages has to be installed:
	Microsoft.ML.OnnxRuntime
	Microsoft.ML.OnnxRuntime.Managed
.Gpu doen't work, probably because of the lack of .Managed

These dll files has to be in DEPTH/Assets/Plugins/OnnxRuntimeDlls/win-x64/native
	onnxruntime.dll
	onnxruntime_providers_shared.dll
Which are in the nupkg file
*/

public class OnnxRuntimeDepthModelBehavior : MonoBehaviour {
	private string _largeModelPath = "D:/tmp/dpt_large-midas.onnx";

	public DepthModel GetLargeModel() {
		return new OnnxRuntimeDepthModel(_largeModelPath, 400);
	}
}

public class OnnxRuntimeDepthModel : DepthModel {
	private int _modelTypeVal;
	public int ModelTypeVal {get {return _modelTypeVal;}}

	private InferenceSession _infsession;
	private int _width, _height;

	private RenderTexture _rt;

	public OnnxRuntimeDepthModel(string onnxpath, int modelTypeVal) {
		_modelTypeVal = modelTypeVal;

		_infsession = new InferenceSession(onnxpath);
		foreach (KeyValuePair<string, NodeMetadata> item in _infsession.InputMetadata) {
			_width = item.Value.Dimensions[2];
			_height = item.Value.Dimensions[3];
			Debug.Log(_width);
			Debug.Log(_height);
		} //only 1

		_rt = new RenderTexture(_width, _height, 16);
	}

	public float[] Run(Texture inputTexture, out int x, out int y) {
		Graphics.Blit(inputTexture, _rt);

		Texture2D tex = new Texture2D(_width, _height);
		RenderTexture.active = _rt;
		tex.ReadPixels(new Rect(0, 0, _width, _height), 0, 0);
		RenderTexture.active = null;

		var rawdata = tex.GetRawTextureData();
		Debug.Log($"rawdata.Length: {rawdata.Length}"); //why is this lesser than 384*384*4 ?
		for (int i = 0; i < _width * _height; i++) {
			//...
		}
		
		UnityEngine.GameObject.Destroy(tex);

		x = y = 0;
		return null;
	}

	public float[] RunAndClone(Texture inputTexture, out int x, out int y) {
		return (float[]) Run(inputTexture, out x, out y).Clone();
	}

	public void Dispose() {
		_infsession.Dispose();

		_rt?.Release();
		_rt = null;
	}

	private void PrintMetadata() {
		/* For debug */

		Debug.Log("************************INPUTMETADATA");
		foreach (KeyValuePair<string, NodeMetadata> item in _infsession.InputMetadata) { //only 1
			Debug.Log("+++++" + item.Key + ": ");
			var v = item.Value;
			Debug.Log($"Dimensions:{v.Dimensions}"); //[1, 3, 384, 384]
			Debug.Log($"Dimensions.Length:{v.Dimensions.Length}");
			foreach (var e in v.Dimensions) Debug.Log(e);

			Debug.Log($"ElementType:{v.ElementType}");
			Debug.Log($"IsTensor:{v.IsTensor}");
			Debug.Log($"OnnxValueType:{v.OnnxValueType}");

			Debug.Log($"SymbolicDimensions:{v.SymbolicDimensions }");
			Debug.Log($"SymbolicDimensions.Length:{v.SymbolicDimensions.Length}");
			foreach (var e in v.SymbolicDimensions) Debug.Log(e);
		}
		Debug.Log("************************OUTPUTMETADATA");
		foreach (KeyValuePair<string, NodeMetadata> item in _infsession.OutputMetadata) { //only 1
			Debug.Log("+++++" + item.Key + ": ");
			var v = item.Value;
			Debug.Log($"Dimensions:{v.Dimensions}"); //[1, 384, 384]
			Debug.Log($"Dimensions.Length:{v.Dimensions.Length}");
			foreach (var e in v.Dimensions) Debug.Log(e);

			Debug.Log($"ElementType:{v.ElementType}");
			Debug.Log($"IsTensor:{v.IsTensor}");
			Debug.Log($"OnnxValueType:{v.OnnxValueType}");

			Debug.Log($"SymbolicDimensions:{v.SymbolicDimensions }");
			Debug.Log($"SymbolicDimensions.Length:{v.SymbolicDimensions.Length}");
			foreach (var e in v.SymbolicDimensions) Debug.Log(e);
		}

		Debug.Log("************************MODELMETADATA");
		var mm = _infsession.ModelMetadata;
		foreach (KeyValuePair<string, string> item in mm.CustomMetadataMap)
			Debug.Log(item.Key + ": " + item.Value);
		Debug.Log($"Description:{mm.Description}");
		Debug.Log($"Domain:{mm.Domain}");
		Debug.Log($"GraphDescription:{mm.GraphDescription}");
		Debug.Log($"GraphName:{mm.GraphName}");
		Debug.Log($"ProducerName:{mm.ProducerName}");
		Debug.Log($"Version:{mm.Version}");
	}
}