"""
Modified from:
	https://github.com/isl-org/MiDaS/blob/master/run.py
	https://github.com/isl-org/MiDaS/blob/master/utils.py
	(MIT License, see ./midas/LICENSE)
"""

import os
import argparse
import zipfile
import io
import time
import hashlib

import numpy as np
import cv2
import torch
from torchvision.transforms import Compose

from midas.dpt_depth import DPTDepthModel
from midas.midas_net import MidasNet
from midas.midas_net_custom import MidasNet_small
from midas.transforms import Resize, NormalizeImage, PrepareForNet

VERSION = "v0.3-alpha"

class Runner():
	
	def __init__(self, model_path=None):
		#path to model and model_type_val -- see DepthFileUtils.cs for explanation
		self.default_models = { 
			"MidasV21Small": ("midas_v21_small-70d6b9c8.pt", 100),
			"MiDasV21": ("midas_v21-f6b98070.pt", 200),
			"MidasV3DptHybrid": ("dpt_hybrid-midas-501f0c75.pt", 300),
			"MidasV3DptLarge": ("dpt_large-midas-2f21e586.pt", 400),
		}
		
		if model_path:
			self.model_path = model_path
		else:
			self.model_path = os.path.join(os.path.abspath(os.path.dirname(__file__)), "weights")

		# set torch options
		torch.backends.cudnn.enabled = True
		torch.backends.cudnn.benchmark = True

		print("initialize")

		# select device
		self.device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
		print("device: %s" % self.device)

		self.framecount = 1 #for video

	def run(self, inpath, outpath, isvideo, model_type="dpt_hybrid", optimize=True, zip_in_memory=True, update=True) -> None:
		"""Run MonoDepthNN to compute depth maps.

		Args:
			inpath (str): input file.
			outpath (str): output directory.
			isvideo (bool): whether the input is a video.
			zip_in_memory (bool): If True, ZIP file will be created in the RAM until it finishes writing.
		"""

		print("Destination: {}".format(outpath))
		print("Loading model {}...".format(model_type))
		
		model_weight_path = os.path.join(self.model_path, self.default_models[model_type][0])
		model_type_val = self.default_models[model_type][1]

		if not os.path.exists(inpath):
			print("ERROR: Could not find {}".format(inpath))
			return

		#Get the generator
		if isvideo:
			inputs = self.read_video(inpath)
		else:
			inputs = self.read_image(inpath)

		# load network
		if model_type == "MidasV3DptLarge": # DPT-Large
			model = DPTDepthModel(
				path=model_weight_path,
				backbone="vitl16_384",
				non_negative=True,
			)
			net_w, net_h = 384, 384
			resize_mode = "minimal"
			normalization = NormalizeImage(mean=[0.5, 0.5, 0.5], std=[0.5, 0.5, 0.5])
		elif model_type == "MidasV3DptHybrid": #DPT-Hybrid
			model = DPTDepthModel(
				path=model_weight_path,
				backbone="vitb_rn50_384",
				non_negative=True,
			)
			net_w, net_h = 384, 384
			resize_mode="minimal"
			normalization = NormalizeImage(mean=[0.5, 0.5, 0.5], std=[0.5, 0.5, 0.5])
		elif model_type == "MiDasV21":
			model = MidasNet(model_weight_path, non_negative=True)
			net_w, net_h = 384, 384
			resize_mode="upper_bound"
			normalization = NormalizeImage(
				mean=[0.485, 0.456, 0.406], std=[0.229, 0.224, 0.225]
			)
		elif model_type == "MidasV21Small":
			model = MidasNet_small(model_weight_path, features=64, backbone="efficientnet_lite3", exportable=True, non_negative=True, blocks={'expand': True})
			net_w, net_h = 256, 256
			resize_mode="upper_bound"
			normalization = NormalizeImage(
				mean=[0.485, 0.456, 0.406], std=[0.229, 0.224, 0.225]
			)
		else:
			print(f"model_type '{model_type}' not implemented, use: --model_type large")
			assert False
		
		transform = Compose(
			[
				Resize(
					net_w,
					net_h,
					resize_target=None,
					keep_aspect_ratio=True,
					ensure_multiple_of=32,
					resize_method=resize_mode,
					image_interpolation_method=cv2.INTER_CUBIC,
				),
				normalization,
				PrepareForNet(),
			]
		)

		print("Loaded the model.")

		model.eval()
		
		if optimize==True:
			if self.device == torch.device("cuda"):
				model = model.to(memory_format=torch.channels_last)  
				model = model.half()

		model.to(self.device)

		#Prepare the zipfile
		if zip_in_memory:
			if update and os.path.exists(outpath):
				with open(outpath, "rb") as fin:
					mem_buffer = io.BytesIO(fin.read())
			else:
				mem_buffer = io.BytesIO()
		else:
			mem_buffer = outpath

		zipfilemode = "a" if update else "w"
		zout = zipfile.ZipFile(mem_buffer, zipfilemode, compression=zipfile.ZIP_DEFLATED, compresslevel=5)
		if update:
			existing_filelist = zout.namelist()
			has_metadata = "METADATA.txt" in existing_filelist
		else:
			has_metadata = False
		
		width = height = 0 #for metadata
		i = 0
		for img in inputs:
			print("! Processing #{}".format(i)) #starts with 0

			pgmname = "{}.pgm".format(i)
			if update and pgmname in existing_filelist:
				print("Already exists.")
				i += 1
				continue
			
			# input
			img_input = transform({"image": img})["image"]

			# compute
			with torch.no_grad():
				sample = torch.from_numpy(img_input).to(self.device).unsqueeze(0)
				if optimize==True and self.device == torch.device("cuda"):
					sample = sample.to(memory_format=torch.channels_last)  
					sample = sample.half()
				prediction = model.forward(sample)
				prediction = prediction.squeeze().cpu().numpy()

			# output
			out = self.normalize(prediction)
			zout.writestr(pgmname, self.get_pgm(out))

			#save width, height & the original size
			#Write the metadata
			if i == 0 and not has_metadata:
				height, width = out.shape[:2]
				original_height, original_width = img.shape[:2]
				
				sha256 = hashlib.sha256()
				with open(inpath, "rb") as fin:
					while True:
						datablock = fin.read(128*1024) #buffer it
						if not datablock:
							break
						sha256.update(datablock)
				hashval = sha256.hexdigest()

				startframe = -1 #since we can't check this is opencv, set it a negative value

				original_name = os.path.basename(inpath)
				framecount = self.framecount
				timestamp = int(time.time())
				version = VERSION

				metadata = self.get_metadata(hashval=hashval, framecount=framecount, startframe=startframe, width=width, height=height, model_type=model_type, model_type_val=model_type_val, original_name=original_name, original_width=original_width, original_height=original_height, timestamp=timestamp, version=version)
				zout.writestr("METADATA.txt", metadata, compresslevel=0)

			i += 1

		#ZipFile Close
		zout.close()
		
		if zip_in_memory:
			#Write the ZipFile from RAM & close the BytesIO buffer.
			with open(outpath, "wb") as fout:
				fout.write(mem_buffer.getbuffer())
			mem_buffer.close()

	def normalize(self, image):
		maxval = 255
		dtype = np.uint8

		image = image.astype(np.float32)

		depth_min = image.min()
		depth_max = image.max()

		if depth_max - depth_min > np.finfo("float").eps:
			normalized = maxval * (image - depth_min) / (depth_max - depth_min)
			normalized = normalized.astype(dtype)
		else:
			normalized = np.zeros(image.shape, dtype=dtype)

		return normalized

	def get_pgm(self, image) -> bytes:
		# 1byte per pixel

		if image.dtype != np.uint8:
			raise ValueError("Expecting np.uint8, received {}".format(image.dtype))

		height, width = image.shape[:2]

		return b"P5\n" + "{} {} {}\n".format(width, height, 255).encode("ascii") + image.tobytes()

	def get_metadata(self, hashval, framecount, startframe, width, height, model_type, model_type_val, original_name, original_width, original_height, timestamp, version) -> str:

		
		metadata = (
f"""DEPTHVIEWER
hashval={hashval}
framecount={framecount}
startframe={startframe}
width={width}
height={height}
model_type={model_type}
model_type_val={model_type_val}
original_name={original_name}
original_width={original_width}
original_height={original_height}
timestamp={timestamp}
version={version}
""")
		return metadata

	def read_image(self, path):
		"""
		Read an image and return a list for the iterator for self.run()
		File should not be just .imread()'ed since it does not support unicode
		"""

	
		img = cv2.imread(path)
		if img is None:
			print("Error: could not open. This may occur when the path is non-ascii. Trying the other method...")
			img = cv2.imdecode(np.fromfile(path, np.uint8), cv2.IMREAD_UNCHANGED)
			if img is None:
				raise ValueError("Could not open the image.")
			print("Success")
		img = self.as_input(img)

		return [img]

	def read_video(self, path):
		"""
		Read a video and make a generator for self.run()
		"""

		buffer = None

		try:
			cap = cv2.VideoCapture(path)
		except:
			print("Error: could not open. This may occur when the path is non-ascii. Trying to load it to RAM...")
			buffer = io.BytesIO()
			with open(path, "rb") as fin:
				buffer = fin.read()
			cap = cv2.VideoCapture(buffer)

		self.framecount = int(cap.get(cv2.CAP_PROP_FRAME_COUNT))

		while cap.isOpened():
			ret, frame = cap.read()
			if not ret:
				print("Can't receive frame (stream end?). Exiting ...")
				break

			yield self.as_input(frame)

		cap.release()
		cv2.destroyAllWindows()

		if buffer:
			buffer.close()

	def as_input(self, img):
		"""
		Set img for the input format
		"""

		if img.ndim == 2:
			img = cv2.cvtColor(img, cv2.COLOR_GRAY2BGR)
		img = cv2.cvtColor(img, cv2.COLOR_BGR2RGB) / 255.0

		return img

	def check_ascii_string(self, string):
		"""
		Returns true is string is comprised of ASCII, otherwise false.
		Not used
		"""

		return all(c <= 127 for c in string)


