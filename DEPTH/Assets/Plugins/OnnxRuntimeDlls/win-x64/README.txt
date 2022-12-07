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

Refer to OnnxRuntimeDepthModelBehavior.cs