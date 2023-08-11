import sys
import os
import shutil

#Only for Windows
assert sys.platform == "win32"

datadir = os.path.join(os.getenv("APPDATA"), r"../LocalLow/parkchamchi")
print(f"datadir: `{datadir}`")

if not os.path.exists(datadir):
    print("datadir does not exist. exiting...")
    exit(0)
    
inputstr = input("Remove this dir? [Y/n]: ")
if inputstr.upper() != "Y":
    print("Exiting.")
    exit(0)

print("Removing...")
shutil.rmtree(datadir)
print("Removed.")