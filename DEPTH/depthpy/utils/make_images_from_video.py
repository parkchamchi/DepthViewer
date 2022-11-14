import numpy as np
import cv2

import os
import argparse

"""
Makes a sequence of images from a video input
"""

parser = argparse.ArgumentParser()

parser.add_argument("input")
parser.add_argument("outputdir")

args = parser.parse_args()

filename = args.input
outdir = args.outputdir

if not os.path.exists(outdir):
	os.mkdir(outdir)

i = 0
cap = cv2.VideoCapture(filename)
while cap.isOpened():
	ret, frame = cap.read()
	if not ret:
		print("Can't receive frame (stream end?). Exiting ...")
		break

	cv2.imwrite(f"{outdir}/{i}.jpg", frame)
	i += 1

cap.release()