import depth
import mqpy

import numpy as np

import argparse

"""
Header is an ASCII string of key=value pairs. It ends with `!HEADEREND\n`.
The rest is the data bytestring.

***************************
ptype=REQ
pname=HANDSHAKE_DEPTH

pversion=2
client_program=DepthViewer
client_program_version=v0.8.11
!HEADEREND
***************************
ptype=RES
pname=HANDSHAKE_DEPTH

model_type=dpt_hybrid_384
accepted_input_formats=jpg,ppm
output_format=pfm
depth_map_type=Inverse

server_program=depthmq
server_program_version=v0.8.11
!HEADEREND
***************************

***************************
ptype=REQ
pname=DEPTH

input_format=jpg
!HEADEREND
(bytestring)
***************************
ptype=RES
pname=DEPTH

!HEADEREND
(bytestring)
***************************

Key "data" is reserved.
"""

model_type = "NotSetYet"
accepted_input_formats = "jpg, ppm"
output_format = "pfm"

def on_req_handshake_depth(mdict, data=None):
	pversion = mdict["pversion"]
	if int(pversion) > mqpy.PVERSION:
		return mqpy.create_error_message(f"Unsupported pversion: {pversion}")

	return mqpy.create_message({
		"ptype": "RES",
		"pname": mdict["pname"],
		"model_type": model_type,
		"accepted_input_formats": accepted_input_formats,
		"output_format": output_format,
		"depth_map_type": runner.depth_map_type,

		"server_program": "depthmq",
		"server_program_version": depth.VERSION,
	})

def on_req_depth(mdict, data):
	input_format = mdict["input_format"]

	if input_format in ["jpg", "ppm"]:
		img = runner.read_image_bytes(data)
		
		res = runner.run_frame(img) #ndarray
		res = runner.get_pfm(res)

	else:
		error_msg = f"Error: unknown input_format: {input_format}"
		print(error_msg)
		return mqpy.create_error_message(error_msg)
	
	return mqpy.create_message({
		"ptype": "RES",
		"pname": mdict["pname"],
	}, data=res)

if __name__ == "__main__":
	parser = argparse.ArgumentParser()

	default_port = 5555
	parser.add_argument("-p", "--port",
		help=f"port number. defaults to {default_port}.",
		default=None
	)

	depth.add_runner_argparser(parser)

	args = parser.parse_args()

	print("depthmq: Init.")
	model_type = args.model_type
	runner = depth.get_loaded_runner(args)

	print("depthmq: Preparing the model. This may take some time.")
	dummy = np.zeros((512, 512, 3), dtype=np.float32)
	runner.run_frame(dummy)
	print("depthmq: Done.")

	port = args.port if args.port is not None else default_port
	mq = mqpy.MQ({
		("REQ", "HANDSHAKE_DEPTH"): on_req_handshake_depth,
		("REQ", "DEPTH"): on_req_depth,
	})
	mq.bind(port)

	while True:
		mq.receive()