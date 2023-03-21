# Temporary technical memos

Written reviewing the version [v0.8.8-beta-prerelease-2](https://github.com/parkchamchi/DepthViewer/releases/tag/v0.8.8-beta-prerelease-2) <br>
Everything is subject to the change. <br>
**WIP**

## General
- Android and WebGL builds has not been tested beyond the version `v0.6.2`
- The main engine is `TexInputs`. It fetches the input (e.g. a `jpg` file to a Texture object) then infers the depth using the inferer `DepthModel`. Then it updates the mesh `MeshBehavior`.
When this cycle is finished (not parallelized), the `MainBehavior` calls `TexInputs.UpdateTex()`, repeating the cycle.


## MainBehavior.cs
[code](https://github.com/parkchamchi/DepthViewer/blob/master/DEPTH/Assets/Scripts/MainBehavior.cs) <br>
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

### Start()
[#](https://github.com/parkchamchi/DepthViewer/blob/1e6ca57dd9e1c0233fa1fe1bcf2cb8007f79c8ac/DEPTH/Assets/Scripts/MainBehavior.cs#L73)

#### First we find the MonoBehaviours to be used.
- `MeshBehavior`: the object the depth values will be visualized
- `DepthModelBehavior`: the depth map inference engine
- `VRRecordBehavior`: this is for recording the screen on VR headsets.
- `ServerConnectBehavior`: this is to connect to a server that provides the depth map when an image is given (`AsyncDepthModel`).
It is only implemented for the image inputs and it usage is not recommended.
Not to be confused with `HttpOnlineTex`, which fetches *images* from the server.

#### Then we find the `VideoPlayer` and assign it to `_vp`.
But it seems it can be removed here, since `_vp` is only for used for being destroying on the termination of the program (which can be just fetched then) and to be used for an argument for `ImgVidDepthTexInputs`.
If there were several meshes and different `VideoPlayer`s it would make sense, but as it don't seem to be implemented, I think this can be omitted here and have it fetched on `ImgVidDepthTexInputs`.

#### Calling ToggleOutputSave(), ToggleSearchCache()
Initialize the `OutputSave` and `SearchCache` toggles. Well, sort of. It's actually to set the variables `_canUpdateArchive` and `_searchCache` based on the toggle UI's values (i.e. the value that was set on the editor)
Can these variables just be replaced with `OutputSaveToggle.isOn` and `SearchCacheToggle.isOn`?

#### Command line arguments
This code is outdated and would only work for image and video inputs, if there's any usage for this.

#### The keys to be sent to _texInputs
If the keys in `_sendMsgKeyCodes` is pressed, it will be sent to the active `_texInputs` (that is processing the input). For example, if `Keypad5` is pressed, it will inform the `ImgVidDepthTexInputs` and will pause the video.

...