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
import traceback
import sys
from typing import Union, Iterable, Tuple

import numpy as np
import cv2
import torch

from midas.model_loader import default_models, load_model

VERSION = "v0.9.0-anaglyph.1"

class ModelParams():
	#this might as well just be a dictionary rather than a class

	def __init__(self, optimize=False, height=None, square=None):
		self.optimize = optimize #to half floats
		self.height = height #inference encoder image height
		self.square = square #resize to a square resolution?

	def __eq__(self, other):
		return (
			self.optimize == other.optimize 
			and self.height == other.height
			and self.square == other.square
		)

	def __str__(self):
		return f"{{'optimize'={self.optimize}, 'height'={self.height}, 'square'={self.square}}}" #pseudo-dictionary

class Runner():
	def framework_init(self, **kwargs):
		#To be called in __init__
		raise NotImplementedError()

	def load_model(self, model_type, **kwargs):
		#This should set self.model_type (and optionally self.model_params and self.depth_map_type (defaults to "Inverse"))
		raise NotImplementedError()

	def run_frame(self, img) -> np.ndarray:
		#Should be identical to `run_frames([img])[1][0]`
		raise NotImplementedError()
	
	def run_frames(self, imgs: Iterable, batch_size) -> Tuple[int, np.ndarray]:
		#Returns:
		# 	# of valid frames, the frames
		raise NotImplementedError()
	
	def __init__(self):
		print("Initialize")

		self.framework_init()

		self.framecount = 1 #for video
		self.framerate = 0

		self.model_params = self.model_type = None
		self.depth_map_type = "Inverse" #Or "Linear" or "Metric". Set in the subclasses.

	def model_exists(self, model_type) -> Union[str, None]:
		orig_cwd = os.getcwd()
		os.chdir(os.path.dirname(os.path.abspath(__file__)))

		if model_type in default_models:
			model_path = default_models[model_type]
		else:
			print(f"`{model_type}` does not exist in `default_models`...", end=" ")
			ext = ".pt" if "openvino_" not in model_type else ".xml"
			model_path = f"weights/{model_type}{ext}"
			print(f"Assuming {model_path}")

		if not os.path.exists(model_path):
			model_path = None
			
		os.chdir(orig_cwd)
		return model_path

	def run(self, inpath, outpath, isimage, zip_in_memory=True, update=True, batch_size=None) -> None:
		"""Run MonoDepthNN to compute depth maps.

		Args:
			inpath (str): input file.
			outpath (str): output directory.
			isvideo (bool): whether the input is a video.
			zip_in_memory (bool): If True, ZIP file will be created in the RAM until it finishes writing.
			batch_size (int | None): If valid integer, it will use `self.run_frames()`. Else, it will use `self.run_frame()`.
		"""

		print(f"Source: {inpath}")
		print(f"Destination: {outpath}")
		
		if not os.path.exists(inpath):
			print(f"ERROR: Could not find {inpath}")
			return

		#Get the generator
		if isimage:
			inputs = self.read_image(inpath)
		else:
			inputs = self.read_video(inpath)

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
		
		has_written_metadata = False
		frames_dict = {} #{pgmname: frame}, for batch inference
		prev = time.time()
		starttime = prev
		
		for i, img in enumerate(inputs):
			#Save width, height & the original size
			#Write the metadata
			if not has_written_metadata and not has_metadata:
				original_shape = img.shape[:2]

				#Pass dummy data for the metadata. The first input may takes some time.
				print("Passing a dummy data. This may take a second.")
				dummy = np.zeros_like(img)
				if batch_size is None:
					dummy = self.run_frame(img)
				else:
					_, dummy = self.run_frames([dummy], batch_size=batch_size)
					dummy = dummy[0] #Get the first one
				shape = dummy.shape
				
				self.save_metadata(zout=zout, inpath=inpath, shape=shape, original_shape=original_shape)
				has_written_metadata = True

			print("! On #{}".format(i)) #starts with 0

			pgmname = "{}.pgm".format(i)
			if update and pgmname in existing_filelist:
				print("Already exists.")
				continue
			
			#Using `run_frame()`
			if batch_size is None: 
				out_ndarray = self.run_frame(img)
				pgm = self.get_pgm(out_ndarray)
				zout.writestr(pgmname, pgm)

				now = time.time()
				fps_per_inf = 1 / (now - prev)
				print(f"Processed, fps: {1 / (now - prev) :.2f}")
				prev = now

			#Using `run_frames()` (Batch inference)
			else:
				##
				def process_frames_dict(frames_dict):
					#Process
					print(f"Processing: {list(frames_dict.keys())}")

					_, out_ndarrays = self.run_frames(frames_dict.values(), batch_size=batch_size)
					for pgmname, out_ndarray in zip(frames_dict.keys(), out_ndarrays):
						pgm = self.get_pgm(out_ndarray)
						zout.writestr(pgmname, pgm)
				##

				frames_dict[pgmname] = img
				if len(frames_dict) >= batch_size:
					shape = process_frames_dict(frames_dict)
					frames_dict = {} #Reset.

					now = time.time()
					fps_per_inf = 1 / (now - prev)
					print(f"Processed, fps: {fps_per_inf :.2f} * {batch_size} == {fps_per_inf * batch_size :.2f}")
					prev = now

		#(Batch inference): Process the remaining ones
		if batch_size is not None and frames_dict != {}: 
			shape = process_frames_dict(frames_dict)

		print(f"Took {time.time() - starttime :.2f}s")

		#ZipFile Close
		zout.close()
		
		if zip_in_memory:
			#Write the ZipFile from RAM & close the BytesIO buffer.
			while True:
				try:
					with open(outpath, "wb") as fout:
						fout.write(mem_buffer.getbuffer())
				except Exception as exc:
					traceback.print_exc()
					if input("PRESS 'r' TO RETRY: ").lower().startswith('r'):
						continue
					else:
						mem_buffer.close()
						raise exc #rethrow
				else:
					break
		
			mem_buffer.close()

	def save_metadata(self, zout, inpath, shape, original_shape):
		"""
		Args:
			zout (zipfile.ZipFile): obj to `.writestr()`
			inpath (str): the path of the input
		"""

		print("Saving the metadata.")

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
		original_framerate = self.framerate
		timestamp = int(time.time())
		version = VERSION
		program = "depthpy"

		model_type = self.model_type
		model_params = self.model_params
		depth_map_type = self.depth_map_type

		height, width = shape
		original_height, original_width = original_shape

		metadata = self.get_metadata(hashval=hashval, framecount=framecount, startframe=startframe, width=width, height=height, model_type=model_type, model_params=model_params, depth_map_type=depth_map_type, 
			original_name=original_name, original_width=original_width, original_height=original_height, original_framerate=original_framerate, timestamp=timestamp, program=program, version=version)
		zout.writestr("METADATA.txt", metadata, compresslevel=0)

	def normalize(self, image):
		#dtype = np.float32
		#image = image.astype(dtype)
		dtype = image.dtype

		depth_min = image.min()
		depth_max = image.max()

		if depth_max - depth_min > np.finfo("float").eps:
			normalized = (image - depth_min) / (depth_max - depth_min)				
		else:
			normalized = np.zeros(image.shape, dtype=dtype)

		return normalized
	
	def as_uint8(self, image):
		maxval = 255
		image *= maxval
		image = image.astype(np.uint8)

		return image

	def get_pgm(self, image) -> bytes:
		# 1byte per pixel

		image = self.as_uint8(image)
		if image.dtype != np.uint8: #This line is not needed now
			raise ValueError("Expecting np.uint8, received {}".format(image.dtype))

		height, width = image.shape[:2]

		return b"P5\n" + "{} {} {}\n".format(width, height, 255).encode("ascii") + image.tobytes()

	def get_metadata(self, hashval, framecount, startframe, width, height, model_type, model_params, depth_map_type, original_name, original_width, original_height, original_framerate, timestamp, program, version) -> str:

		metadata = '\n'.join([
			f"DEPTHVIEWER",
			f"hashval={hashval}",
			f"framecount={framecount}",
			f"startframe={startframe}",
			f"width={width}",
			f"height={height}",
			f"model_type={model_type}",
			f"model_type_val=0", #model_type_val is not used anymore
			f"model_params={model_params}",
			f"depth_map_type={depth_map_type}",
			f"original_name={original_name}",
			f"original_width={original_width}",
			f"original_height={original_height}",
			f"original_framerate={original_framerate}",
			f"timestamp={timestamp}",
			f"program={program}",
			f"version={version}",
		])
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
		self.framerate = float(cap.get(cv2.CAP_PROP_FPS))

		while cap.isOpened():
			ret, frame = cap.read()
			if not ret:
				print("Can't receive frame (stream end?). Exiting ...")
				break

			#Before `as_input()`, resize it
			maxsize = max(self.net_w, self.net_h)
			h, w = frame.shape[:2]
			if max(h, w) > maxsize:
				#...so that the shorter side's length equal `maxsize`
				if h < w: #horizontally long (most case)
					new_h, new_w = (maxsize, int(maxsize * (w / h)))
				else: #vertically long
					new_h, new_w = (int(maxsize * (h / w)), maxsize)
				frame = cv2.resize(frame, (new_w, new_h), interpolation=cv2.INTER_AREA)

			yield self.as_input(frame)

		cap.release()
		#cv2.destroyAllWindows()

		if buffer:
			buffer.close()

	def as_input(self, img):
		"""
		Set img for the input format
		as [0, 1]
		"""

		if img.ndim == 2:
			img = cv2.cvtColor(img, cv2.COLOR_GRAY2BGR)
		img = cv2.cvtColor(img, cv2.COLOR_BGR2RGB) / 255.0

		return img

	def read_image_bytes(self, bytestr: bytes):
		"""
		`bytestr` is a bytestring of jpg file
		"""
		img = np.frombuffer(bytestr, np.uint8)
		img = cv2.imdecode(img, cv2.IMREAD_UNCHANGED)
		img = self.as_input(img)
		return img
	
	def get_pfm(self, image, scale=1) -> bytes:
		#Modified from https://github.com/isl-org/MiDaS/blob/master/utils.py
		#This was originally from zoeserver.py

		assert len(image.shape) == 2

		image = image.astype(np.float32) #Convert the half-precision maps
		image = np.flipud(image)

		pfm = b""
		pfm += "Pf\n".encode("ascii")
		pfm += "%d %d\n".encode("ascii") % (image.shape[1], image.shape[0])

		endian = image.dtype.byteorder
		#print(image.dtype, image.dtype.byteorder, sys.byteorder)
		if endian == "<" or endian == "=" and sys.byteorder == "little":
			scale = -scale
		pfm += "%f\n".encode("ascii") % scale

		pfm += image.tobytes()

		return pfm
	
