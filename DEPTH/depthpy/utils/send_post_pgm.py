import requests
import numpy as np
import cv2

host = "http://127.0.0.1:5000"
model_type = "dpt_beit_large_512"
url = f"{host}/depthpy/models/{model_type}/pgm"

with open("../tmp/test.jpg", "rb") as fin:
	image = fin.read()

res = requests.post(url, data=image)
print("Got.")
jpg = res.content

img = np.frombuffer(jpg, np.uint8)
img = cv2.imdecode(img, cv2.IMREAD_UNCHANGED)

cv2.imshow("out", img)
cv2.waitKey(0)