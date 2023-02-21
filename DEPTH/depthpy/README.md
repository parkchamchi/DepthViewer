### Using depth.py and depthserver.py (OPTIONAL)
#### This is not needed anymore, just use the onnx files...
#### Note that this uses PyTorch not OnnxRuntime

`depth.py` is for generating `.depthviewer` files so that it can be opened with the DepthViewer.<br>

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
`python depth.py --help` <br>
and see if it prints the manual without any error.
7. Get `dpt_beit_large_512` model (and others) from [here](https://github.com/isl-org/MiDaS#setup) and locate them in `depthpy/weights`. Do not change the filenames.
(Other models can be loaded by adding the `-t` argument, see `--help` for more.)
8. Place any image in the `depthpy` directory, rename it to `test.jpg` (or `test.png`) and run <br>
`python depth.py test.jpg out.depthviewer -i` <br>
See if it generates an output. Also check if `depth.py` is using CUDA by checking `device: cuda` line.

#### If it isn't and you want to use CUDA:
- Check the installed CUDA version and if the installed Pytorch version supports that.
- Uninstall Pytorch `pip uninstall torch torchvision` and reinstall it.

#### For depthserver.py
- Install Flask `pip install Flask`
- Run `python depthserver.py` to open the server and connect to it via the option menu. If it's connected all image inputs will be processed by calling the server.