class PyTorchRunner(Runner):
	def framework_init(self):
		# set torch options
		torch.backends.cudnn.enabled = True
		torch.backends.cudnn.benchmark = True

		# select device
		self.device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
		print("device: %s" % self.device)

	def load_model(self, model_type="dpt_beit_large_512", optimize=False, height=None, square=None):
		new_model_params = ModelParams(optimize=optimize, height=height, square=square)

		#check if the model exists
		model_path = self.model_exists(model_type)
		if not model_path:
			raise ValueError(f"Model not found: {model_type}")

		#check if it's the already loaded
		if self.model_type == model_type and self.model_params == new_model_params:
			return

		print(f"Loading model {model_type}...")
		print(new_model_params)

		orig_cwd = os.getcwd()
		os.chdir(os.path.dirname(os.path.abspath(__file__)))
		self.model, self.transform, self.net_w, self.net_h = load_model(self.device, model_path, model_type, optimize, height, square)
		os.chdir(orig_cwd)

		print("Loaded the model.")

		self.model_type = model_type
		self.model_params = new_model_params

	def run_frame(self, img):
		#Should be identical to `return run_frames([img])[1][0]`. Left for compability

		# input
		img_input = self.transform({"image": img})["image"]

		# compute
		with torch.no_grad():
			if "openvino" in self.model_type:
				#not tested
				sample = [np.reshape(img_input, (1, 3, self.net_w, self.net_h))]
				prediction = self.model(sample)[self.model.output(0)][0]
			else:
				sample = torch.from_numpy(img_input).to(self.device).unsqueeze(0)
				if self.model_params.optimize == True and self.device == torch.device("cuda"):
					sample = sample.to(memory_format=torch.channels_last)  
					sample = sample.half()
				prediction = self.model.forward(sample)
				prediction = prediction.squeeze().cpu().numpy()

		# output
		out = self.normalize(prediction)
		return out
	
	def run_frames(self, imgs: Iterable, batch_size) -> Tuple[int, np.ndarray]:
		#Returns the number of valid frames and an ndarray of shape (batch_size, ...)

		#Stack
		frames = []
		for i, img in enumerate(imgs):
			if i >= batch_size:
				break

			frame = self.transform({"image": img})["image"]
			frames.append(frame)

		#Empty frames
		if frames == []:
			return 0, None
		
		#Add dummy frames
		empty = batch_size - len(frames)
		for _ in range(empty):
			frames.append(np.zeros_like(frames[0]))

		frames = np.stack(frames)

		#Compute
		with torch.no_grad():
			if "openvino" in self.model_type:
				#not tested
				#sample = [np.reshape(frames, (-1, 3, self.net_w, self.net_h))]
				#print(f"sample.shape: {sample[0].shape}")
				#prediction = self.model(sample)[self.model.output(0)]
				raise NotImplementedError("Batch inference on OpenVINO models is not implemented yet (please make a GitHub issue)")
			else:
				sample = torch.from_numpy(frames).to(self.device)
				if self.model_params.optimize == True and self.device == torch.device("cuda"):
					sample = sample.to(memory_format=torch.channels_last)
					sample = sample.half()
				prediction = self.model.forward(sample)
				prediction = prediction.cpu().numpy()

		#Normalize, per frame
		for i in range(batch_size):
			prediction[i] = self.normalize(prediction[i])

		return (batch_size - empty), prediction
	
