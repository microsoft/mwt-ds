import ntpath
import os
import os.path
import sys
from vowpalwabbit import pyvw

if len(sys.argv) < 1:
    print("Folder expected. Example: python list_vw_args.py <folder>")

for root, subdirs, files in os.walk(sys.argv[1]):
    for file in files:
        if file == 'model':
            model = os.path.join(root, file)
            vw = pyvw.vw("--quiet -i {0}".format(model))
            print(vw.get_arguments())
            del vw