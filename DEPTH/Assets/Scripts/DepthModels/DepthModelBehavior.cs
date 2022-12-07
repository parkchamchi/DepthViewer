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

#define _CHANNEL_SWAP //baracular 1.0.5 <=

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;

using UnityEngine;
using Unity.Barracuda;
using Unity.Barracuda.ONNX;

public class DepthModelBehavior : MonoBehaviour {
	/* Built-in: midas v2.1 small model*/

	public NNModel BuiltIn;

	private DepthFileUtils.ModelTypes _builtInModelType = DepthFileUtils.ModelTypes.MidasV21Small;

	private static DepthModel _donnx;

	private string _modelType;
	private int _modelTypeVal;

	public DepthModel GetBuiltIn() {

		if (_donnx != null && _donnx.ModelType != _modelType && _donnx.ModelTypeVal != _modelTypeVal) {
			_donnx.Dispose();
			_donnx = null;
		}

		if (_donnx == null) {
			_modelType = _builtInModelType.ToString();
			_modelTypeVal = (int) _builtInModelType;

			_donnx = new DepthONNX(BuiltIn, _modelType, _modelTypeVal);
		}

		return _donnx;
	}
}

public class DepthONNX : DepthModel {
	public string ModelType {get; private set;}
	public int ModelTypeVal {get; private set;}

	private RenderTexture _input;
	private float[] _output;
	private int _width, _height;
	private IWorker _engine;
	private Model _model;

	public DepthONNX(NNModel nnm, string modelType, int modelTypeVal) {
		_model = ModelLoader.Load(nnm);

		ModelType = modelType;
		ModelTypeVal = modelTypeVal;

		InitializeNetwork();
		AllocateObjects();
	}

	public DepthONNX(string onnxpath, string modelType, int modelTypeVal) {
		/*
		Currently not used.
		Args:
			onnxpath: path to .onnx file
		*/

		var onnx_conv = new ONNXModelConverter(true);
		_model = onnx_conv.Convert(onnxpath);

		ModelType = modelType;
		ModelTypeVal = modelTypeVal;

		InitializeNetwork();
		AllocateObjects();
	}

	public float[] Run(Texture inputTexture, out int x, out int y) {
		/*
		Returns a private member (may change)
		*/

		x = _width;
		y = _height;

		if (inputTexture == null || _model == null) {
			x = y = 0;
			return null;
		}

		// Fast resize
		Graphics.Blit(inputTexture, _input);

		RunModel(_input);
		
		//return (float[]) _output.Clone();
		return _output;
	}

	public float[] RunAndClone(Texture inputTexture, out int x, out int y) {
		/*
		Returns a clone of the output
		Not used
		*/
		return (float[]) Run(inputTexture, out x, out y).Clone();
	}

	private void OnDestroy() => DeallocateObjects();

	public void Dispose() {
		DeallocateObjects();
	}

	/// Loads the NNM asset in memory and creates a Barracuda IWorker
	private void InitializeNetwork()
	{
		// Create a worker
		_engine = WorkerFactory.CreateWorker(_model, WorkerFactory.Device.GPU);

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
		using (var tensor = new Tensor(source, 3)) {
			_engine.Execute(tensor);
		}
		
		// In Barracuda 1.0.4 the output of MiDaS can be passed  directly to a texture as it is shaped correctly.
		// In later versions we have to reshape the tensor. Don't ask why...
		#if _CHANNEL_SWAP
			var to = _engine.PeekOutput().Reshape(new TensorShape(1, _width, _height, 1));
		#else
			var to = _engine.PeekOutput();
		#endif
		//I don't know what this code does, both have the same output for me

		float[] output = TensorExtensions.AsFloats(to);

		to?.Dispose();

		float min = output.Min();
		float max = output.Max();

		//Rotate 90 degrees & Normalize
		for (int i = 0; i < output.Length; i++) 
			_output[(i%_width)*_width + (i/_width)] = (output[i] - min) / (max - min); //col*_width + row
	}
}
