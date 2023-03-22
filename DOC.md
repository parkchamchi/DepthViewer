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

...