@echo off
REM ============================================================================
REM  HSEQ - site folder check
REM
REM  Plain Command Prompt. No PowerShell, no administrator rights needed.
REM  Reads only; changes nothing.
REM
REM  Deliberately ASCII-only: a .bat file runs under the console codepage, and
REM  Persian text in it comes out as mojibake on a default Windows Server
REM  console. Readable English beats unreadable Persian here.
REM
REM  Usage:   check-site.bat            (uses the default path below)
REM           check-site.bat D:\some\other\path
REM ============================================================================

setlocal enabledelayedexpansion

set "SITE=%~1"
if "%SITE%"=="" set "SITE=D:\HouzoriApps\HSEQTest"
set "OUT=%USERPROFILE%\Desktop\hseq-check.txt"

call :main > "%OUT%" 2>&1
type "%OUT%"
echo.
echo ----------------------------------------------------------------
echo Report also saved to: %OUT%
echo Send that file, or a screenshot of the lines above.
echo ----------------------------------------------------------------
goto :eof

:main
echo ================================================================
echo  HSEQ SITE CHECK
echo  date: %DATE% %TIME%
echo  site folder: %SITE%
echo ================================================================
echo.

echo [1] Does the site folder exist?
if exist "%SITE%\" (
    echo     YES
) else (
    echo     NO  ^<-- THE PROBLEM. This folder does not exist.
    echo         Copy the HSEQTest folder from the ServerReady package to here.
    goto :end
)
echo.

echo [2] Is index.html directly inside it?
if exist "%SITE%\index.html" (
    echo     YES
    for %%F in ("%SITE%\index.html") do echo     size: %%~zF bytes   modified: %%~tF
) else (
    echo     NO  ^<-- THE PROBLEM. IIS returns 403.14 because there is no
    echo         default document to serve.
    echo         Most common cause: the Frontend folder itself was copied
    echo         instead of its contents, so index.html sits one level deeper.
    if exist "%SITE%\Frontend\index.html" (
        echo.
        echo     CONFIRMED: found "%SITE%\Frontend\index.html"
        echo     Move everything from %SITE%\Frontend\ up into %SITE%\
    )
)
echo.

echo [3] Is web.config present, and does it declare a default document?
if exist "%SITE%\web.config" (
    echo     web.config: YES
    for %%F in ("%SITE%\web.config") do echo     size: %%~zF bytes   modified: %%~tF
    findstr /I /C:"defaultDocument" "%SITE%\web.config" >nul 2>&1
    if !errorlevel! EQU 0 (
        echo     defaultDocument: YES - this is the NEW web.config
    ) else (
        echo     defaultDocument: NO  ^<-- THE PROBLEM. This is the OLD web.config.
        echo         Copy the new one from the ServerReady package.
    )
) else (
    echo     web.config: NO  ^<-- SPA routing and default document both missing.
)
echo.

echo [3b] Do the client and backend folders come from the same release?
echo ----------------------------------------------------------------
set "APIDIR=%SITE%-api"
set "SITEREL=(none)"
set "APIREL=(none)"
if exist "%SITE%\RELEASE.txt" for /f "usebackq delims=" %%L in ("%SITE%\RELEASE.txt") do if "!SITEREL!"=="(none)" set "SITEREL=%%L"
if exist "%APIDIR%\RELEASE.txt" for /f "usebackq delims=" %%L in ("%APIDIR%\RELEASE.txt") do if "!APIREL!"=="(none)" set "APIREL=%%L"
echo     client  (%SITE%): !SITEREL!
echo     backend (%APIDIR%): !APIREL!
if "!SITEREL!"=="(none)" (
    echo     No RELEASE.txt - this predates version stamping, so the two
    echo     folders cannot be compared. Recopy both from the current package.
) else (
    if "!SITEREL!"=="!APIREL!" (
        echo     MATCH - both folders are from the same release.
    ) else (
        echo     MISMATCH ^<-- THE PROBLEM. The two folders are from different
        echo         releases. A new client against an old backend makes every
        echo         API call return 404, because the backend of that release
        echo         lacks the path-prefix fix. Copy BOTH folders from one package.
    )
)
echo.

echo [4] Full listing of the site folder
echo ----------------------------------------------------------------
dir /a "%SITE%"
echo.

echo [5] Effective permissions - can IIS read this folder?
echo ----------------------------------------------------------------
icacls "%SITE%"
echo.
echo     Look for IIS_IUSRS or "Users" with (RX) or (R).
echo     If neither appears, IIS cannot read the files and returns 403.
echo     Fix:  icacls "%SITE%" /grant "IIS_IUSRS:(OI)(CI)RX" /T
echo.

echo [6] Can this account actually read index.html?
if exist "%SITE%\index.html" (
    type "%SITE%\index.html" >nul 2>&1
    if !errorlevel! EQU 0 (
        echo     YES - readable
    ) else (
        echo     NO  ^<-- permission problem on the file itself.
    )
) else (
    echo     skipped - index.html not present
)
echo.

:end
echo ================================================================
echo  SUMMARY
echo ================================================================
if not exist "%SITE%\index.html" (
    echo  * index.html is MISSING from the site root.
    echo    Nothing else matters until that is fixed.
) else (
    findstr /I /C:"defaultDocument" "%SITE%\web.config" >nul 2>&1
    if !errorlevel! NEQ 0 (
        echo  * index.html is there, but web.config is the OLD one.
        echo    IIS does not know index.html is the default document,
        echo    which is exactly what produces 403.14.
        echo    Copy the new web.config from the ServerReady package.
    ) else (
        echo  * Files and web.config both look correct.
        echo    If the browser still returns 403, the cause is permissions
        echo    or the IIS site is pointing at a different folder than
        echo    %SITE%
        echo.
        echo    Check the physical path shown on the IIS error page and
        echo    make sure it matches the folder checked above.
    )
)
echo.
goto :eof
