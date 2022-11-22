import depth

import flask

import base64

print("Loading the depth.py Runner")
runner = depth.Runner()
print("Loaded.")

app = flask.Flask(__name__)

@app.route('/', methods=["POST"])
def root():
	args = flask.request.json

	model_type = args["model_type"]
	runner.load_model(model_type)

	is_image = args["is_image"]
	if is_image:
		image = args["image"] + "==" #padding
		image = base64.b64decode(image)
		image = runner.read_image_bytes(image)

		out, _, _ = runner.run_frame(image)
		out = base64.b64encode(out)

	return out

if __name__ == "__main__":
	app.run(debug=True)