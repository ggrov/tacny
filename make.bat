@echo off

echo "Testing MSBuild..."
msbuild /ver /nologo
echo .
if NOT "%errorlevel%"=="0" ( 
 echo ========================================================
 echo Please reuse the script inside an MSBuild command prompt
 echo Default located under Start - Visual Studio - Tools
 echo ========================================================
 goto badend
)

echo "Testing NuGet..."
nuget config
echo .
if NOT "%errorlevel%"=="0" ( 
 echo ====================================================
 echo Please install nuget command line and add it to path
 echo ====================================================
 goto badend
)

if "%1"=="all" ( goto boogie )
if "%1"=="boogie" ( goto boogie )
if "%1"=="dafny" ( goto dafny )
if "%1"=="ext" ( goto ext )

if NOT "%1"=="" (
 echo =================================================================
 echo Usage: make {all/boogie/dafny/ext}                               
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
 if NOT "%errorlevel%"=="0" ( goto badend )
 if "%1"=="boogie" ( goto end )
 
:dafny
 nuget restore new-dafny\Source\Dafny.sln
 msbuild new-dafny\Source\Dafny.sln /p:Configuration=Debug /p:Platform="Any CPU"
 if NOT "%errorlevel%"=="0" ( goto badend )
 if "%1"=="dafny" ( goto end )

:ext
 nuget restore new-dafny\Source\DafnyExtension.sln
 msbuild new-dafny\Source\DafnyExtension.sln /p:Configuration=Debug /p:Platform="Any CPU"
 if NOT "%errorlevel%"=="0" ( goto badend )

:end
exit /b 0

:badend
exit /b 1