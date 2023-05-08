from depth import Runner

import torch
from torchvision.transforms import ToTensor
import cv2
import numpy as np

default_max_height = 512

class ZoeRunner(Runner):
	def framework_init(self):
		self.device = "cuda" if torch.cuda.is_available() else "cpu"
		print(f"device: {self.device}")

	def load_model(self, model_type="ZoeD_N", height=default_max_height, **kwargs):
		"""
		Args:
			height (int): the max height of the output
		"""

		repo = "isl-org/ZoeDepth"
		model = torch.hub.load(repo, model_type, pretrained=True)
		model.eval()
		model.to(self.device)
		self.model = model

		self.depth_map_type = "Metric"

		self.height = height if height is not None else default_max_height
		print(f"Resize all outputs w/ `max_height`={self.height}")

	def run_frame(self, img):
		img = img.astype(np.float32)
		img = ToTensor()(img).unsqueeze(0).to(self.device) #np.ndarray -> torch.Tensor
		depth = self.model.infer(img) #Infer
		depth = depth.detach().squeeze().cpu().numpy() #torch.Tensor -> np.ndarray

		h, w = depth.shape
		if (h > self.height):
			newshape = (int((w/h) * self.height), self.height)
			depth = cv2.resize(depth, newshape, interpolation=cv2.INTER_AREA)

		return depth