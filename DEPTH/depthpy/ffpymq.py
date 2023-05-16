"""
In place of the Unity VideoPlayer, this script will use ffpyplayer (binding for FFmpeg) to decode multimedia files.
It has an advantage of being able to decode VP9 codecs and other formats (gif, ...)
ffpyplayer is of LGPL.
"""

from ffpyplayer.player import MediaPlayer
from ffpyplayer.pic import SWScale
import numpy as np
import cv2

import time
from typing import Union

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

target = ("tmp/test.webm")
player = Player()
player.play(target)

while True:
	res = player.get_frame()
	if res is None:
		continue

	print(res)