def add_runner_argparser(parser):
	parser.add_argument('-t', '--model_type',
		default="dpt_hybrid_384",
		help="model type",
	)

	parser.add_argument("--optimize",
			help="Use the half-precision float. (Use with caution, because models like Swin require float precision to work properly and may yield non-finite depth values to some extent for half-floats.)",
			action="store_true"
		)
	parser.add_argument('--height',
		type=int, default=None,
		help='Preferred height of images feed into the encoder during inference. Note that the '
		'preferred height may differ from the actual height, because an alignment to multiples of '
		'32 takes place. Many models support only the height chosen during training, which is '
		'used automatically if this parameter is not set.'
	)
	parser.add_argument('--square',
		action='store_true',
		help='Option to resize images to a square resolution by changing their widths when images are '
		'fed into the encoder during inference. If this parameter is not set, the aspect ratio of '
		'images is tried to be preserved if supported by the model.'
	)

	parser.add_argument("-r", "--runner",
		default="pt",
		help="`pt`: PyTorch (default)\n"
			"`ort`: Use OnnxRuntime. Options like `--optimize`, `--height`, `--square` will be ignored.\n"
			"`zoe`: Use ZoeDepth (w/ PyTorch)"
	)
	parser.add_argument("--ort_ep",
		default="cpu",
		help="Execution provider to use with ORT.",
		choices=["cpu", "cuda", "dml"],
	)

