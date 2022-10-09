import cv2

path = r"F:\videos\traktor.mp4"

cap = cv2.VideoCapture(path)

print((cap.get(cv2.CAP_PROP_FRAME_COUNT)))

while cap.isOpened():
	print(cap.get(cv2.CAP_PROP_POS_FRAMES))
	ret, frame = cap.read()
	if not ret:
		print("Can't receive frame (stream end?). Exiting ...")
		break



cap.release()
cv2.destroyAllWindows()