import numpy as np
import matplotlib.pyplot as plt
import cv2

size = 512
steps = 4

assert steps % 2 == 0

arr = np.zeros((size, size), dtype=np.float32)

half = size / 2
k = half / (steps+1)
for i in range(steps):
	disp = k * i
	from_idx = int(disp)
	to_idx = int(-disp)
	val = i * (1 / (steps-1))

	print(from_idx, to_idx, val)

	arr[from_idx:to_idx, from_idx:to_idx] = val
	
plt.imshow(arr, cmap="gray")
plt.colorbar()
plt.show()

arr = arr * 255
arr = arr.astype(np.uint8)

name = "../tmp/dd"
pgmname = name + ".pgm"
pngname = name + ".png"
cv2.imwrite(pgmname, arr)
cv2.imwrite(pngname, arr)