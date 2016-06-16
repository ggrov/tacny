# the Tacny tool #

This repository contains the Source code for Tacny - a tool that implements tactics for the Dafny program verifier.
# Build Guide #https://github.com/ggrov/tacny/blob/master/README.md

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

### C# 6.0 for VS 2012 ###
* Go to TOOL -> Extensions and Updates to double all are the latest version, in particular NuGet
* Open PM from TOOL -> NGet Package Manager -> Package Manager Console
* To install the 6.0 compiler: Install-Package Microsoft.Net.Compilers -Version 1.2.2 (Note that for each solution requiring c# 6.0 will need to install this package)
* Right click solution to select Manage NuGet Package for Solution, then to apply the installed package to all projects in the solution
