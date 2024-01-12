"""
	This is a modification of
		https://github.com/prs-eth/Marigold/blob/v0.1.1/run.py
	(Apache License 2.0)

	The original header was:
		# Copyright 2023 Bingxin Ke, ETH Zurich. All rights reserved.
		#
		# Licensed under the Apache License, Version 2.0 (the "License");
		# you may not use this file except in compliance with the License.
		# You may obtain a copy of the License at
		#
		#     http://www.apache.org/licenses/LICENSE-2.0
		#
		# Unless required by applicable law or agreed to in writing, software
		# distributed under the License is distributed on an "AS IS" BASIS,
		# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
		# See the License for the specific language governing permissions and
		# limitations under the License.
		# --------------------------------------------------------------------------
		# If you find this code useful, we kindly ask you to cite our paper in your work.
		# Please find bibtex at: https://github.com/prs-eth/Marigold#-citation
		# More information about the method can be found at https://marigoldmonodepth.github.io
		# --------------------------------------------------------------------------
"""

from depth import Runner, ModelParams

from Marigold.marigold import MarigoldPipeline
from Marigold.marigold.util.seed_all import seed_all

import torch
import numpy as np
from PIL import Image

default_max_height = 512

class MariRunner(Runner):
	def framework_init(self):
		pass

	def load_model(self,
			model_type="Bingxin/Marigold",
			optimize=True,
			height=768,

			denoise_steps=10,
			ensemble_size=10,
			seed=None,
			batch_size=0,
			apple_silicon=False,
			**kwargs):

		self.depth_map_type = "Linear"

		self.model_type = model_type
		
		self.height = height
		self.batch_size = batch_size

		aux_args = None
		if "aux_args" in kwargs and kwargs["aux_args"] is not None:
			aux_args = kwargs["aux_args"]
			aux_args_list = aux_args.split(',')
			for aux_arg in aux_args_list:
				if "=" not in aux_arg:
					print(f"Invalid aux_arg: {aux_arg}")
					continue

				k, v = aux_arg.split('=', maxsplit=1)
				if k == "den_s":
					print(f"Setting denoise_steps to {v}. This will ignore the original value {denoise_steps}.")
					denoise_steps = int(v)
				elif k == "ens_s":
					print(f"Setting ensemble_size to {v}. This will ignore the original value {ensemble_size}.")
					ensemble_size = int(v)
				else:
					print(f"Unknown aux_arg: {k}")

		self.denoise_steps = denoise_steps
		self.ensemble_size = ensemble_size

		print(f"denoise_steps={denoise_steps}, ensemble_size={ensemble_size}")
		if ensemble_size > 15:
			print(f"Warning: Running with large ensemble size will be slow.")

		if apple_silicon and 0 == batch_size:
			batch_size = 1  # set default batchsize

		self.model_params = ModelParams(optimize=optimize, height=height, aux_args=aux_args)

		# -------------------- Preparation --------------------
		# Random seed
		if seed is None:
			import time
			seed = int(time.time())
		seed_all(seed)

		# -------------------- Device --------------------
		if apple_silicon:
			if torch.backends.mps.is_available() and torch.backends.mps.is_built():
				device = torch.device("mps:0")
			else:
				device = torch.device("cpu")
				print(f"Warning: MPS is not available. Running on CPU will be slow.")
		else:
			if torch.cuda.is_available():
				device = torch.device("cuda")
			else:
				device = torch.device("cpu")
				print(f"Warning: CUDA is not available. Running on CPU will be slow.")
		print(f"Info: device = {device}")

		# -------------------- Model --------------------
		if optimize:
			dtype = torch.float16
			print(f"Info: Running with half precision ({dtype}).")
		else:
			dtype = torch.float32

		pipe = MarigoldPipeline.from_pretrained(model_type, torch_dtype=dtype)
		try:
			import xformers
			pipe.enable_xformers_memory_efficient_attention()
		except:
			pass  # run without xformers
		pipe = pipe.to(device)

		self.model = pipe

	def run_frame(self, img):
		pil_image = Image.fromarray(np.uint8(img * 255)).convert("RGB")
		#pil_image.show()

		# Predict depth
		pipe_out = self.model(
			pil_image,
			denoising_steps=self.denoise_steps,
			ensemble_size=self.ensemble_size,
			processing_res=self.height,
			match_input_res=False,
			batch_size=self.batch_size,
			color_map="Spectral",
			show_progress_bar=True,
		)

		depth_pred: np.ndarray = pipe_out.depth_np

		return depth_pred