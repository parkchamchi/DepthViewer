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
using UnityEngine;
using Unity.Barracuda;

public class DepthONNXBehavior : MonoBehaviour {
	/*This script has to be in a single object.*/

	public NNModel NNM;
	private static DepthONNX _donnx;

	public const string ModelType = "v2.1-small";
	public const string Weight = "MiDaS_model-small.onnx";

	public DepthONNX GetDepthONNX() {
		if (_donnx == null)
			_donnx = new DepthONNX(NNM, ModelType, Weight);

		return _donnx;
	}
}

public class DepthONNX : IDisposable {
	public readonly string ModelType;
	public readonly string Weight;

	private NNModel _nnm;

	private RenderTexture _input;
	private float[] _output;
	private int _width, _height;
	private IWorker _engine;
	private Model _model;

	public DepthONNX(NNModel NNM, string model_type, string weight) {
		_nnm = NNM;

		ModelType = model_type;
		Weight = weight;

		InitializeNetwork();
		AllocateObjects();
	}

	public float[] Run(Texture inputTexture, out int x, out int y) {
		x = _width;
		y = _height;

		if (inputTexture == null || _nnm == null) {
			x = y = 0;
			return null;
		}

		// Fast resize
		Graphics.Blit(inputTexture, _input);

		RunModel(_input);
		
		return _output;
	}

	private void OnDestroy() => DeallocateObjects();

	public void Dispose() {
		DeallocateObjects();
	}

	/// Loads the NNM asset in memory and creates a Barracuda IWorker
	private void InitializeNetwork()
	{
		if (_nnm == null) {
			Debug.LogError("_nnm == null.");
			return;
		}

		// Load the model to memory
		_model = ModelLoader.Load(_nnm);

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
