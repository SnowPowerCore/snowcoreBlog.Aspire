@echo off
REM Windows entry point for the AI-tools sync.
REM Bypasses PowerShell execution policy so devs don't need Set-ExecutionPolicy.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0sync-ai-tools.ps1" %*
