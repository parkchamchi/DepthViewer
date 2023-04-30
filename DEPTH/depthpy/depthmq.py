import depth
from midas.model_loader import default_models, load_model

import zmq

import argparse

"""
Header is an ASCII string of key=value pairs. It ends with `!HEADERENDS\n`.
The rest is the data bytestring.

***************************
ptype=REQ
pname=HANDSHAKE_DEPTH

pversion=1
client_program=DepthViewer
client_program_version=v0.8.11
!HEADEREND
***************************
ptype=RES
pname=HANDSHAKE_DEPTH

model_type=dpt_hybrid_384
accepted_input_formats=jpg,ppm
output_format=pfm

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

def process():
	print('*'*64)

	message = socket.recv()
	mdict = {}

	#Decode. Assumes that the message is in the correct format
	while message != b"": #while the message is exhausted
		if b'\n' not in message:
			#Exhausted
			line = message
			message = b""
		else:
			line, message = message.split(b'\n', maxsplit=1)

		line = line.strip()
		if not line: #skip the blank line
			continue
		line = line.decode("ascii")

		#Does the line start with '!'?
		if line.startswith('!'):
			if line == "!HEADEREND":
				#The rest is the data
				mdict["data"] = message
				break
			else:
				print(f"Unknown line: {line}")
				continue

		#If it doesn't it's a `key=value` line
		if '=' not in line:
			print(f"Illegal key-value line: {line}")
			continue
		key, value = [token.strip() for token in line.split('=', maxsplit=1)] #strip the key and the token
		mdict[key] = value

	#Check the decoded message
	print(mdict)
	
	#Handle
	handler = on_unknown_ptype_pname
	t = (mdict["ptype"], mdict["pname"])
	if t == ("REQ", "HANDSHAKE_DEPTH"):
		handler = on_req_handshake_depth

	print(f"Using handler {handler}")
	res = handler(mdict)

	socket.send(res)

def create_message(mdict, data=None) -> bytes:
	message = ""
	for key, value in mdict.items():
		message += key + '=' + value + '\n'
	message += "!HEADEREND\n"
	message = message.encode("ascii")

	if data is not None:
		message += data

	return message

def create_error_message(msg: str):
	return create_message({
		"ptype": "RES",
		"pname": "ERROR",
	}, data=msg.encode("ascii"))

def on_unknown_ptype_pname(mdict):
	msg = f"Unknown (ptype, pname): ({mdict['ptype']}, {mdict['pname']})".encode("ascii")
	print(msg)

	return create_error_message(msg)

def on_req_handshake_depth(mdict):
	pversion = mdict["pversion"]
	if pversion != "1":
		return create_error_message(f"Unsupported pversion: {pversion}")

	return create_message({
		"ptype": "RES",
		"pname": mdict["pname"],
		"model_type": model_type,
		"accepted_input_formats": accepted_input_formats,
		"output_format": output_format,

		"server_program": "depthmq",
		"server_program_version": depth.VERSION,
	})

if __name__ == "__main__":
	parser = argparse.ArgumentParser()

	default_port = 5555
	parser.add_argument("-p", "--port",
		help=f"port number. defaults to {default_port}.",
		default=None
	)

	parser.add_argument('-t', '--model_type',
		default="dpt_hybrid_384",
		help="model type",
		choices=default_models.keys()
	)

	parser.add_argument("--optimize",
			help="Use the half-precision float. (Use with caution, because models like Swin require float precision to work properly and may yield non-finite depth values to some extent for half-floats.)",
			action="store_true"
		)
	parser.add_argument('--height',
		type=int, default=None,
		help='Preferred height of images feed into the encoder during inference. Note that the '
		'preferred height may differ from the actual height, because an alignment to multiples of '
		'32 takes place. Many models support only the height chosen during training, which is '
		'used automatically if this parameter is not set.'
	)
	parser.add_argument('--square',
		action='store_true',
		help='Option to resize images to a square resolution by changing their widths when images are '
		'fed into the encoder during inference. If this parameter is not set, the aspect ratio of '
		'images is tried to be preserved if supported by the model.'
	)

	args = parser.parse_args()

	print("depthmq: Init.")
	runner = depth.Runner()
	model_type = args.model_type
	runner.load_model(model_type=model_type, optimize=args.optimize, height=args.height, square=args.square)

	port = args.port if args.port is not None else default_port
	addr = f"tcp://*:{port}"
	print(f"Binding to {addr}")
	context = zmq.Context()
	socket = context.socket(zmq.REP)
	socket.bind(addr)

	while True:
		process()