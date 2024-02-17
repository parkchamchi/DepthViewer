"""
A modification of
	https://github.com/LiheYoung/Depth-Anything/blob/cd9421bf682692408a8f9699041f744eee756e8a/run.py
	(Apache License 2.0)

> git clone https://github.com/LiheYoung/Depth-Anything
> rename Depth-Anything dany
"""

from depth import Runner

import cv2
import torch
from torchvision.transforms import Compose

import os
import sys
sys.path.append(os.path.join(
	os.path.dirname(os.path.abspath(__file__)),
	"dany/"
))
from dany.depth_anything.dpt import DepthAnything
from dany.depth_anything.util.transform import Resize, NormalizeImage, PrepareForNet

class DanyRunner(Runner):
	def framework_init(self):
		# set torch options
		#torch.backends.cudnn.enabled = True
		#torch.backends.cudnn.benchmark = True

		# select device
		self.device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
		print("device: %s" % self.device)

	def load_model(self, model_type="vitl14", **kwargs):
		repo = f"LiheYoung/depth_anything_{model_type}"
		print(f"repo: {repo}")

		orig_cwd = os.getcwd()
		os.chdir(
			os.path.join(
				os.path.dirname(os.path.abspath(__file__)),
				"dany/"
			)
		)
		model = DepthAnything.from_pretrained(repo)
		os.chdir(orig_cwd)
		
		model.to(self.device)

		total_params = sum(param.numel() for param in model.parameters())
		print('Total parameters: {:.2f}M'.format(total_params / 1e6))
		model.eval()
		self.model = model

		self.net_w, self.net_h = 518, 518

		self.transform = Compose([
			Resize(
				width=self.net_w,
				height=self.net_h,
				resize_target=False,
				keep_aspect_ratio=True,
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
		img_input = self.transform({"image": img})["image"]
		img_input = torch.from_numpy(img_input).unsqueeze(0).to(self.device)

		# compute
		with torch.no_grad():
			depth = self.model(img_input)
			depth = depth.cpu().numpy()
		depth = depth[0]

		# output
		out = self.normalize(depth)
		return out