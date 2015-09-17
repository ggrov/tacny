# the tacny project {tactics for Dafny} #

This repository contains the Source code for Tacny - a tool that implements tactics for the Dafny program verifier.
# Build Guide #

### Requirements ###
* Visual Studio 2012 Professional/Ultimate
* Visual Studio 2012 SDK [Download](https://visualstudiogallery.msdn.microsoft.com/b2fa5b3b-25eb-4a2f-80fd-59224778ea98)
* Code Contracts Extension [Download](https://visualstudiogallery.msdn.microsoft.com/1ec7db13-3363-46c9-851f-1ce455f66970)
* NUnit Test Adapter [Download](https://visualstudiogallery.msdn.microsoft.com/6ab922d0-21c0-4f06-ab5f-4ecd1fe7175d)
* TortoiseHg Version Control [Download](http://tortoisehg.bitbucket.org/download/windows.html)
* Z3 Version 4.1 [Download](ftp://ftp.research.microsoft.com/downloads/0a7db466-c2d7-4c51-8246-07e25900c7e7/z3-4.1.msi)
* Python
* Pip
* pip install lit
* pip install OutputCheck

### Dependencies ###
* Dafny Repository for Tortoise: https://hg.codeplex.com/dafny 
* Boogie GitHub Repository: https://github.com/boogie-org/boogie

* BoogiePartners Repository for Tortoise: https://hg.codeplex.com/boogiepartners
* Coco.exe [Download](http://www.ssw.uni-linz.ac.at/Research/Projects/Coco)

### Installation ###
* Clone Boogie and Boogiepartners to the same directory as Tacny
* Create new dir boogiepartners/bin and move Coco.exe there
* Build Boogie  [Guide](https://github.com/boogie-org/boogie#windows)
   * *Note:* Check Boogie [requirements](https://github.com/boogie-org/boogie#requirements).
* Build Dafny with Visual Studio
* Build DafnyExtension with Visual Studio
* The Dafny VisualStudio Extension is located in dafny/Binaries/DafnyLanguageService.vsix

### Executing Tacny ###
* Create a working directory in tacny/tacny directory
* Create dafny program in the created folder
* Open Tacny solution in Visual Studio
* Select Debug > TacnyDriver Properties 
* Under debug tab, select the Working Directory 
* In the command line arguments type /rprint:- dafny_file.dfy
* Regsiter Dafny and Boogie Refereces
* Run Tacny
