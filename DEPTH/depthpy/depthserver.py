import depth

import flask

import argparse

print("Loading the depth.py Runner")
runner = depth.Runner()
print("Loaded.")

loaded_model = ""

app = flask.Flask(__name__)

@app.route('/')
def index():
	return '<a href="https://github.com/parkchamchi/DepthViewer">depthserver.py<a>'

@app.route("/depthpy")
def depthpy_version():
	return f"depth.py {depth.VERSION}"

@app.route("/depthpy/models/<model_type>")
def model_index(model_type):
	if runner.model_exists(model_type):
		return model_type
	else:
		flask.abort(404)

#model_type_val is not used anymore! just return 0 when the model exists
@app.route("/depthpy/models/<model_type>/modeltypeval")
def model_type_val(model_type):
	res = runner.model_exists(model_type)
	if res:
		return "0"
	else:
		flask.abort(404)

@app.route("/depthpy/models/<model_type>/pgm", methods=["POST"])
def pgm(model_type):
	global loaded_model
	if model_type != loaded_model:
		loaded_model = model_type
		runner.load_model(model_type)

	data = flask.request.data
	image = runner.read_image_bytes(data)

	out, _, _ = runner.run_frame(image)

	return out

if __name__ == "__main__":
	parser = argparse.ArgumentParser()
	parser.add_argument("-p", "--port",
		help="port number. defaults to 5000.",
		default=None
	)
	args = parser.parse_args()

	app.run(port=args.port)