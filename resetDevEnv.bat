@echo off
echo Reset the Experimental Developer Environment
pause
del "%appdata%\Microsoft\VisualStudio\14.0Exp\" /S /Q
del "%localappdata%\Microsoft\VisualStudio\14.0Exp\" /S /Q