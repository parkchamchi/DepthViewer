import zmq

import argparse

"""
Header is an ASCII string of key=value pairs. It ends with `!HEADERENDS\n`.
The rest is the data bytestring.

***************************
ptype=REQ
name=HANDSHAKE_DEPTH
pversion=1
client_program=DepthViewer
client_program_version=v0.8.11
!HEADEREND
***************************
ptype=RES
name=HANDSHAKE_DEPTH
model_type=dpt_hybrid_384
accepted_input_formats=jpg,ppm
output_format=pfm
!HEADEREND
***************************

***************************
ptype=REQ
name=DEPTH
input_format=jpg
!HEADEREND
(bytestring)
***************************
ptype=RES
name=DEPTH
!HEADEREND
(bytestring)
***************************

Key "data" is reserved.
"""

model_type = b"NotSetYet"
accepted_input_formats = b"jpg, ppm"
output_format = b"pfm"

def process():
	print('*'*64)

	message = socket.recv()
	mdict = {}

	#Decode. Assumes that the message is in the correct format
	while message != b"": #while the message is exhausted
		if b'\n' not in message:
			print("Error: `message` exhausted before the !HEADEREND.")
			break

		line, message = message.split(b'\n', maxsplit=1)
		line = line.strip()
		if not line: #skip the blank line
			continue

		#Does the line start with '!'?
		if line.startswith(b'!'):
			if line == b"!HEADEREND":
				#The rest is the data
				mdict[b"data"] = message
				break
			else:
				print(f"Unknown line: {line}")
				continue

		#If it doesn't it's a `key=value` line
		if b'=' not in line:
			print(f"Illegal key-value line: {line}")
			continue
		key, value = [token.strip() for token in line.split(b'=', maxsplit=1)] #strip the key and the token
		mdict[key] = value

	#Check the decoded message
	print(mdict)
	
	#Handle
	handler = on_unknown_ptype_name
	t = (mdict[b"ptype"], mdict[b"name"])
	if t == (b"REQ", b"HANDSHAKE_DEPTH"):
		handler = on_req_handshake_depth

	print(f"Using handler {handler}")
	res = handler(mdict)

	socket.send(res)

def create_message(mdict, data=None) -> bytes:
	message = b""
	for key, value in mdict.items():
		message += key + b'=' + value + b'\n'
	message += b"!HEADEREND\n"

	if data is not None:
		message += data

	return message

def create_error_message(msg: str):
	return create_message({
		b"ptype": b"RES",
		b"name": b"ERROR",
	}, data=msg.encode("ascii"))

def on_unknown_ptype_name(mdict):
	msg = f"Unknown (ptype, name): ({mdict[b'ptype']}, {mdict[b'name']})".encode("ascii")
	print(msg)

	return create_error_message(msg)

def on_req_handshake_depth(mdict):
	pversion = mdict[b"pversion"]
	if pversion != b"1":
		return create_error_message(f"Unsupported pversion: {pversion}")

	return create_message({
		b"ptype": b"RES",
		b"name": mdict[b"name"],
		b"model_type": model_type,
		b"accepted_input_formats": accepted_input_formats,
		b"output_format": output_format,
	})

if __name__ == "__main__":
	parser = argparse.ArgumentParser()
	default_port = 5555
	parser.add_argument("-p", "--port",
		help=f"port number. defaults to {default_port}.",
		default=None
	)
	args = parser.parse_args()
	port = args.port if args.port is not None else default_port

	context = zmq.Context()
	socket = context.socket(zmq.REP)
	addr = f"tcp://*:{port}"
	print(f"Binding to {addr}")
	socket.bind(addr)

	while True:
		process()