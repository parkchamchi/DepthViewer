# DepthViewer
Converts 2D videos/images into 3d object.
Uses [MiDaS Deep Learning Model](https://github.com/isl-org/MiDaS).

## Examples

## Models
The built-in model is [MiDaS v2.1 small model](https://github.com/isl-org/MiDaS/releases/tag/v2_1), which is ideal for real-time rendering.

### Call python
The [MiDaS v3 DPT models](https://github.com/isl-org/MiDaS), which I found to be exceptionally accurate, hasn't been released as ONNX model that can be used with Unity's Barracuda.
The `Call Python` buttons will call python subprocess and process it with pytorch. 
For now it just calls `python ../depthpy/depth.py [args]...`, so dependency for MiDaS should be installed manually, for that check [MiDaS github page](https://github.com/isl-org/MiDaS). (Will be packaged later)
For this [dpt_hybrid and dpt_large .pt model files](https://github.com/isl-org/MiDaS#setup) has to be in `depthpy/weights` directory.

## Inputs
- Right mouse key: hides the UI.
- WASD: rotate the mesh. (direction bugs right now)

## Notes
- This program is not optimized yet.
- All outputs will be cached to Application.persistentDataPath (In Windows, `...\AppData\LocalLow\parkchamchi\DepthViewer`).
- Depth files this program creates are of extention `.depthviewer`, which is a zip file with .pgm files and a metadata file.

## Tested formats:
### Images
- .jpg
- .png

### Videos
- .mp4
Some mp4 files do not play because Unity can't open them. \
Other formats has not been tested.

## Todo
- Overhaul UI & Control, add features such as file browsing
- Add options, such as not saving the output, camera/depth parameters, ...
- Stablize and parallelize

## License
- Font used: [Noto Sans KR](https://fonts.google.com/noto/specimen/Noto+Sans+KR) (SIL Open Font License)
MIT License.