# DepthViewer
Using [MiDaS Deep Learning Model](https://github.com/isl-org/MiDaS), converts 2D videos/images into 3D object with Unity for VR.

## Examples
| Original input (resized) | v2.1 small (built in) | dpt-large model | Source |
| --- | --- | --- | --- |
| ![landscape_orig](./examples/landscape_orig.jpg) | ![landscape_small](./examples/landscape_small.jpg) | ![landscape_large](./examples/landscape_large.jpg) | [#](https://commons.wikimedia.org/wiki/File:%D0%9F%D0%B0%D0%BD%D0%BE%D1%80%D0%B0%D0%BC%D0%B0_%D0%86%D0%BD%D1%82%D0%B5%D0%B3%D1%80%D0%B0%D0%BB%D1%83.jpg) |
| ![boat_orig](./examples/boat_orig.jpg) | ![boat_small](./examples/boat_small.jpg) | ![boat_large](./examples/boat_large.jpg) | [#](https://commons.wikimedia.org/wiki/File:Escale_%C3%A0_S%C3%A8te_2022_D.jpg) |
| ![cat_orig](./examples/cat_orig.gif) | ![cat_small](./examples/cat_small.gif) | ![cat_large](./examples/cat_large.gif) | [#](https://commons.wikimedia.org/wiki/File:Cat_kneading_blanket.gk.webm) |
Note: Angles are not identical.

## Models
The built-in model is [MiDaS v2.1 small model](https://github.com/isl-org/MiDaS/releases/tag/v2_1), which is ideal for real-time rendering.

### Call python
The [MiDaS v3 DPT models](https://github.com/isl-org/MiDaS), which I found to be exceptionally accurate, hasn't been released as ONNX model that can be used with Unity's Barracuda.
The `Call Python` buttons will call python subprocess and process it with pytorch. 
For now it just calls `python ../depthpy/depth.py [args]...`, so dependency for MiDaS should be installed manually, for that check [MiDaS github page](https://github.com/isl-org/MiDaS). 
For this [dpt_hybrid and dpt_large .pt model files](https://github.com/isl-org/MiDaS#setup) has to be in `depthpy/weights` directory.

## Inputs
- Right mouse key: hides the UI.
- WASD: rotate the mesh. (direction bugs right now)

## Notes
- This program is not optimized yet.
- If VR HMD is detected, it will open with OpenXR.
- All outputs will be cached to Application.persistentDataPath (In Windows, `...\AppData\LocalLow\parkchamchi\DepthViewer`).
- Depth files this program creates are of extention `.depthviewer`, which is a zip file with .pgm files and a metadata file.
- C# scripts are in [DEPTH/Assets/Scripts](DEPTH/Assets/Scripts).

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
- Fix codecs
- Open depth map image of .jpg or .png (.depthviewer file itself too)
- Stablize & Parallelize

## Misc
- Font used: [Noto Sans KR](https://fonts.google.com/noto/specimen/Noto+Sans+KR) (SIL Open Font License)
