import requests
import numpy as np
import cv2

import matplotlib.pyplot as plt

host = "http://127.0.0.1:5000"
#model_type = "dpt_beit_large_512"
#url = f"{host}/depthpy/models/{model_type}/pgm"
url = f"{host}/zoeserver/pfm"

with open("../tmp/test.jpg", "rb") as fin:
	image = fin.read()

res = requests.post(url, data=image)
print("Got.")
pfm = res.content

with open("../tmp/output.pfm", "wb") as fout:
	fout.write(pfm)

img = np.frombuffer(pfm, np.uint8)
img = cv2.imdecode(img, cv2.IMREAD_UNCHANGED)

print(img)
plt.imshow(img)
plt.colorbar()
plt.show()