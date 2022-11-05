import cv2
import numpy as np
from PIL import Image, ImageDraw

fourcc = cv2.VideoWriter_fourcc(*"XVID")
size = (640, 480)
fps = 1
seconds = 5
outpath = f"D:/videos/tmp_{fps}x{seconds}.avi"
out = cv2.VideoWriter(outpath, fourcc, fps, size)

for i in range(fps * seconds):
	img = Image.new("RGB", size)
	draw = ImageDraw.Draw(img)
	draw.text((size[0]/2, size[1]/2), f"{i}")
	
	out.write(np.asarray(img))
	
# Release everything if job is finished
out.release()
cv2.destroyAllWindows()