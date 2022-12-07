using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using Microsoft.ML.OnnxRuntime;
//using Microsoft.ML.OnnxRuntime.Gpu;
using Microsoft.ML.OnnxRuntime.Tensors;

/*
These dll files has to be in DEPTH/Assets/Plugins/OnnxRuntimeDlls/win-x64/native
They are in the nuget package files (.nupkg), get them from
	https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime.Managed/
		[THE NUPKG FILE]/lib/netstandard1.1/*.dll
	https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime.Gpu/
		[THE NUPKG FILE]/runtimes/win-x64/native/*.dll

	From Microsoft.ML.OnnxRuntime.Gpu
		onnxruntime.dll
		onnxruntime_providers_shared.dll
		onnxruntime_providers_cuda.dll
		onnxruntime_providers_tensorrt.dll (i don't think that this is needed)

	From Microsoft.ML.OnnxRuntime.Managed
		Microsoft.ML.OnnxRuntime.dll

I think it would work in the linux build if you get the .so files in linux-64 directory

Used https://github.com/lewiji/godot-midas-depth/blob/master/src/Inference/InferImageDepth.cs as reference
	MIT License

	Copyright (c) 2022 Lewis James

	Permission is hereby granted, free of charge, to any person obtaining a copy
	of this software and associated documentation files (the "Software"), to deal
	in the Software without restriction, including without limitation the rights
	to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
	copies of the Software, and to permit persons to whom the Software is
	furnished to do so, subject to the following conditions:

	The above copyright notice and this permission notice shall be included in all
	copies or substantial portions of the Software.

	THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
	IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
	FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
	AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
	LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
	OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
	SOFTWARE.
*/

public class OnnxRuntimeDepthModelBehavior : MonoBehaviour {
	private string _largeModelPath = "D:/tmp/dpt_large-midas.onnx";

	public DepthModel GetLargeModel() {
		var modeltype = DepthFileUtils.ModelTypes.MidasV3DptLarge;
		return new OnnxRuntimeDepthModel(_largeModelPath, modeltype.ToString(), (int) modeltype);
	}
}

public class OnnxRuntimeDepthModel : DepthModel {
	public string ModelType {get; private set;}
	public int ModelTypeVal {get; private set;}

	private InferenceSession _infsession;
	private int _width, _height;
	private string _inputname;
	private int _outwidth, _outheight;

	private RenderTexture _rt;
	private float[] _output;

	public OnnxRuntimeDepthModel(string onnxpath, string modelType, int modelTypeVal) {
		ModelType = modelType;
		ModelTypeVal = modelTypeVal;

		_infsession = new InferenceSession(onnxpath, SessionOptions.MakeSessionOptionWithCudaProvider(0));
		foreach (KeyValuePair<string, NodeMetadata> item in _infsession.InputMetadata) {
			_inputname = item.Key;
			_width = item.Value.Dimensions[2];
			_height = item.Value.Dimensions[3];
		} //only 1
		foreach (KeyValuePair<string, NodeMetadata> item in _infsession.OutputMetadata) {
			_outwidth = item.Value.Dimensions[1];
			_outheight = item.Value.Dimensions[2];
		} //only 1

		_rt = new RenderTexture(_width, _height, 16);
		_output = new float[_outwidth * _outheight];
	}

	public float[] Run(Texture inputTexture, out int x, out int y) {
		int length = _width * _height;

		Graphics.Blit(inputTexture, _rt);

		Texture2D tex = new Texture2D(_width, _height);
		RenderTexture.active = _rt;
		tex.ReadPixels(new Rect(0, 0, _width, _height), 0, 0);
		RenderTexture.active = null;
		UnityEngine.GameObject.Destroy(tex);

		var rawdata = tex.GetRawTextureData();
		//Debug.Log($"rawdata.Length: {rawdata.Length}"); //why is this more than 384*384*4 ?

		float[] rfloats = new float[length];
		float[] gfloats = new float[length];
		float[] bfloats = new float[length];

		for (int i = 0; i < length; i++) {
			rfloats[i] = (float) rawdata[i*4 + 0] / 255;
			gfloats[i] = (float) rawdata[i*4 + 1] / 255;
			bfloats[i] = (float) rawdata[i*4 + 2] / 255;
			//a = rawdata[i*4 + 3];
		}
		
		var dimensions = new ReadOnlySpan<int>(new []{1, 3, _height, _width});
		var t1 = new DenseTensor<float>(dimensions);
		for (var j = 0; j < _height; j++) {
			for (var i = 0; i < _width; i++) {
				var index = j * _height + i;
				t1[0, 0, j, i] = rfloats[index];
				t1[0, 1, j, i] = gfloats[index];
				t1[0, 2, j, i] = bfloats[index];
			}
		}

		var inputs = new List<NamedOnnxValue>() {
			NamedOnnxValue.CreateFromTensor<float>(_inputname, t1)
		};

		using var results = _infsession?.Run(inputs);
		float[] output = results?.First().AsEnumerable<float>().ToArray();
		results?.Dispose();

		float max = output.Max();
		float min = output.Min();

		for (int i = 0; i < length; i++) 
			_output[(_height-1-(i/_width))*_width + (i%_width)] = (output[i] - min) / (max - min); //rotate 180

		x = _outwidth;
		y = _outheight;
		return _output;
	}

	public float[] RunAndClone(Texture inputTexture, out int x, out int y) {
		return (float[]) Run(inputTexture, out x, out y).Clone();
	}

	public void Dispose() {
		_infsession.Dispose();
		_infsession = null;

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