#######################

if __name__ == "__main__":
	parser = argparse.ArgumentParser()

	parser.add_argument('input',
		help='input file'
	)

	parser.add_argument("output",
		help="Output path & filename",
	)

	parser.add_argument("-v", "--video",
		help="Assume an video input.",
		action="store_true"
	)

	parser.add_argument('-t', '--model_type',
		default="dpt_large",
		help="model type",
		choices=["MidasV3DptLarge", "MidasV3DptHybrid", "MiDasV21", "MidasV21Small"]
	)

	parser.add_argument("--zip_in_memory",
		help="Whether zip the file in RAM and dump on the disk only after it finishes.",
		action="store_true"
	)

	parser.add_argument("-u", "--update",
		help="If output file already exists, update it.",
		action="store_true"
	)

	args = parser.parse_args()

	print(f"input: {args.input}")
	print(f"output: {args.output}")

	if args.update and not args.video and os.path.exists(input):
		print("Image: already exists.")
		exit(0)

	runner = Runner()

	try:
		outs = runner.run(inpath=args.input, outpath=args.output, isvideo=args.video, model_type=args.model_type, zip_in_memory=args.zip_in_memory, update=args.update)

		print("Done.")
	except Exception as exc:
		print("EXCEPTION:")
		print(exc)

		input("press enter to continue\n")