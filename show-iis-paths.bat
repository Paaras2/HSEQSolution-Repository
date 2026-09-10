@echo off
REM ============================================================================
REM  HSEQ - show what IIS is ACTUALLY configured to run
REM
REM  Right-click this file and choose "Run as administrator".
REM  appcmd needs elevation to read IIS configuration; without it this prints
REM  nothing useful.
REM
REM  Reads only. Changes nothing.
REM
REM  Why this exists: the site can be repaired on disk over and over with no
REM  effect if the IIS application points somewhere else. That mapping is not
REM  visible from the file system or from the browser - only from IIS itself.
REM ============================================================================

setlocal enabledelayedexpansion
set "APPCMD=%windir%\system32\inetsrv\appcmd.exe"
set "OUT=%USERPROFILE%\Desktop\hseq-iis-paths.txt"

call :main > "%OUT%" 2>&1
type "%OUT%"
echo.
echo ----------------------------------------------------------------
echo Saved to: %OUT%
echo ----------------------------------------------------------------
pause
goto :eof

:main
echo ================================================================
echo  WHAT IIS IS ACTUALLY RUNNING
echo  %DATE% %TIME%
echo ================================================================
echo.

net session >nul 2>&1
if errorlevel 1 (
    echo NOT RUNNING AS ADMINISTRATOR.
    echo Right-click this file and choose "Run as administrator".
    goto :eof
)

if not exist "%APPCMD%" (
    echo appcmd.exe not found - IIS management tools are not installed.
    goto :eof
)

echo [1] Sites and their bindings
echo ----------------------------------------------------------------
"%APPCMD%" list site
echo.

echo [2] Applications - THE PHYSICAL PATH IS WHAT MATTERS
echo ----------------------------------------------------------------
"%APPCMD%" list app /text:*  | findstr /I "APP.NAME path applicationPool physicalPath"
echo.

echo [3] Virtual directories (the real folder each app serves from)
echo ----------------------------------------------------------------
"%APPCMD%" list vdir
echo.

echo [4] Application pools and their state
echo ----------------------------------------------------------------
"%APPCMD%" list apppool
echo.

echo [5] Which release is in each configured folder?
echo ----------------------------------------------------------------
REM Walk every vdir physical path and read its RELEASE.txt. This is the step
REM that catches an application still pointing at an older copy: the folder
REM you keep updating and the folder IIS serves need not be the same one.
for /f "tokens=2 delims=()" %%V in ('"%APPCMD%" list vdir') do (
    set "LINE=%%V"
    for /f "tokens=2 delims=:" %%P in ("!LINE!") do (
        set "VPATH=%%P"
        if exist "!VPATH!" (
            echo   folder: !VPATH!
            if exist "!VPATH!\RELEASE.txt" (
                for /f "usebackq delims=" %%L in ("!VPATH!\RELEASE.txt") do (
                    echo       release: %%L
                    goto :nextvdir
                )
            ) else (
                echo       release: NO RELEASE.txt - predates version stamping, or
                echo                this folder was never updated.
            )
            if exist "!VPATH!\HSEQ.API.dll" (
                for %%F in ("!VPATH!\HSEQ.API.dll") do echo       HSEQ.API.dll modified: %%~tF
            )
            :nextvdir
            echo.
        )
    )
)

echo ================================================================
echo  WHAT TO LOOK FOR
echo ================================================================
echo  * The application with path /api must have a physicalPath that is
echo    the folder you are actually copying the backend into.
echo  * Its release line must match the client folder's release line.
echo  * If /api is missing entirely from section 2, the backend was never
echo    registered as an application and every API call returns 404.
echo.
goto :eof
