@echo off
cd /d "%~dp0"
start "" "%~dp0Trasvase.exe" -screen-width 1280 -screen-height 720 -screen-fullscreen 0 -player-profile "%~dp0Trasvase-profile.json"
