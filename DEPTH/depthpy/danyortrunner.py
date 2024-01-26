"""
A modification of
	https://github.com/fabio-sim/Depth-Anything-ONNX/blob/main/infer.py
	(Apache License 2.0)
"""

from depth import Runner

import cv2
import numpy as np
import onnxruntime as rt
from torchvision.transforms import Compose

#See `danyrunner.py`
import os
import sys
sys.path.append(os.path.join(
	os.path.dirname(os.path.abspath(__file__)),
	"dany/"
))
from dany.depth_anything.util.transform import Resize, NormalizeImage, PrepareForNet

class DanyOrtRunner(Runner):
	def framework_init(self):
		pass

	def load_model(self, model_type="vitl14", provider="cuda", **kwargs):
		print(f"OrtRunner: using provider {provider}")
		if provider == "cpu":
			providers = ["CPUExecutionProvider"]
		elif provider == "cuda":
			providers = ["CUDAExecutionProvider"]	
		elif provider == "dml":
			providers = ["DmlExecutionProvider"]
		else:
			print(f"DanyOnnxRunner.load_model(): Unknown provider {provider}. Falling back to CPU.")
			providers = ["CPUExecutionProvider"]

		filename = os.path.join("../onnx", f"depth_anything_{model_type}.onnx")
		print(f"Trying to load {filename}...")

		orig_cwd = os.getcwd()
		os.chdir(os.path.dirname(os.path.abspath(__file__)))
		self.infsession = rt.InferenceSession(filename, providers=providers)
		os.chdir(orig_cwd)

		self.net_w, self.net_h = 518, 518

		self.transform = Compose([
			Resize(
				width=self.net_w,
				height=self.net_h,
				resize_target=False,
				keep_aspect_ratio=False, #Note: was `True` on `dany`
				ensure_multiple_of=14,
				resize_method='lower_bound',
				image_interpolation_method=cv2.INTER_CUBIC,
			),
			NormalizeImage(mean=[0.485, 0.456, 0.406], std=[0.229, 0.224, 0.225]),
			PrepareForNet(),
		])

		print("Loaded the model.")

		self.model_type = model_type

	def run_frame(self, img):
		# input
		img_input = self.transform({"image": img})["image"] # C, H, W
		img_input = img_input[None]  # B, C, H, W

		# compute
		depth = self.infsession.run(None, {"image": img_input})[0]
		depth = depth.squeeze()

		# output
		out = self.normalize(depth)
		return out