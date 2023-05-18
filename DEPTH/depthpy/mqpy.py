import zmq

PVERSION = 2

class MQ:
	def __init__(self, handlers: dict):
		self.handlers = handlers #{(ptype, pname): lambda (mdict, data)}

	def bind(self, port):
		addr = f"tcp://*:{port}"
		print(f"MQ: Binding to {addr}")
		context = zmq.Context()
		socket = context.socket(zmq.REP)
		socket.bind(addr)

		self.socket = socket

	def connect(self, port):
		addr = f"tcp://localhost:{port}"
		print(f"MQ: Connecting to {addr}")
		context = zmq.Context()
		socket = context.socket(zmq.REQ)
		socket.connect(addr)

		self.socket = socket

	def receive(self):
		"""
		Returns:
			(ptype, pname), mdict, data
		"""

		message = self.socket.recv()
		mdict = {}
		data = b""

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
					data = message
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
		if len(data) > 0:
			print(f"len(data): {len(data)}")

		t = mdict["ptype"], mdict["pname"]
		if t in self.handlers:
			handler = self.handlers[t]
		else:
			handler = on_unknown_ptype_pname

		print(f"Using handler {handler}")
		res = handler(mdict, data)

		self.send(res)
	
	def send(self, message):
		self.socket.send(message)

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

def on_unknown_ptype_pname(mdict, data=None):
	msg = f"Unknown (ptype, pname): ({mdict['ptype']}, {mdict['pname']})"
	print(msg)

	return create_error_message(msg)