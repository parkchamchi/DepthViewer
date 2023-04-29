import zmq

import argparse

"""

***************************
ptype=REQ
name=DEPTH
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

def process():
	print('*'*64)

	message = socket.recv()
	message_decoded = {}

	data = None

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
				data = message
				break
			else:
				print(f"Unknown line: {line}")
				continue

		#If it doesn't it's a `key=value` line
		if b'=' not in line:
			print(f"Illegal key-value line: {line}")
			continue
		key, value = [token.strip() for token in line.split(b'=', maxsplit=1)] #strip the key and the token
		message_decoded[key] = value

	#Check the decoded message
	print(message_decoded)
	if data:
		print(f"len(data): {len(data)}")
		print(data)

	socket.send(b"hi")

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