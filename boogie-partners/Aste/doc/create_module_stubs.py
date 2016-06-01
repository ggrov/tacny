# --------------------------------- LICENSE: ----------------------------------
# The file is part of Aste (pronounced "S-T"), an automatic build tool
# originally tailored towards Spec# and Boogie.
#
# Copyright (C) 2010  Malte Schwerhoff
#
# This program is free software; you can redistribute it and/or
# modify it under the terms of the GNU General Public License
# as published by the Free Software Foundation; either version 2
# of the License, or any later version.
#
# This program is distributed in the hope that it will be useful,
# but WITHOUT ANY WARRANTY; without even the implied warranty of
# MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
# GNU General Public License for more details.
#
# You should have received a copy of the GNU General Public License
# along with this program; if not, write to the Free Software
# Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301,
# USA.
# --------------------------------- :LICENSE ----------------------------------

import os
import sys

thisPath = os.path.realpath(os.path.dirname(sys.argv[0]))
# print thisPath
# thisPath = os.modulePath.realpath(os.modulePath.dirname(sys.argv[0]))
sys.path.append(thisPath + '\\..\\')

import fnmatch
import re
import inspect

def ensure_dir(f):
    d = os.path.dirname(f)
    if not os.path.exists(d):
        os.makedirs(d)

ROOT = "..\\"
PATTERN = "*.py" # unix-style file name matching
EXCLUDES = ['run.py', '__init__.py', '_lumberroom\\.*', 'doc\\.*'] # regular expressions

candidates = []
for dirpath, dirnames, filenames in os.walk(ROOT):
    candidates.extend (
        os.path.join(dirpath, f) for f in fnmatch.filter(filenames, PATTERN)
        )

ensure_dir('source/modules')

files = []

for f in candidates:
    if not any(re.search(pattern, f) for pattern in EXCLUDES):
        files.append(f)

print "FILES: ", files

for f in files:
    fqModuleName = f.replace('..\\', '').replace('\\', '.').replace('.py', '')
    modulePath = os.path.dirname(os.path.realpath(f))

    print '\n', f, modulePath, fqModuleName

#       i = fqModuleName.rfind('.')
#       if i > -1:
#           moduleName = fqModuleName[i+1:]
#       else:
#           moduleName = fqModuleName

    module = __import__(fqModuleName, fromlist=[''])
    print module

    with open('source/modules/%s.rst' % fqModuleName,'w') as fh:
        displayName = fqModuleName.replace('aste.', '')
        fh.writelines([displayName, '\n', '+' * len(displayName), '\n' * 2])

        for klass in inspect.getmembers(module, inspect.isclass):
            if klass[1].__module__ == fqModuleName:
                print 'class ' + klass[0]

                fh.writelines([klass[0], '\n', '~' * len(klass[0])])
                fh.write("""
.. kaaclass:: %s.%s
   :synopsis:

   .. automethods::
   .. autoproperties::

""" % (fqModuleName, klass[0]))
