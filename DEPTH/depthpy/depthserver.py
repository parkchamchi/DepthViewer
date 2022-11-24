import depth

import flask

print("Loading the depth.py Runner")
runner = depth.Runner()
print("Loaded.")

loaded_models = set()

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

@app.route("/depthpy/models/<model_type>/modeltypeval")
def model_type_val(model_type):
	res = runner.model_exists(model_type)
	if res:
		return str(res)
	else:
		flask.abort(404)

@app.route("/depthpy/models/<model_type>/pgm", methods=["POST"])
def pgm(model_type):
	if model_type not in loaded_models:
		loaded_models.add(model_type)
		runner.load_model(model_type)

	data = flask.request.data
	image = runner.read_image_bytes(data)

	out, _, _ = runner.run_frame(image)

	return out

if __name__ == "__main__":
	app.run()