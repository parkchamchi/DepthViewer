import os
import argparse

parser = argparse.ArgumentParser()

parser.add_argument("prev")
parser.add_argument("new")

args = parser.parse_args()

prev = args.prev
new = args.new

tochanges = [
	"../depth.py",
	"../../Assets/Assets/README.txt",
	"../../Assets/Scripts/Utils/DepthFileUtils.cs",
	"../../ProjectSettings/ProjectSettings.asset"
]

for filename in tochanges:
	print(f"On {filename}")
	if not os.path.exists(filename):
		print("Does not exist.")
		exit(1)

	#read the file
	with open(filename, "rt") as fin:
		content = fin.read()
	
	idx = content.index(prev)
	print(content[idx:idx+len(prev)])

	content = content.replace(prev, new)
	with open(filename, "wt") as fout:
		fout.write(content)

	print()