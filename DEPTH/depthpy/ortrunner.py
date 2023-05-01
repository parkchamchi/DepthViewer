from depth import Runner

from midas.transforms import Resize, NormalizeImage, PrepareForNet

from torchvision.transforms import Compose
import cv2
import numpy as np
import onnxruntime as rt

import os

class OrtRunner(Runner):
	def framework_init(self):
		pass
		
	def load_model(self, model_type, provider="cuda"):
		if provider == "cpu":
			providers = ["CPUExecutionProvider"]
		elif provider == "cuda":
			providers = ["CUDAExecutionProvider"]	
		elif provider == "dml":
			providers = ["DmlExecutionProvider"]
		else:
			print(f"OrtRunner.__init__(): Unknown provider {provider}. Falling back to CPU.")
			providers = ["CPUExecutionProvider"]

		filename = os.path.join("../onnx", f"{model_type}.onnx")
		print(f"Trying to load {filename}...")

		orig_cwd = os.getcwd()
		os.chdir(os.path.dirname(os.path.abspath(__file__)))
		self.infsession = rt.InferenceSession(filename, providers=providers)
		os.chdir(orig_cwd)

		self.input_name = self.infsession.get_inputs()[0].name
		self.output_name = self.infsession.get_outputs()[0].name

		self.net_w = int(model_type[model_type.rfind('_')+1:])
		self.net_h = self.net_w
		print(f"Assuming {self.net_w}x{self.net_h}...")

		self.transform = self.get_transform(model_type, self.net_w, self.net_h)
		self.model_type = model_type

	def run_frame(self, img):
		img_input = self.transform({"image": img})["image"]

		output = self.infsession.run([self.output_name], {self.input_name: img_input.reshape(1, 3, self.net_h, self.net_w).astype(np.float32)})[0]
		output = output[0]

		output = self.normalize(output)
		return output

	def get_transform(self, model_type, net_w, net_h):
		if "model-" not in model_type: #not v2.1
			#From model_loader.py
			transform = Compose(
				[
					Resize(
						net_w,
						net_h,
						resize_target=None,
						keep_aspect_ratio=False,
						ensure_multiple_of=32,
						resize_method="minimal",
						image_interpolation_method=cv2.INTER_CUBIC,
					),
					NormalizeImage(mean=[0.5, 0.5, 0.5], std=[0.5, 0.5, 0.5]),
					PrepareForNet(),
				]
			)

		else:
			#From run_onnx.py

			def compose2(f1, f2):
				return lambda x: f2(f1(x))

			resize_image = Resize(
				net_w,
				net_h,
				resize_target=None,
				keep_aspect_ratio=False,
				ensure_multiple_of=32,
				resize_method="upper_bound",
				image_interpolation_method=cv2.INTER_CUBIC,
			)

			transform = compose2(resize_image, PrepareForNet())

		return transform