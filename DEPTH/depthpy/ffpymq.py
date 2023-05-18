"""
In place of the Unity VideoPlayer, this script will use ffpyplayer (binding for FFmpeg) to decode multimedia files.
It has an advantage of being able to decode VP9 codecs and other formats (gif, ...)
ffpyplayer is of LGPL.
"""

"""
***************************
ptype=REQ
pname=HANDSHAKE_IMAGE_AND_DEPTH

pversion=2
client_program=DepthViewer
client_program_version=v0.8.11
!HEADEREND
***************************
ptype=RES
pname=HANDSHAKE_IMAGE_AND_DEPTH

image_format=jpg
output_format=pfm
depth_map_type=Inverse

server_program=ffpymq
server_program_version=v0.8.11
!HEADEREND
***************************

***************************
ptype=REQ
pname=IMAGE_AND_DEPTH
!HEADEREND
***************************
ptype=RES
pname=INPUT_AND_DEPTH

status=new

len_input=1234
len_depth=4567
!HEADEREND
(input)(depth)
***************************
ptype=RES
pname=INPUT_AND_DEPTH

status=not_modified
!HEADEREND
***************************
ptype=RES
pname=INPUT_AND_DEPTH

status=not_available
!HEADEREND
***************************
"""

import depth
import mqpy

from ffpyplayer.player import MediaPlayer
from ffpyplayer.pic import SWScale
import numpy as np
import cv2

import time
from typing import Union
import argparse

runner = None
player = None

class Player:
	def __init__(self):
		self.player = None
		self.sleepuntil = 0

	def play(self, path):
		if self.player is not None:
			self.player.close_player()
			self.player = None #Not needed

		self.player = MediaPlayer(
			path,
			ff_opts={
				"loop": 0, #loop the video
			}
		)

	def get_frame(self) -> Union[np.ndarray, None]:
		if self.player is None:
			return None
		if time.time() < self.sleepuntil:
			return None
		self.sleepuntil = 0

		frame, val = self.player.get_frame()
		if val == 'eof':
			return None
		elif frame is None:
			self.sleepuntil = time.time() + 0.01
			return None
		else:
			img, t = frame
			#print(val, t, img.get_pixel_format(), img.get_buffer_size())

			w, h = img.get_size()
			sws = SWScale(w, h, img.get_pixel_format(), ofmt="bgr24") #Convert as uint8 bgr
			bgr = sws.scale(img)
			bgr = bgr.to_bytearray()[0]
			bgr = np.frombuffer(bgr, dtype=np.uint8)

			channels = []
			for i in range(3):
				channels.append(bgr[i::3].reshape(h, -1))
			bgr = np.dstack(channels)

			self.sleepuntil = time.time() + val
			return bgr
		
def on_req_handshake_image_and_depth(mdict, data=None):
	pversion = mdict["pversion"]
	if int(pversion) > mqpy.PVERSION:
		return mqpy.create_error_message(f"Unsupported pversion: {pversion}")

	return mqpy.create_message({
		"ptype": "RES",
		"pname": mdict["pname"],

		"image_format": "jpg",
		"output_format": "pfm",
		"depth_map_type": runner.depth_map_type,

		"server_program": "ffpymq",
		"server_program_version": depth.VERSION,
	})

def on_req_image_and_depth(mdict, data=None):
	bgr = player.get_frame()

	#if not modified
	if bgr is None:
		return mqpy.create_message({
			"ptype": "RES",
			"pname": mdict["pname"],

			"status": "not_modified"
		})
	
	output = runner.as_input(bgr)
	output = runner.run_frame(output)
	output = runner.get_pfm(output)

	jpg = cv2.imencode(".jpg", bgr)[1]
	#jpg = np.array(jpg)
	jpg = jpg.tobytes()

	len_image = str(len(jpg))
	len_depth = str(len(output))
	print(f"Sending ({len_image}, {len_depth})")
	return mqpy.create_message({
		"ptype": "RES",
		"pname": mdict["pname"],

		"status": "new",

		"len_image": len_image,
		"len_depth": len_depth,
	}, data=jpg+output)

if __name__ == "__main__":
	parser = argparse.ArgumentParser()

	default_port = 5556
	parser.add_argument("-p", "--port",
		help=f"port number. defaults to {default_port}.",
		default=None
	)
	
	args = parser.parse_args()

	target = ("tmp/test.webm")
	player = Player()
	player.play(target)

	#TODO: Seperate the factory code of `Runner` to `runners.py` w/ parser manipulation
	runner = depth.PyTorchRunner()
	runner.load_model(model_type="dpt_hybrid_384", optimize=True) #tmp.

	print("ffpymq: Preparing the model. This may take some time.")
	dummy = np.zeros((512, 512, 3), dtype=np.float32)
	runner.run_frame(dummy)
	print("ffpymq: Done.")

	port = args.port if args.port is not None else default_port
	mq = mqpy.MQ({
		("REQ", "HANDSHAKE_IMAGE_AND_DEPTH"): on_req_handshake_image_and_depth,
		("REQ", "IMAGE_AND_DEPTH"): on_req_image_and_depth,
	})
	mq.bind(port)

	while True:
		mq.receive()