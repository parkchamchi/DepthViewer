import os
import shutil

builddir = "../../../Build"

print("#1. Deleting the noshipdir")

for filename in os.listdir(builddir):
	if filename.endswith("DoNotShip"):
		filename = os.path.join(builddir, filename)
		print(f"Deleting {filename}")
		shutil.rmtree(filename)

print()

print("#2. Locating the README.txt")

readmetxtpath = "../../Assets/Assets/README.txt"
print(f"Copying {readmetxtpath}")
shutil.copyfile(readmetxtpath, os.path.join(builddir, "README.txt"))

print()

print("#3. Making the onnx directory")

onnxdir = os.path.join(builddir, "onnx")
print(f"Creating {onnxdir}")
os.mkdir(onnxdir)

onnxdir_readme = os.path.join(onnxdir, "put onnx files here.txt")
print(f"Creating {onnxdir_readme}")
with open(onnxdir_readme, "wt") as fout:
	fout.write('\n'.join([
		"MiDaS v2.1 384 model (model-f6b98070.onnx) is here: https://github.com/isl-org/MiDaS/releases/tag/v2_1",
		"See the GitHub Repo https://github.com/parkchamchi/DepthViewer#models for more.",
	]))

print()

print("#4. Locating the depthpy")

depthpydir = os.path.join(builddir, "depthpy")
os.mkdir(depthpydir)

#move depth.py & depthserver.py

depthpy_files = [
	"depth.py",
	"depthmq.py",
	"ffpymq.py",
	"mqpy.py"
	"ortrunner.py",
	"zoerunner.py"
]
for filename in depthpy_files:
	print(f"Copying {filename}")
	shutil.copyfile(os.path.join("..", filename), os.path.join(depthpydir, filename))

#makedir midas
print("Making the midas directory")
midasdir = os.path.join(depthpydir, "midas")
os.mkdir(midasdir)
os.mkdir(os.path.join(midasdir, "backbones"))

#copy the midas
#might as well just glob "*.py" and "LICENSE"... later
midasfiles = [
	"base_model.py",
	"blocks.py",
	"dpt_depth.py",
	"midas_net_custom.py",
	"midas_net.py",
	"model_loader.py",
	"transforms.py",
	"LICENSE",

	"backbones/beit.py",
	"backbones/levit.py",
	"backbones/next_vit.py",
	"backbones/swin.py",
	"backbones/swin2.py",
	"backbones/swin_common.py",
	"backbones/utils.py",
	"backbones/vit.py",
]
for midasfile in midasfiles:
	fromfile = os.path.join("../midas", midasfile)
	tofile = os.path.join(midasdir, midasfile)
	print(f"From {fromfile} to {tofile}")

	shutil.copyfile(fromfile, tofile)

#makedir weights (empty)
weightsdir = os.path.join(depthpydir, "weights")
print(f"Creating {weightsdir}")
os.mkdir(weightsdir)

print()

print("Done.")