def get_loaded_runner(args):
	model_type = args.model_type

	if args.runner == "pt":
		runner = PyTorchRunner()
		runner.load_model(model_type=model_type, optimize=args.optimize, height=args.height, square=args.square)
	elif args.runner == "ort":
		from ortrunner import OrtRunner
		runner = OrtRunner()
		runner.load_model(model_type=model_type, provider=args.ort_ep)
	elif args.runner == "zoe":
		from zoerunner import ZoeRunner
		runner = ZoeRunner()
		runner.load_model(model_type=model_type, height=args.height)
	else:
		raise ValueError(f"Unknwon runner {args.runner}")
	
	return runner

#######################

if __name__ == "__main__":
	try:
		parser = argparse.ArgumentParser()

		parser.add_argument('input',
			help='input file'
		)

		parser.add_argument("output",
			help="Output path & filename",
		)

		parser.add_argument("-i", "--image",
			help="Assume an image input.",
			action="store_true"
		)

		parser.add_argument("--zip_in_memory",
			help="Whether zip the file in RAM and dump on the disk only after it finishes.",
			action="store_true"
		)

		parser.add_argument("--noupdate",
			help="Replace existing file.",
			action="store_true"
		)

		parser.add_argument("--batch_size",
			help="Batch size (experimental)",
			type=int,
			default=None,
		)

		add_runner_argparser(parser)

		args = parser.parse_args()

		print(f"input: {args.input}")
		print(f"output: {args.output}")

		print(f"batch_size: {args.batch_size}")

		#Check if the input is of image ext but (not args.image)
		if any(map(args.input.endswith, [".jpg", ".png"])) and not args.image:
			print("Warning: input has an image ext but `-i` was not given.")

		if not args.noupdate and args.image and os.path.exists(args.output):
			print(f"Image: already exists: {args.output}. Use --noupdate to replace it.")
			exit(0)

		runner = get_loaded_runner(args)
		outs = runner.run(inpath=args.input, outpath=args.output, isimage=args.image, zip_in_memory=args.zip_in_memory, update=not args.noupdate,
			batch_size=args.batch_size)

		print("Done.")
	except Exception as exc:
		print("EXCEPTION:")
		traceback.print_exc()

		print('*'*32)
		print("PRESS ENTER TO CONTINUE.")
		input()