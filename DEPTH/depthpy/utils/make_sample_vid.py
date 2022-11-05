import cv2
import numpy as np
from PIL import Image, ImageDraw

fourcc = cv2.VideoWriter_fourcc(*"XVID")
size = (640, 480)
fps = 30
outpath = "D:/videos/tmp.avi"
out = cv2.VideoWriter(outpath, fourcc, fps, size)
seconds = 10

for i in range(fps * seconds): #10 secs -> 300 frames
	img = Image.new("RGB", size)
	draw = ImageDraw.Draw(img)
	draw.text((size[0]/2, size[1]/2), f"{i}")
	
	out.write(np.asarray(img))
	
# Release everything if job is finished
out.release()
cv2.destroyAllWindows()