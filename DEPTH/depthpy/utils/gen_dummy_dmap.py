import numpy as np
import matplotlib.pyplot as plt
import cv2

size = 512
steps = 5

arr = np.empty((size, size), dtype=np.float32)

half = size / 2
for i in np.arange(1, 0, -1/steps):	
	from_idx = int(half - i * half)
	to_idx = int(half + i * half)

	print(from_idx, to_idx)

	arr[from_idx:to_idx, from_idx:to_idx] = 1-i
	
plt.imshow(arr)
plt.show()

arr = arr * 255
arr = arr.astype(np.uint8)

name = "../tmp/dd"
pgmname = name + ".pgm"
pngname = name + ".png"
cv2.imwrite(pgmname, arr)
cv2.imwrite(pngname, arr)