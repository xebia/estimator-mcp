@echo off
REM Estimator MCP Server Launcher
REM Sets environment variables and runs the MCP server

REM Get the directory where this batch file is located
set SCRIPT_DIR=%~dp0

REM Set environment variables with absolute paths
set ESTIMATOR_DATA_PATH=%SCRIPT_DIR%data
set ESTIMATOR_CATALOG_PATH=%SCRIPT_DIR%..\CatalogEditor\CatalogEditor\CatalogEditor\data\catalogs
set ESTIMATOR_LOGS_PATH=%SCRIPT_DIR%logs

REM Run the MCP server from the bin/Debug/net10.0 directory
"%SCRIPT_DIR%bin\Debug\net10.0\estimator-mcp.exe" %*
