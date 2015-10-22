# the Tacny tool #

This repository contains the Source code for Tacny - a tool that implements tactics for the Dafny program verifier.
# Build Guide #

### Installation ###
* Pull the repository
* Compile Boogie and Dafny in Visual Studio
* Add references in Tacny (Reference list to be added)
### Executing Tacny ###
* Create a working directory in tacny/tacny directory
* Create dafny program in the created folder
* Open Tacny solution in Visual Studio
* Select Debug > TacnyDriver Properties 
* Under debug tab, select the Working Directory 
* In the command line arguments type /rprint:- dafny_file_name.dfy
* Run Tacny
