import flask
import torch
from PIL import Image
import numpy as np

import cv2

import argparse
import io
import sys

"""
Uses https://github.com/isl-org/ZoeDepth for metric depth estimation
"""

model = None

app = flask.Flask(__name__)

def write_pfm(image, scale=1) -> bytes:
    #Modified from https://github.com/isl-org/MiDaS/blob/master/utils.py

	assert len(image.shape) == 2
	image = np.flipud(image)

	pfm = b""
	pfm += "Pf\n".encode("ascii")
	pfm += "%d %d\n".encode("ascii") % (image.shape[1], image.shape[0])

	endian = image.dtype.byteorder
	if endian == "<" or endian == "=" and sys.byteorder == "little":
		scale = -scale
	pfm += "%f\n".encode("ascii") % scale

	pfm += image.tobytes()

	return pfm

@app.route("/zoeserver/pfm", methods=["POST"])
def pfm():
	global model

	image = flask.request.data
	image = io.BytesIO(image)
	image = Image.open(image)

	depth = model.infer_pil(image)

	image.close()

	return write_pfm(depth)

if __name__ == "__main__":
	parser = argparse.ArgumentParser()
	parser.add_argument("-m", "--modelname",
		help="name of the zoedepth model",
		default="ZoeD_NK",
		choices=["ZoeD_NK", "ZoeD_N", "ZoeD_K"])
	args = parser.parse_args()
	
	repo = "isl-org/ZoeDepth"
	model = torch.hub.load(repo, args.modelname, pretrained=True)
	device = "cuda" if torch.cuda.is_available() else "cpu"
	print(f"device: {device}")
	model.eval()
	model.to(device)

	app.run()