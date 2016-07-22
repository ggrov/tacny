@echo off

echo "Testing MSBuild..."
msbuild /ver /nologo
echo .
if NOT "%errorlevel%"=="0" ( 
 echo ========================================================
 echo Please reuse the script inside an MSBuild command prompt
 echo Default located under Start - Visual Studio - Tools
 echo ========================================================
 goto end
)

echo "Testing NuGet..."
nuget config
echo .
if NOT "%errorlevel%"=="0" ( 
 echo ====================================================
 echo Please install nuget command line and add it to path
 echo ====================================================
 goto end
)

if "%1"=="all" ( goto boogie )
if "%1"=="boogie" ( goto boogie )
if "%1"=="dafny" ( goto dafnyprimary )
if "%1"=="ext" ( goto ext )

if NOT "%1"=="" (
 echo =================================================================
 echo Usage: make {all/boogie/dafny/ext} [ext]                       
 echo Make either all, boogie, dafny, or just the extension            
 echo When building dafny, tacny is automatically included in the build
 echo If building both dafny and the extension, use second ext option  
 echo Shorthand for make all is just make                              
 echo =================================================================
 goto end
)

:boogie
 nuget restore boogie\Source\Boogie.sln
 msbuild boogie\Source\Boogie.sln /p:Configuration=Debug /p:Platform="Any CPU"
 if NOT "%errorlevel%"=="0" ( goto end )
 if "%1"=="boogie" ( goto end )
 
:dafnyprimary
 nuget restore dafny\Source\Dafny.sln
 msbuild dafny\Source\Dafny.sln /p:Configuration=Debug /p:Platform="Any CPU"
 if NOT "%errorlevel%"=="0" ( goto end )
 
:tacny
 nuget restore tacny\Tacny.sln
 msbuild tacny\Tacny.sln /p:Configuration=Debug /p:Platform="Any CPU"
 if NOT "%errorlevel%"=="0" ( goto end )
 
:dafnysecondary
 msbuild dafny\Source\Dafny.sln /p:Configuration=Debug /p:Platform="Any CPU"
 if NOT "%errorlevel%"=="0" ( goto end )
 if "%1"=="all" ( goto ext )
 if "%1"=="" ( goto ext )
 if NOT "%2"=="ext" ( goto end )
 
:ext
 nuget restore dafny\Source\DafnyExtension.sln
 msbuild dafny\Source\DafnyExtension.sln /p:Configuration=Debug /p:Platform="Any CPU"

:end