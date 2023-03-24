# Temporary technical memos

Written reviewing the version [v0.8.8-beta-prerelease-2](https://github.com/parkchamchi/DepthViewer/releases/tag/v0.8.8-beta-prerelease-2) <br>
Everything is subject to change. <br>
**WIP**

## General
- Android and WebGL builds has not been tested beyond the version `v0.6.2`
- The main engine is `TexInputs`. It fetches the input (e.g. a `jpg` file to a Texture object) then infers the depth using the inferer `DepthModel`. Then it updates the mesh `MeshBehavior`.
When this cycle is finished (not parallelized), the `MainBehavior` calls `TexInputs.UpdateTex()`, repeating the cycle.


## `MainBehavior.cs`
The main script of the program. It's a bit messy and has many points to be fixed

### enum `FileTypes`
The constants for indicating the type of the input. To summary:
- `NotExists`, `Dir`, `Unsupported`
- `Img`, `Vid`, `Depth`: `Depth` is the "depthfile" that contains depth maps of images or videos.
These share the same processing class `ImgVidDepthTexInputs` since they are heavily interwined (since it can't determine if it's a image or a video before opening the depthfile).
While `ImgVidDepthTexInputs` is a bit bloated, if depthfile processing is not considered handling image inputs is actually fairly short,
see [here, a three-line snippet (ad hoc code for webgl, not tested)](https://github.com/parkchamchi/DepthViewer/blob/1e6ca57dd9e1c0233fa1fe1bcf2cb8007f79c8ac/DEPTH/Assets/Scripts/MainBehavior.cs#L432) that loads the image, infers the depth, and visualizes it.
- `Online`: inputs that keeps changing. Like mirroring the desktop screen, where input image keeps changing every time it is requested. Also used for the Http inputs.
- `Gif`
- `Pgm`: the [Portable Graymap Format](https://netpbm.sourceforge.net/doc/pgm.html) that contains a grayscale image.
It is used as the depth map format for this program and the python scripts, and it can be used independently as the depth map input.
Note that in this way the inference engine is not needed.

### `Start()`
The main initialization.

#### First we find the MonoBehaviours to be used.
- `MeshBehavior`: the object the depth values will be visualized
- `DepthModelBehavior`: the depth map inference engine
- `VRRecordBehavior`: this is for recording the screen on VR headsets.
- `ServerConnectBehavior`: this is to connect to a server that provides the depth map when an image is given (`AsyncDepthModel`).
It is only implemented for the image inputs and it usage is not recommended.
Also while waiting the server, it may block changing the input.
Not to be confused with `HttpOnlineTex`, which fetches *images* from the server.

#### Then we find the `VideoPlayer` and assign it to `_vp`.
But it seems it can be removed here, since `_vp` is only for used for being destroyed on the termination of the program (which can be just fetched then) and to be used for an argument for `ImgVidDepthTexInputs`.
If there were several meshes and different `VideoPlayer`s it would make sense, but as it doesn't seem to be implemented, I think this can be omitted here and have it fetched on `ImgVidDepthTexInputs`.

#### Calling ToggleOutputSave(), ToggleSearchCache()
Initialize the `OutputSave` and `SearchCache` toggles. Well, sort of. It's actually to set the variables `_canUpdateArchive` and `_searchCache` based on the toggle UI's values (i.e. the value that was set on the editor).
Can these variables just be replaced with `OutputSaveToggle.isOn` and `SearchCacheToggle.isOn`?

#### Command line arguments
This code is outdated and would only work for image and video inputs, if there's any usage for this.

#### The keys to be sent to _texInputs
If the keys in `_sendMsgKeyCodes` is pressed, it will be sent to the active `_texInputs` (that is processing the input). For example, if `Keypad5` is pressed, it will inform the `ImgVidDepthTexInputs` and will pause the video.

#### The file selecter
By default it uses the Standalone File Browser, which calls the OS file explorer.

#### Set the console commands
The current code makes me nauseous. Also the commands of `_depthModelBahav` can be moved to that class, since there wouldn't be meshes other than the main one.

#### Load the built-in model
This does not call the wrapper method `LoadBuiltIn()` but instead just calls `_depthModelBehav` directly. The reason behind this is that the wrapper method expects the other objects to be already initialized.

#### Load the options
First we import the min/max values of the sliders using `MeshSliderParents` and load the options from `Utils`.

### `Update()`
Called every frame. Since the inference is not parallelized, this is called every time `_texInputs` is done inferencing and setting the mesh.

#### Hiding the UI by the right click.

#### Detect the mouse wheel event, and change the input if possible
This really has to be in the another class, more below.
Note that it does not take effect when the option menu is active, but when the UI is hidden this exception would be ignored.
As now the windows are managed by `WindowsManager` I think it can be generalized instead of checking the Options menu.
But it would not make sense for the small windows (e.g. directory window), so extra steps would be needed.

#### At last, update the TexInputs
Also send the pressed key.

### `ToggleFullscreen()`
**BUG**: when changed to the fullscreen, it would just stretch the previous screen and make it look blured.

### `ToggleOutputSave()`
When `SaveOutput` is on, `SearchCache` will be always on. This is because saving the output requires computing the hash, and that hash can be used to find the cache. Of course this can be optional, but I think there's no reason not to.

### The directory setters
As said, this should be sepereated to another class.

When the user clicks `browse dirs` button, it calls `BrowseDirs()`. This saves the filenames on a list. The gif files can be omitted, since the current gif decoder is slow.
It just omits from the list so to see gifs after loading the directory it has to be loaded again...

### And several wrapper functions...
Most of them are called from `OptionsBehavior`, some of them can just be inserted to it. Maybe the mesh should be assigned to a static varible.

### `EnterVrMode()`
I could not make my WMR click the UI idk

### `Exts`
Note that it does not check if the file actually exists.

## `MeshBehavior.cs`

### `IDepthMesh`
The interface to be used by `TexInputs`.

#### `ShouldUpdateDepth`
When this is false, the mesh would not update when the actual depth value is not changed. False for the video inputs.

#### `SetScene()`
Sets the depth values.

### `MeshBehavior`

#### `_vertices[]` and `_vertices_proj[]`
The former stores the (x, y) values before the projection.

#### Parameter `Alpha` and `Beta`
They are used for inversing the depth, z = 1/(`Alpha` * x + `Beta`).

#### `_camDistPerCamDist`
The constant `0.96f/150`. In the older versions the mesh was 150m from the camera, and its scale was 1.
After the new parameters the mesh should not change its size when the camera distance is changed, it has to be scaled linearly. Thus it is divided by 150.
Also the default scaled was shrinked a bit, about 4%.

#### Parameter `CamDist`
Distance between the camera and the mesh. Changing this will make the mesh disappeared, since seeing it changing hurts my eyes.

#### Parameter `ScaleR`
The *R*elative scale.

#### Parameter `DepthMultR`

#### Parameter `MeshHor` and `MeshVer`
These two parameters are not useful now.

#### Paramter `ProjRatio`
When `1`, the mesh would project the vertices on the screen.

#### Paramter `TargetVal` and `Threshold`
Deprecated: the values lowers than `Threshold` would be set to `TargetVal`.

#### _mesh.indexFormat
If this is not set to `UInt32`, the depth maps whose size is bigger than `256*256`, which is the size of the built-in model, will be broken.

### `MeshShaders`

#### Property `ShouldSetVertexColors`
Indicates if should set the vertex colors. Used for point clouds.

#### `MaterialProperties`
Values to set to the material.

#### Point cloud shaders
It uses the shader included in [this library](https://github.com/keijiro/Pcx). The point size should be scaled later.

### `Wiggler`
Rotates the mesh in predefined manner. If the angles differ too much it would look clunky.

## `DepthModel.cs`

- Interface `DepthModel` has a `Run()` method that returns a `Depth` object.
- Interface `AsyncDepthModel` has a callback that would be called when the inference is complete. Used by `ServerConnectBehavior`.

### enum `DepthMapType`

- `Inverse`: true depth values are scaled, shifted and inversed. Parameter `Alpha` and `Beta` are used for this.
- `Linear`
- `Metric`: Actual values in meter (which is identical to a Unity unit)

`Inverse` and `Linear` are expected to be normalized to [0, ..., 1]

## `DepthModelBehavior.cs`

### class `BarracudaDepthModel`

Uses Unity's [Barracuda](https://github.com/Unity-Technologies/barracuda-release) engine.
The current version is `v3.0`, which does not support MiDaS v3 or higher.
The code is modified from [here](https://github.com/GeorgeAdamon/monocular-depth-unity/blob/main/MonocularDepthBarracuda/Packages/DepthFromImage/Runtime/DepthFromImage.cs)

## `OnnxRuntimeDepthModelBehavior.cs`
### class `OnnxRuntimeDepthModel`

TODO: change the filename to just `OnnxRuntimeDepthModel.cs`

```
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

I think it would work in the linux build if you get the .so files in linux-64 directory.
```

To use other providers other than CUDA, the matching provider dll has to be present, which does not exist on the official binary build. Thus it has to be built from the source.
The [GPU providers](https://onnxruntime.ai/docs/execution-providers/) to test and implement are: DirectML, OpenVINO, TVM, ROCm.

## `ServerConnectBehvior.cs`

To connect to a server that can infer the depth and return it as a `pgm` file when an image is given (`depthserver.py`).
TODO: Have `DepthServerModel` implement the `AsyncDepthModel`, not `ServerConnectBehavior`.

## `FileSelecters.cs`

### class `FileSelecter`

It has two methods: `SelectFile()` and `SelectDir()`, which calls the callback function when the path is selected.

- `StandaloneFileSelecter`: uses [`Standalone File Browser`](https://github.com/gkngkc/UnityStandaloneFileBrowser), which calls the OS file browser.
- `SimpleFileSelecter`: uses [`Simple File Browser`](https://github.com/yasirkula/UnitySimpleFileBrowser), which uses its own UI. Used for the Android version.
- `WebGLFileSelecter`: a WebGL version of `Standalone File Browser`. The current code wouldn't work, change it referencing the last version that supported WebGL. (`UploadFile` has to call the callback)

## `OnlineTex.cs`

### interface `OnlineTex`

Used when the input texture (almost) always changes.

- Field `Supported`: If the operation is available. I don't think it's needed anymore.
- Field `LastTime`: The last time the tex is updated. Always differs when it's real-time (e.g. Screen capturing)

- `StartRendering()`: Prepare the input.
- `GetTex()`: Fetch the texture.

### class `DesktopRenderBehavior`

Deprecated method to capture the screen. It calls Windows system calls to check the available processes and `System.Drawing` for capturing. Really slow.

### class `HttpOnlineTex`

Fetches the jpgs from the server `Screen Capture Server`[https://github.com/parkchamchi/screencaptureserver], which guarantees 20fps for 1080p.
- When it fails to fetch the image, it will return the placeholder texture.

## interface `TexInputs`

The main engine.

- `UpdateTex()`
- `SendMsg()`: send a subclass-specific message (e.g. video control)

- Field `WaitingSequentialInput`: if true, it's waiting for an additional input, such as (depth map, actual texture) pair
- `SequentialInput()`

## class `ImgVidDepthTexInputs`

Handles images, videos, and depthfiles.
This class was not changed much since its alpha era (when it was not split from `MeshBehavior`).
Most of its complexity comes from the low-level operations with the `VideoPlayer` and misdesigned interactions with the `DepthFileUtils`.

To point out some characteristics...
- The depthfile is compatible with the python scripts.
- The image input is the only input that supports `AsyncDepthModel`.
- When `searchCache` is on, it will check if the saved cache with the same hash value exists.
- When `canUpdateArchive` is on, it will save the output so that it can be loaded later when `searchCache` is on.

- By its type, one of `FromImage()`, `FromVideo()`, `FromDepthFile()` will be called.
- `FromDepthFile()` then receives the additional input and calls `FromImage()` of `FromVideo()`.

### `FromImage()`

- After fetching the texture from the file, `_orig_width` and `_orig_height` is set. This is actually insignificant, since its only purpose is to be saved to the depthfile metadata where they would not matter.
- Also set `_startFrame` and `_frameCount`, just for the compability with the video depthfile metadata.
- Then if `_searchCache` is on and it does not already have the path for depthfile (i.e. not from `FromDepthFile()`), check if the saved file exists.
- If the depthfile is found, load from it. This also sets `_paramsDict` to set the parameters if exists, more below.
- Else if `AsyncDepthModel` exists, infer it using it. This uses the callback `OnDepthReady()`. This will not create/modify the depthfile.
- Else, infer it using the `DepthModel` and save it if it should.
- Finally, set the mesh.

### `FromVideo()`

- Search the cache if it should.

#### If it's found
- Check if it's "full", which means all frames are processed and present in the file. Then it won't have to save the outputs to the depthfile.
- `_vp.sendFrameReadyEvents = (_startFrame < 0) ? true : false;`:
Ah I remember, on the python code to generate the depthfile `_startFrame` was set to `-1` since it can't be determined. In that case they have to initialize them on the first frame.
`_startFrame` not being a negative number means it has been processed using this program.
- Initialize the parameter, if it should.

#### Else
Have the `VideoPlayer` make the `frameReady` event so that the variables be initialized on the load.

- Finally, set the `VideoPlayer`'s target. The video will play automatically.

### `OnFrameReady()`

Called when `_vp.sendFrameReadyEvents` is true.

- On initialization (except a valid `_startFrame` was on the depthfile), this is called to set `_startFrame` and other variables to be saved to the depthfile.
- Also called when recording, where it's just redirected to `RecordingFrameReady()`.

### `UpdateVid()`

Called when `UpdateTex()` is called when a video input is present and is not recording.

- Check the frame number of the `_vp`. if it's identical to the already processed one (`_currentFrame`), ignore and return.
- Calculate `actualFrame`: some videos do no start at frame 0, which causes problems with the compability with the python script, where the first available frame (`_startFrame`) is always 0.
Subtract the `_vp`'s frame with the `_startFrame` to get the actual frame number that starts with 0.
- Fetch the texture.

- If the depthfile and the depth values for the current frame is present, load it.
- Else, infer it, save it if it should (create the depthfile if it should too)

- Finally, set the mesh.
- Update the parameters if the saved parameters for the frame exists.

### `OnLoopPointReached()`

By default the video always loops.

- Pause the video.
- Save the depth using `SaveDepth()`.
- If the depthfile was created, load it so it can be used.
- Replay the video.

### `SaveDepth()`

The "saving the output" operations above are async methods whose `Task` objects are save to the `_processedFrames` list.
Also has `shouldReload` parameter to reload the depthfile. This is set true when the depthfile is modified, because for some reason opening an entry that was already opened is prohibited even when no modification is done.

- Wait for all tasks in the list.
- Determine if the depthfile is full and if it is set `_shouldUpdateArchive` false. In this case when it's reopened the mode for the depthfile will be changed from `Update` to `Read`.

### `FromDepthFile()`
Indicates that an additional input is needed.

### `DepthFileInput()`
Called by `SequentialInput()`.

- Check the type of the input.
- Set the location for the depthfile
- Call `FromImage()` or `FromVideo()`.

### `StartRecording()`
Triggered when the matching command is given.

- Set the size out the output (2048, 4096)
- Create the path of the output
- If the input is the image, simply `Capture()` and return.
- Else, set `_recording` true, `_shouldCapture` (whether the screen should be captured at the very moment) false, make the `_vp` send the `frameReady` event, and rewind the video.

### `RecordingFrameReady()`
As `frameReady` is set true, this will called every frame.

- Pause the video.
- Call `UpdateVid()` manually. This changes the mesh.
- Set `_shouldCapture` true, indicating that the screen has to be captured.

### `UpdateRecording()`
Called when `UpdateTex()` is called and it's recording.
Only active when `_shouldCapture`.

- Capture the screen.
- Set the `_shouldCapture` false, making the mesh update
- Check if the video has ended, and advance or terminate.

### `Capture()`
Adds the task for capturing to `_processedFrames`.

### `RecrodingEnded()`
Cleanup and wait for all tasks.

### `PausePlay()`
TODO: delete the reference for the `AsyncDepthModel`

### `ExportParams()`
Saves the current parameters to `_paramDict`, which can be saved to the depthfile.
If `init`, this will be loaded at the first frame and the frame number for this will be `-1`.

### `Dispose()`
Cleanup the handlers, save the depths, save the `_paramsDist` if it should. Also dispose the `DepthFileUtils`.

## class `OnlineTexInputs`
Uses `OnlineTex`.

## class `GifTexInputs`

Decodes gif files using `UniGif`. Unfortunatley, this operation is slow (probablity not meant for real-time input).
It uses the `GifPlayer.cs` wrapper. More there.

## class `PgmTexInputs`

Uses pgm files as the depth map. Requires an additional input to use as the texture.

## `AboutScreenBehavior.cs`
The contents of the `About` screen is loaded from an text file.

## `SliderParentBehavior.cs`
For all sliders on this program. The values on the label changes when the slider value changes.

## `MeshSliderParentsBehavior.cs`

The scripts for the sliders for the mesh. Extends `SliderParentBehavior`.

### class `MeshSliderParents`

A static class to store all mesh sliders.
There min/max values can be saved and loaded.

### class `MeshSliderParentBehavior`

#### `Start()`

- Get the target parameter.
- Add listener to the mesh.
- Add itself to the `MeshSliderParents`.

#### `OnParamChanged()`
Invoked when any parameter in the mesh changes.

- Check if the parameter name matches.
- If the value is identical, it means the event was invoked by the slider.
- When the value is in range of the slider, set it accordingly.
- Else, just change the value of the label but not the slider's value. Also visually indicate this case.

## `OptionsBehavior.cs`
Operations for the options menu.
Most of them calls the wrapper methods in the `MainBehavior`.

### `ExportDepthMap()`
Exports the current depth values as an image file.

TODO: add an EXR export using `EncodeToEXR()`
BUG: resized depth maps have artifacts on the borders

## `SaveOptions.cs`
Why hasn't this been deleted

## `TempCanvasBehavior.cs`
When `VrMode()` is called, it would change the canvas to the world space and activate the VR controllers. Except the controller trigger does not work for unknown reason.

TODO: for android builds, revert this to [this](https://github.com/parkchamchi/DepthViewer/blob/aa633fc206be8cabe2d714ba2ce1d2884b07f455/DEPTH/Assets/Scripts/UI/TempCanvasBehavior.cs)

## `UIStaticClassSetters.cs`
Sets the GameObjects that are used by multiple classes to a static class.

## `WindowManager.cs`
Manages the windows so that only one is active on screen.

## `AndroidVRBehavior.cs`
For Cardboards.

## `DepthFileUtils.cs`
For manupulating depthfiles.

TODO: seperate actual static utility functions and the ones that can be disposed

### `CreateDepthFile()`
Parallel to the python version.

### `GetDepthFileName()`
Get the matching depthfile name given the filename, hash value, and the model.

### `UpdateDepthFile()`
Save the depth values at the frame given.

- All files are saved as 8-bit pgm file.
- The variable `_count` keeps track of valid frames, and when it reaches the framecount the file is set to `full`.

### `ProcessedDepthFileExists()`
Check if the depthfile is available with the matching hashval and the model.

### `WriteMetadata()`
Again, parallel with the python version.

### `WritePGM()`
Converts the `Depth` object into a pgm bytestring.

### `ReadDepthFile()`

- If it's not `ReadOnlyMode` and not full, open it as `Update` mode. Else, use the `Read` mode.
- Also fetches `framecount` (which is also in the `metadata`), `modelType` (why is this still here?), `metadata` dictionary, `paramDict`.
- These better be an another object?

### `ReadFromArchive()`
Fetch the `Depth` object from the given frame.

### `ReadParamsStr()`
There is a code that converts the legacy parameters (`MeshLoc`, ...) to the new ones.

### `ReadPgm()`
16bit PGM files are not supported.

## `DummyBehavior.cs`
For using Unity coroutines in non-`Monobehaviour`s.

## `GifPlayer.cs`

- When a gif is started decoding, it saves the start time to the variable `_decodingStartTime` and to the callback function.
- Upon decoded, the callback funtion checks if the given time is equal to `_decodingStartTime`. If it isn't, it means another gif has started being decoded. Then the decoded contents are discarded.
- This is because halting the decoding before it finishes creates a memory leak.
- All textures are decoded at once, and it calcuates the current frame using the time offset and returns the matching texture.

## `RecenterCameraBehavior.cs`
Sets the current VR camera rotation to the origin direction.

## `Utils.cs`

### `LoadImage()`
Converts image files to the texture.

### `GetHashval()`
Calculates the SHA-256 hash.

### `GetTimestamp()`
Returns the UNIX time.

### `CreateDirectory()`
Create the directory if it doesn't exist, does nothing if it exists.

### `GetDummyBehavior()`
Shouldn't this be in `UIStaticSetter.cs`?

### `ReadOptionsString()` and `WriteOptionsString()`
Read/Write the options strings that saves `searchCache` and `saveOutput`. It's just a text files with two lines. I believe this just should use `PlayerPrefs`

### `DepthToTex()`
Converts the `Depth` object into a texture.
- Shouldn't this be in `Depth`?

### `VRRecordBehavior.cs`
Saves the screen on the VR headset.