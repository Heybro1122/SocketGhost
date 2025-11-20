@echo off
REM start_mitm.bat - Start mitmproxy adapter for SocketGhost (Windows)

setlocal

set SCRIPT_DIR=%~dp0
set ADDON_SCRIPT=%SCRIPT_DIR%adapter_addon.py

REM Check if mitmdump is installed
where mitmdump >nul 2>nul
if %ERRORLEVEL% neq 0 (
    echo Error: mitmdump not found. Please install mitmproxy:
    echo   pip install mitmproxy
    echo or
    echo   python -m pip install mitmproxy
    exit /b 1
)

REM Default values
if not defined SOCKETGHOST_MITM_HOST set SOCKETGHOST_MITM_HOST=127.0.0.1
if not defined SOCKETGHOST_MITM_PORT set SOCKETGHOST_MITM_PORT=8081
if not defined SOCKETGHOST_INGEST_URL set SOCKETGHOST_INGEST_URL=http://127.0.0.1:9001/ingest

echo Starting mitmproxy adapter for SocketGhost...
echo Proxy: http://%SOCKETGHOST_MITM_HOST%:%SOCKETGHOST_MITM_PORT%
echo Ingest endpoint: %SOCKETGHOST_INGEST_URL%

REM Start mitmdump
mitmdump -s "%ADDON_SCRIPT%" --listen-host %SOCKETGHOST_MITM_HOST% --listen-port %SOCKETGHOST_MITM_PORT% --set flow_detail=2 %*
