The script
  create_module_stubs.py
creates module reST files for all python files found
in the repository.

The script contains a list of regular expressions used to filter python files
that should not get their own module stubs (e.g. __init__.py, every file in
doc/).  

Prior to running the script delete all module stubs from
  doc\source\modules
except the main file
  index.rst
  

Running
  makehtml.bat
will clean and then recreate the HTML version of the documentation. 