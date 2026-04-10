# FFmpeg Setup for Smart Dash

## What is this folder for?

This folder contains FFmpeg, which Smart Dash uses to convert your camera's RTSPS stream into HLS format that your browser can play.

## Installation Steps:

1. **Download FFmpeg:**
   - Go to: https://www.gyan.dev/ffmpeg/builds/
   - Download: **ffmpeg-release-essentials.zip**

2. **Extract:**
   - Unzip the downloaded file
   - Navigate to the `bin/` folder inside

3. **Copy ffmpeg.exe here:**
   - Copy `ffmpeg.exe` from the `bin/` folder
   - Paste it directly in THIS folder (Backend/ffmpeg/)

## Verification:

After copying, this folder should contain:
- `ffmpeg.exe` ← The actual FFmpeg executable
- `README.md` ← This file

## How it works:

When you click **LIVE** mode on your camera:
1. Smart Dash starts FFmpeg in the background
2. FFmpeg connects to your UniFi camera (RTSPS)
3. FFmpeg converts the stream to HLS format
4. Your browser plays the HLS stream
5. When you switch modes, FFmpeg stops automatically

**No external dependencies needed - everything runs in Smart Dash!**
