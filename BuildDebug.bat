@echo off

if "%1"=="" (goto DoAll)

if "%1"=="all" (
:DoAll
    msbuild boogie\Source\Boogie.sln /p:Configuration=Debug /p:Platform="Any CPU" &&     msbuild dafny\Source\Dafny.sln /p:Configuration=Debug /p:Platform="Any CPU" &&     msbuild tacny\Tacny.sln /p:Configuration=Debug /p:Platform="Any CPU" &&     msbuild dafny\Source\Dafny.sln /p:Configuration=Debug /p:Platform="Any CPU" &&     msbuild dafny\Source\DafnyExtension.sln /p:Configuration=Debug /p:Platform="Any CPU"
) else if "%1"=="ext" (
    msbuild dafny\Source\Dafny.sln /p:Configuration=Debug /p:Platform="Any CPU" &&     msbuild tacny\Tacny.sln /p:Configuration=Debug /p:Platform="Any CPU" &&     msbuild dafny\Source\Dafny.sln /p:Configuration=Debug /p:Platform="Any CPU" &&     msbuild dafny\Source\DafnyExtension.sln /p:Configuration=Debug /p:Platform="Any CPU"
) else if "%1"=="daf"  (
    msbuild dafny\Source\Dafny.sln /p:Configuration=Debug /p:Platform="Any CPU" &&     msbuild tacny\Tacny.sln /p:Configuration=Debug /p:Platform="Any CPU" &&     msbuild dafny\Source\Dafny.sln /p:Configuration=Debug /p:Platform="Any CPU" 
 )