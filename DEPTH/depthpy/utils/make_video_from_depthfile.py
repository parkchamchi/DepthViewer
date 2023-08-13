import zipfile
import argparse

import cv2
import numpy as np

if __name__ == "__main__":
	parser = argparse.ArgumentParser()

	parser.add_argument("input",
		help="input file",
	)
	parser.add_argument("output",
		help="output file",
	)

	parser.add_argument("--fps",
		help="FPS value that will override the value on the metadata",
		default=None	     
	)
	
	default_fourcc = "XVID"
	parser.add_argument("--fourcc",
		help=f"defaults to {default_fourcc}",
		default=default_fourcc
	)

	args = parser.parse_args()

	filename = args.input
	fps = args.fps

	depthfile = zipfile.ZipFile(filename, "r")

	existing_filelist = depthfile.namelist()

	if args.fps is None:
		has_metadata = "METADATA.txt" in existing_filelist
		if has_metadata:
			print("Found the metadata...")
			with depthfile.open("METADATA.txt", "r") as fin:
				metadata = fin.read()
				metadata = metadata.decode("utf-8")
				metadata = [line for line in metadata.split() if line.startswith("original_framerate")]

				if metadata != []:
					fps = metadata[0].split('=')[-1]
					fps = float(fps)

	print(f"fps: {fps}")

	#Video
	vout = None
	fourcc = cv2.VideoWriter_fourcc(*args.fourcc)

	i = 0
	while True:
		pgmname = f"{i}.pgm"
		if pgmname not in existing_filelist:
			break
		print(f"On {pgmname}")

		with depthfile.open(pgmname, "r") as fin:
			pgm = fin.read()

		img = np.fromstring(pgm, np.uint8)
		img = cv2.imdecode(img, cv2.IMREAD_COLOR)

		if vout is None:
			vout = cv2.VideoWriter(args.output, fourcc, fps, img.shape[:2][::-1])
			print(img.shape[:2][::-1])
		vout.write(img)

		i += 1

	vout.release()
	print("Done.")