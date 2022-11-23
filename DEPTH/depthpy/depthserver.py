import depth

import flask

print("Loading the depth.py Runner")
runner = depth.Runner()
print("Loaded.")

app = flask.Flask(__name__)

@app.route('/')
def index():
	return '<a href="https://github.com/parkchamchi/DepthViewer">depthserver.py<a>'

@app.route("/depthpy")
def depthpy_version():
	return f"depth.py {depth.VERSION}"

@app.route("/depthpy/models/<model_type>")
def model_available(model_type):
	res = runner.model_exists(model_type)
	if res:
		return str(res)
	else:
		flask.abort(404)

@app.route("/depthpy/models/<model_type>/pgm", methods=["POST"])
def pgm(model_type):
	runner.load_model(model_type)

	data = flask.request.data
	image = runner.read_image_bytes(data)

	out, _, _ = runner.run_frame(image)

	return out

if __name__ == "__main__":
	app.run()