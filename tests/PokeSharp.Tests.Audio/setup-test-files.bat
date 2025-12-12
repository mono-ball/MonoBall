@echo off
REM Audio Streaming Test Setup Script (Windows)
REM Generates test OGG audio files for streaming tests

setlocal enabledelayedexpansion

set SCRIPT_DIR=%~dp0
set TEST_DATA_DIR=%SCRIPT_DIR%TestData\Audio
set SFX_DIR=%TEST_DATA_DIR%\SFX

echo ==========================================
echo Audio Streaming Test Setup
echo ==========================================
echo.

REM Check if ffmpeg is installed
where ffmpeg >nul 2>nul
if %ERRORLEVEL% neq 0 (
    echo ERROR: ffmpeg is not installed!
    echo Please install ffmpeg from https://ffmpeg.org/download.html
    echo Add ffmpeg to your PATH environment variable
    exit /b 1
)

for /f "tokens=*" %%i in ('ffmpeg -version ^| findstr /C:"ffmpeg version"') do (
    echo [OK] ffmpeg found: %%i
    goto :ffmpeg_found
)
:ffmpeg_found
echo.

REM Create directories
echo Creating test data directories...
if not exist "%TEST_DATA_DIR%" mkdir "%TEST_DATA_DIR%"
if not exist "%SFX_DIR%" mkdir "%SFX_DIR%"
echo [OK] Directories created
echo.

REM Generate test music file (3 seconds, 440Hz sine wave)
echo Generating test_music.ogg (3s, 440Hz A4)...
ffmpeg -y -f lavfi -i "sine=frequency=440:duration=3" -acodec libvorbis -q:a 4 "%TEST_DATA_DIR%\test_music.ogg" -loglevel error
if %ERRORLEVEL% equ 0 (
    for %%A in ("%TEST_DATA_DIR%\test_music.ogg") do echo [OK] test_music.ogg created (%%~zA bytes)
) else (
    echo [ERROR] Failed to create test_music.ogg
)

REM Generate test loop file (5 seconds, 880Hz sine wave)
echo Generating test_loop.ogg (5s, 880Hz A5)...
ffmpeg -y -f lavfi -i "sine=frequency=880:duration=5" -acodec libvorbis -q:a 4 "%TEST_DATA_DIR%\test_loop.ogg" -loglevel error
if %ERRORLEVEL% equ 0 (
    for %%A in ("%TEST_DATA_DIR%\test_loop.ogg") do echo [OK] test_loop.ogg created (%%~zA bytes)
) else (
    echo [ERROR] Failed to create test_loop.ogg
)

REM Add loop point metadata (vorbiscomment usually not available on Windows)
where vorbiscomment >nul 2>nul
if %ERRORLEVEL% equ 0 (
    echo Adding loop point metadata to test_loop.ogg...
    (
        echo LOOPSTART=44100
        echo LOOPLENGTH=88200
    ) > "%TEST_DATA_DIR%\loop_tags.txt"
    vorbiscomment -w -c "%TEST_DATA_DIR%\loop_tags.txt" "%TEST_DATA_DIR%\test_loop.ogg"
    del "%TEST_DATA_DIR%\loop_tags.txt"
    echo [OK] Loop point metadata added
) else (
    echo [WARNING] vorbiscomment not found - loop point metadata not added
    echo   You can install vorbis-tools manually if needed
)

REM Generate short sound effect (1 second, 1320Hz sine wave)
echo Generating test_sfx.ogg (1s, 1320Hz E6)...
ffmpeg -y -f lavfi -i "sine=frequency=1320:duration=1" -acodec libvorbis -q:a 4 "%SFX_DIR%\test_sfx.ogg" -loglevel error
if %ERRORLEVEL% equ 0 (
    for %%A in ("%SFX_DIR%\test_sfx.ogg") do echo [OK] test_sfx.ogg created (%%~zA bytes)
) else (
    echo [ERROR] Failed to create test_sfx.ogg
)

REM Generate long sound effect (3 seconds, 660Hz sine wave)
echo Generating test_long_sfx.ogg (3s, 660Hz E5)...
ffmpeg -y -f lavfi -i "sine=frequency=660:duration=3" -acodec libvorbis -q:a 4 "%SFX_DIR%\test_long_sfx.ogg" -loglevel error
if %ERRORLEVEL% equ 0 (
    for %%A in ("%SFX_DIR%\test_long_sfx.ogg") do echo [OK] test_long_sfx.ogg created (%%~zA bytes)
) else (
    echo [ERROR] Failed to create test_long_sfx.ogg
)

echo.
echo ==========================================
echo Setup Complete!
echo ==========================================
echo.
echo Generated files:
echo   %TEST_DATA_DIR%\test_music.ogg
echo   %TEST_DATA_DIR%\test_loop.ogg
echo   %SFX_DIR%\test_sfx.ogg
echo   %SFX_DIR%\test_long_sfx.ogg
echo.
echo You can now run the audio streaming tests:
echo   dotnet test --filter "FullyQualifiedName~Streaming"
echo.

endlocal
