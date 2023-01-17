import os
import sys
import argparse
import math

import matplotlib.pyplot as plt

sys.path.append("..")
from depth import default_models, Runner

"""
Compare the models available in ../weights/
& plot using plt
"""

parser = argparse.ArgumentParser()
parser.add_argument("input", help="The input image.")
args = parser.parse_args()

runner = Runner()

print("Reading the input image.")
img = runner.read_image(args.input)[0]
print("Input image read.")

#First check how many models are available (for plt.subplot)
print("Searching models...")
available_models = []
for model_type, model_path in default_models.items():
	print(model_type, end=": ")

	if not os.path.exists(os.path.join("..", model_path)):
		print(f"File not exists: {model_path}")
	else:
		print("Found.")
		available_models.append(model_type)

print()
grid_size = math.ceil(math.sqrt(len(available_models)))

for model_type in available_models:
	print('-'*32)
	print(f"Loading: {model_type}")
	runner.load_model(model_type)
	print("Loaded the model.")

	print("Running.")
	out = runner.run_frame(img, no_pgm=True)

	#plot
	plt.subplot(grid_size, grid_size, available_models.index(model_type)+1)
	plt.imshow(out)
	plt.title(model_type)

#plt.tight_layout()
print("Done.")
plt.show()