# DepthViewer
![vvvvvv3d](./examples/vvvvvv3d.jpg) \
Using [MiDaS Machine Learning Model](https://github.com/isl-org/MiDaS), renders 2D videos/images into 3D object with Unity for VR.

## Try Now
- [WebGL Demo](https://parkchamchi.github.io/DepthViewer/) ([WebXR version](https://parkchamchi.github.io/DepthViewer/vr_version/))
- [Releases](https://github.com/parkchamchi/DepthViewer/releases)
- [Steam Page](https://store.steampowered.com/app/2218510/DepthViewer/)
- [Play Store (Yes, you can use your $5 cardboard)](https://play.google.com/store/apps/details?id=com.parkchamchi.DepthViewer)

## Examples

| Original input (resized) | v2.1 small (built in) | Src |
| --- | --- | --- |
| ![landscape_orig](./examples/landscape_orig.jpg) | ![landscape_small](./examples/landscape_100.jpg) | [#](https://commons.wikimedia.org/wiki/File:%D0%9F%D0%B0%D0%BD%D0%BE%D1%80%D0%B0%D0%BC%D0%B0_%D0%86%D0%BD%D1%82%D0%B5%D0%B3%D1%80%D0%B0%D0%BB%D1%83.jpg) |
| ![mounts_orig](./examples/mounts_orig.jpg) | ![boat_small](./examples/mounts_100.jpg) | [#](https://pixnio.com/media/lake-dark-blue-glacier-mountain-peak-landscape) |

| Original input (resized) | v2.1 small (built in) | dpt-large model | Src |
| --- | --- | --- | --- |
| ![cat_orig](./examples/cat_orig.gif) | ![cat_small](./examples/cat_100.gif) | ![cat_large](./examples/cat_400.gif) | [#](https://commons.wikimedia.org/wiki/File:Cat_kneading_blanket.gk.webm) |

## So what is this program?
This program is essentially a depthmap plotter with an integrated depthmap inferer, with VR support.<br>
<br>
![demo_basic](./examples/demo_basic.png)<br>
<br>

The depthmaps can be cached to a file so that it can be loaded later.
<br>
![demo_cache](./examples/demo_cache.png)<br>
<br>

## Inputs
- Right mouse key: hides the UI.
- WASD: rotate the mesh.
- Backtick `: opens the console.

## Models
The built-in model is [MiDaS v2.1 small model](https://github.com/isl-org/MiDaS/releases/tag/v2_1), which is ideal for real-time rendering.

### Loading an ONNX model
Open the console and insert
```xml
load_model <onnx_path> false
```
`false` uses Unity's Barracuda and `true` uses OnnxRuntime, which is somewhat buggy and inaccurate now.<br>
<br>

To make OnnxRuntime to use CUDA (takes effect in the next load),
```
set_onnxruntime_params true 0
```
The final `0` is the id of GPU.<br>
<br>

To load the built-in model,
```
load_builtin
```
<br>

To see the current model,
```
print_model_type
```

### Using depth.py and depthserver.py (OPTIONAL)
`depth.py` is for generating `.depthviewer` files so that it can be opened with the DepthViewer.
It can be executed independently from the command console or called from the main program using the console command `send_msg CallPythonHybrid` or `send_msg CallPythonLarge` (replaces the `Call Python` buttons). 

#### Dependencies for depth.py

Also check the [MiDaS github page](https://github.com/isl-org/MiDaS). 

1. Install Python3. The version I use is `3.9.6`. By default the program calls `python`, assuming it is on PATH. This can be changed in the options menu.
2. Install OpenCV and Numpy. <br>
`pip install opencv-python numpy`
3. (Optional but recommended) Install CUDA. You may want to get the [version 11.7](https://developer.nvidia.com/cuda-11-7-0-download-archive); see below.
4. Install Pytorch that matches your environment from [here](https://pytorch.org/get-started/locally/). For me (win64 cuda11.7) it is <br>
`pip install torch torchvision --extra-index-url https://download.pytorch.org/whl/cu117`
5. Install [timm](https://pypi.org/project/timm/) for MiDaS. <br>
`pip install timm`
6. Go to the directory `depthpy` and run <br>
`python depth.py -h` <br>
and see if it prints the manual without any error.
7. Get `dpt_hybrid` and `dpt_large` models from [here](https://github.com/isl-org/MiDaS#setup) and locate them in `depthpy/weights`. Do not change the filenames.
8. Place any image in the `depthpy` directory, rename it to `test.jpg` (or `test.png`) and run <br>
`python depth.py test.jpg out.depthviewer -i` <br>
See if it generates an output. Also check if `depth.py` is using CUDA by checking `device: cuda` line.

#### If it isn't and you want to use CUDA:
- Check the installed CUDA version and if the installed Pytorch version supports that.
- Uninstall Pytorch `pip uninstall torch torchvision` and reinstall it.

#### For depthserver.py
- Install Flask `pip install Flask`
- Run `python depthserver.py` to open the server and connect to it via the option menu. If it's connected all image inputs will be processed by calling the server.

## Recording 360 VR video
If you select a depthfile and an according image/video, a sequence of .jpg file will be generated in `Application.persistentDataPath`. \
Go to the directory, and execute
```xml
ffmpeg -framerate <FRAMERATE> -i %d.jpg <output.mp4>
```
Where `<FRAMERATE>` is the original FPS. 

To add audio,
```xml
ffmpeg -i <source.mp4> -i <output.mp4> -c copy -map 1:v:0 -map 0:a:0 -shortest <output_w_audio.mp4>
```

## Connecting to an image server
The server has to provide a `jpg` or `png` bytestring when requested. 
Like [this program](https://github.com/parkchamchi/screencaptureserver): it captures the screen and returns the jpg file.
I found it to be faster than the built-in one (20fps for 1080p video).
<br>
Open the console with the backtick ` key and execute (url is for the project above, targeting the second monitor)
```
httpinput localhost:5000/screencaptureserver/jpg?monitor_num=2
```

## Tested formats:
### Images
- .jpg
- .png

### Videos
- .mp4, ... : 
Some files can't be played because Unity's VideoPlayer can't open them. (e.g. VP9) 

### Others
- .gif : Certain formats are not supported.
- .pgm : Can be used as a depthmap (Needs a subsequential image input)
- .depthviewer

## Notes
- If VR HMD is detected, it will open with OpenXR.
- All outputs will be cached to `Application.persistentDataPath` (In Windows, `...\AppData\LocalLow\parkchamchi\DepthViewer`).
- Depth files this program creates are of extention `.depthviewer`, which is a zip file with .pgm files and a metadata file.
- Rendering the desktop is only supported in Windows for now.
- C# scripts are in [DEPTH/Assets/Scripts](DEPTH/Assets/Scripts).
- Python scripts are in [DEPTH/depthpy](DEPTH/depthpy).

## Todo
- Overhaul UI & Control
- Add more options
- Fix codecs
- Stablize
### WIP
- VR controllers support [(See here)](https://github.com/parkchamchi/UnityVRControllerTest)
- Support for the servers that send both the image file and the depthmap

## Building
The Unity Editor version used: `2021.3.10f1`

### ONNX Runtime dll files
These dll files have to be in `DEPTH/Assets/Plugins/OnnxRuntimeDlls/win-x64/native`.
They are in the nuget package files (.nupkg), get them from <br>
<br>
[Microsoft.ML.OnnxRuntime.Gpu](https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime.Gpu/) => `microsoft.ml.onnxruntime.gpu.1.13.1.nupkg/runtimes/win-x64/native/*.dll` <br>
- `onnxruntime.dll`
- `onnxruntime_providers_shared.dll`
- `onnxruntime_providers_cuda.dll`
- I don't think this is needed: `onnxruntime_providers_tensorrt.dll`

[Microsoft.ML.OnnxRuntime.Managed](https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime.Managed/) => `microsoft.ml.onnxruntime.managed.1.13.1.nupkg/lib/netstandard1.1/*.dll` <br>
- `Microsoft.ML.OnnxRuntime.dll`

## Misc
### Libraries used
- [Unity Standalone File Browser](https://github.com/gkngkc/UnityStandaloneFileBrowser) (MIT License)
- [Unity Simple File Browser](https://github.com/yasirkula/UnitySimpleFileBrowser) (MIT License)
- [WebXR Export](https://github.com/De-Panther/unity-webxr-export) (Apache License 2.0)
- [Google Cardboard XR Plugin for Unity](https://github.com/googlevr/cardboard-xr-plugin) (Apache License 2.0)
- [UniGif](https://github.com/WestHillApps/UniGif) (MIT License)
- [ONNX Runtime](https://github.com/microsoft/onnxruntime) (MIT License)
- [In-game Debug Console for Unity 3D](https://github.com/yasirkula/UnityIngameDebugConsole) (MIT License)

- Font used: [Noto Sans KR](https://fonts.google.com/noto/specimen/Noto+Sans+KR) (SIL Open Font License)
- [Readme file](DEPTH/Assets/Assets/README.txt)

### Also check out
- This project was inspired by [VRin](https://www.vrin.app/)
- [monocular-depth-unity](https://github.com/GeorgeAdamon/monocular-depth-unity) ([used this code](https://github.com/GeorgeAdamon/monocular-depth-unity/blob/main/MonocularDepthBarracuda/Packages/DepthFromImage/Runtime/DepthFromImage.cs))
- [godot-midas-depth](https://github.com/lewiji/godot-midas-depth)