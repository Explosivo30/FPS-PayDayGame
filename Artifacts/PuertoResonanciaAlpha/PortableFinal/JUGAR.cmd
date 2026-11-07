@echo off
cd /d "%~dp0"
start "" "%~dp0PuertoResonancia.exe" -screen-width 1600 -screen-height 900 -screen-fullscreen 0 -player-profile "%~dp0PuertoResonancia-profile.json"
