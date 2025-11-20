#!/usr/bin/env bash
# record_demo.sh - Instructions for creating a demonstration recording

cat << 'EOF'
SocketGhost Demo Recording Instructions
========================================

To create a professional demo recording, use one of the following methods:

Method 1: Using OBS Studio (Recommended)
-----------------------------------------
1. Download OBS Studio: https://obsproject.com/
2. Settings:
   - Video: 1920x1080, 30 FPS
   - Output: MP4, Quality preset "High Quality, Medium File Size"
3. Recording Steps:
   a. Start SocketGhost Core
   b. Open SocketGhost UI
   c. Select a process (e.g., browser)
   d. Enable interceptor
   e. Make an HTTPS request (e.g., https://httpbin.org/get)
   f. Show flow in Dashboard
   g. Click to view flow details
   h. Navigate to Saved Flows tab
   i. Show captured flow
   j. Click Replay with confirmation modal
4. Export as MP4
5. Convert to GIF (optional): ffmpeg -i demo.mp4 -vf "fps=10,scale=800:-1" demo.gif

Method 2: Using ffmpeg (Command-line)
--------------------------------------
# Windows
ffmpeg -f gdigrab -framerate 30 -i desktop -t 20 -vf "crop=1920:1080:0:0" demo.mp4

# macOS
ffmpeg -f avfoundation -i "1" -t 20 demo.mp4

# Linux
ffmpeg -video_size 1920x1080 -framerate 30 -f x11grab -i :0.0 -t 20 demo.mp4

# Convert to GIF
ffmpeg -i demo.mp4 -vf "fps=10,scale=800:-1:flags=lanczos,palettegen" palette.png
ffmpeg -i demo.mp4 -i palette.png -filter_complex "fps=10,scale=800:-1:flags=lanczos[x];[x][1:v]paletteuse" demo.gif

Method 3: Using ScreenToGif (Windows)
--------------------------------------
1. Download: https://www.screentogif.com/
2. Record → Select region → Start recording
3. Stop after 20 seconds
4. Edit → Save as GIF or MP4

Method 4: Using Kap (macOS)
----------------------------
1. Download: https://getkap.co/
2. Select window or monitor
3. Record demo workflow
4. Export as MP4 or GIF

Recommended Demo Workflow (20 seconds)
----------------------------------------
0:00 - Show SocketGhost UI dashboard
0:02 - Select "node" process from list
0:04 - Enable interceptor toggle
0:06 - Switch to terminal, run: curl -x http://127.0.0.1:8080 https://httpbin.org/get
0:08 - Show flow appears in Dashboard
0:10 - Click flow to view details
0:12 - Navigate to "Saved Flows" tab
0:14 - Show flow in list
0:16 - Click "Replay" button
0:18 - Show confirmation modal
0:20 - End recording

Output Location
----------------
Save final demo as: docs/demo.gif (max 5MB for GitHub README)

Alternative: Upload to YouTube and embed link in README.

Notes
------
- Keep recording under 20 seconds for easy sharing
- Focus on UI interactions, not terminal commands
- Use high contrast theme for readability
- Annotate with text overlays if needed (using video editing software)

EOF
