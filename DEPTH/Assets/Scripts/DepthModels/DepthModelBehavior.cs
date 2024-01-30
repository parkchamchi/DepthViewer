/*
	Modified from https://github.com/GeorgeAdamon/monocular-depth-unity/blob/main/MonocularDepthBarracuda/Packages/DepthFromImage/Runtime/DepthFromImage.cs
	Original License:
		MIT License

		Copyright (c) 2021 GeorgeAdamon

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

//#define _CHANNEL_SWAP //baracular 1.0.5 <=

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Sentis;
using Unity.Sentis.ONNX;

public class DepthModelBehavior : MonoBehaviour {
	/*
	Built-in: midas v2.1 small model
	*/

	public ModelAsset BuiltIn;

	public string OnnxRuntimeGpuProvider {get; set;} = null;
	public int OnnxRuntimeGpuId {get; set;} = 0;
	public string OnnxRuntimeGpuSettings {get; set;} = null;

	public int WidthFallback {get; set;} = 256;
	public int HeightFallback {get; set;} = 256;

	private static DepthModel _donnx;

	public DepthModel GetBuiltIn() {
		_donnx?.Dispose();

		string modelType = "MidasV21Small";

		_donnx = new SentisDepthModel(BuiltIn, modelType);

		return _donnx;
	}

	public DepthModel GetDepthModel(string onnxpath, string modelType, bool useOnnxRuntime=false) {
		_donnx?.Dispose();
		_donnx = null;

		if (!useOnnxRuntime)
			_donnx = new SentisDepthModel(onnxpath, modelType, widthFallback:WidthFallback, heightFallback:HeightFallback);
		else {

#if !UNITY_STANDALONE_WIN && !UNITY_EDITOR_WIN
			Debug.LogError("Not using onnxruntime but got useOnnxRuntime=true!");
			return null;
#else			
			_donnx = new OnnxRuntimeDepthModel(onnxpath, modelType, provider: OnnxRuntimeGpuProvider, gpuid: OnnxRuntimeGpuId, settings: OnnxRuntimeGpuSettings);
#endif

		}

		return _donnx;
	}

	//Wrapper
	public DepthModel GetZmqDepthModel(int port=5555, System.Action onDisposedCallback=null, float timeout=2, int failTolerance=3) {
		_donnx?.Dispose();
		_donnx = null;

		_donnx = new ZmqDepthModel(port, onDisposedCallback, timeout: timeout, failTolerance: failTolerance);

		return _donnx;
	}
}

public class SentisDepthModel : DepthModel {
	public string ModelType {get; private set;}

	private RenderTexture _input;
	private float[] _output;
	private int _width, _height;
	private IWorker _engine;
	private Model _model;

	private int _widthFallback, _heightFallback;

	public SentisDepthModel(ModelAsset nnm, string modelType, int widthFallback=256, int heightFallback=256) {
		_model = ModelLoader.Load(nnm);

		Init(modelType, widthFallback, heightFallback);
	}

	public SentisDepthModel(string onnxpath, string modelType, int widthFallback=256, int heightFallback=256) {
		var onnx_conv = new ONNXModelConverter(true);
		//_model = onnx_conv.Convert(onnxpath);
		//Was this change necessary?
		_model = onnx_conv.Convert(onnxpath, Path.GetDirectoryName(onnxpath));

		Init(modelType, widthFallback, heightFallback);
	}

	private void Init(string modelType, int widthFallback, int heightFallback) {
		_widthFallback = widthFallback;
		_heightFallback = heightFallback;

		ModelType = modelType;

		InitializeNetwork();
		AllocateObjects();
	}

	public Depth Run(Texture inputTexture) {
		/*
		Returns a private member (may change)
		*/

		if (inputTexture == null || _model == null)
			return null;

		// Fast resize
		Graphics.Blit(inputTexture, _input);

		RunModel(_input);
		
		return new Depth(_output, _width, _height);
	}

	private void OnDestroy() => DeallocateObjects();

	public void Dispose() {
		DeallocateObjects();
	}

	/// Loads the NNM asset in memory and creates a ~~Barracuda~~ Sentis IWorker
	private void InitializeNetwork()
	{
		// Create a worker
		//_engine = WorkerFactory.CreateWorker(_model, WorkerFactory.Device.GPU);
		//BackendType: {CPU, GPUCommandBuffer, GPUCompute, GPUPixel}.
		_engine = WorkerFactory.CreateWorker(BackendType.GPUCompute, _model);

		/*
		// Get Tensor dimensionality ( texture width/height )
		// In Barracuda 1.0.4 the width and height are in channels 1 & 2.
		// In later versions in channels 5 & 6
		#if _CHANNEL_SWAP
			_width  = _model.inputs[0].shape[5];
			_height = _model.inputs[0].shape[6];
		#else
			_width  = _model.inputs[0].shape[1];
			_height = _model.inputs[0].shape[2];
		#endif
		*/
		//AssertionException: IndexError: axis 5 is out of bounds shape of rank, 4
		//[1, 3, 256, 256]
		//Set these to (518, 518) for Depth-Anything models

		var heightSym = _model.inputs[0].shape[2];
		var widthSym = _model.inputs[0].shape[3];

		if (heightSym.isValue)
			_height  = heightSym.value;
		else {
			Debug.Log($"SentisDepthModel: Height cannot be inferred, falling back to {_heightFallback}");
			_height = _heightFallback;
		}

		if (widthSym.isValue)
			_width = widthSym.value;
		else {
			Debug.Log($"SentisDepthModel: Width cannot be inferred, falling back to {_widthFallback}");
			_width = _widthFallback;
		}

		_output = new float[_width*_height];
	}

	/// Allocates the necessary RenderTexture objects.
	private void AllocateObjects() {
		// Check for accidental memory leaks
		if (_input  != null) _input.Release();
		
		// Declare texture resources
		_input  = new RenderTexture(_width, _height, 0, RenderTextureFormat.ARGB32);
		
		// Initialize memory
		_input.Create();
	}

	/// Releases all unmanaged objects
	private void DeallocateObjects() {
		_engine?.Dispose();
		_engine = null;

		if (_input != null) _input.Release();
		_input = null;

		_output = null;

		_model = null;
	}

	/// Performs Inference on the Neural Network Model
	private void RunModel(Texture source) {
		//using (var tensor = new Tensor(source, 3)) {
		using (var tensor = TextureConverter.ToTensor(source, channels:3)) {
			_engine.Execute(tensor);
		}
		
		/*
		// In Barracuda 1.0.4 the output of MiDaS can be passed  directly to a texture as it is shaped correctly.
		// In later versions we have to reshape the tensor. Don't ask why...
		#if _CHANNEL_SWAP
			var to = _engine.PeekOutput().Reshape(new TensorShape(1, _width, _height, 1));
		#else
			var to = _engine.PeekOutput();
		#endif
		//I don't know what this code does, both have the same output for me
		*/

		var outTensor = _engine.PeekOutput() as TensorFloat;
		outTensor.MakeReadable();
		float[] output = outTensor.ToReadOnlyArray();

		//to?.Dispose();

		float min = output.Min();
		float max = output.Max();

		//Normalize
		for (int i = 0; i < output.Length; i++) 
			//_output[(i%_width)*_width + (i/_width)] = (output[i] - min) / (max - min); //col*_width + row
			_output[i] = (output[i] - min) / (max - min);
	}
}
