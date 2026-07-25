#!/usr/bin/env python3
"""
Groundwork ASCII Lifecycle Animation

Reads the debug visualization output from /tmp/groundwork_viz.txt,
splits it into frames, and plays them as a terminal animation.

Usage:
    python3 scripts/animate_viz.py                  # Play existing viz frames
    python3 scripts/animate_viz.py --run            # Clear, run sim, animate
    python3 scripts/animate_viz.py --delay 0.05     # Faster (50ms per frame)
    python3 scripts/animate_viz.py --loop           # Loop until Ctrl+C
    python3 scripts/animate_viz.py --record a.cast  # Save asciicast v2
"""

import argparse
import os
import subprocess
import sys
import time

UNITY_PATH = os.path.expanduser("~/Unity/Hub/Editor/6000.3.20f1/Editor/Unity")
PROJECT_PATH = "/home/tim/source/activity/Groundwork"
VIZ_FILE = "/tmp/groundwork_viz.txt"
TITLE_LINE = "|      Groundwork Debug Visualization           |"


def clear_terminal():
    sys.stdout.write("\033[2J\033[H")
    sys.stdout.flush()


def run_simulation():
    """Run Unity headless. Returns True on success."""
    print("Running Groundwork headless (100 ticks)...")

    if os.path.exists(VIZ_FILE):
        os.remove(VIZ_FILE)

    cmd = [
        UNITY_PATH,
        "-batchmode", "-nographics", "-quit",
        "-projectPath", PROJECT_PATH,
        "-executeMethod", "Groundwork.Simulation.HeadlessRunner.Run",
    ]

    print("  Starting Unity (~15-30s)...")
    result = subprocess.run(cmd, capture_output=True, text=True, timeout=300)

    if result.returncode != 0:
        print(f"  Unity exited with code {result.returncode}")
        stderr_lines = result.stderr.strip().split("\n")
        for line in stderr_lines[-20:]:
            print(f"  [unity] {line}")
        return False

    if not os.path.exists(VIZ_FILE):
        print("  ERROR: No viz output generated.")
        return False

    print(f"  Done: {os.path.getsize(VIZ_FILE)} bytes")
    return True


def parse_frames(filepath):
    """Extract complete frames from viz file.
    
    Each frame is: top border, title, header border, grid rows, 
    bottom border, legend. We split on the title line since it's 
    unique per frame.
    """
    with open(filepath) as f:
        content = f.read()

    frames = []
    current = []

    for line in content.split("\n"):
        if line.strip() == TITLE_LINE and current:
            # Save previous frame, start new one
            frames.append("\n".join(current))
            current = [line]
        elif line.strip() == TITLE_LINE and not current:
            # First frame
            current = [line]
        elif current:
            current.append(line)

    if current:
        frames.append("\n".join(current))

    # Reconstruct frames with top border
    full_frames = []
    for f in frames:
        full_frames.append("+----------------------------------------------+\n" + f)

    return full_frames


def animate(frames, delay=0.1, loop=False, record_path=None):
    """Play frames as terminal animation with ANSI clear-screen."""
    try:
        max_width = max(len(line) for f in frames for line in f.split("\n")) if frames else 80
        max_height = max(len(f.split("\n")) for f in frames) if frames else 30
        term_h = os.get_terminal_size().lines

        print(f"Frames: {len(frames)}  Delay: {delay:.3f}s  " +
              f"Size: {max_width}x{max_height}")
        print("Starting in 2s... (Ctrl+C to stop)")
        time.sleep(2)

        while True:
            for i, frame in enumerate(frames):
                clear_terminal()
                sys.stdout.write(frame)
                sys.stdout.write(f"\n\n  Frame {i+1}/{len(frames)}")
                sys.stdout.flush()
                time.sleep(delay)

            if not loop:
                break
            time.sleep(0.5)

    except KeyboardInterrupt:
        clear_terminal()
        print("Animation stopped.")

    if record_path:
        print(f"\nRecord: --record not yet implemented (install asciinema for now)")


def main():
    parser = argparse.ArgumentParser(description="Groundwork ASCII Lifecycle Animation")
    parser.add_argument("--run", action="store_true", help="Run sim first")
    parser.add_argument("--delay", type=float, default=0.1, help="Seconds per frame")
    parser.add_argument("--loop", action="store_true", help="Loop until Ctrl+C")
    parser.add_argument("--record", type=str, metavar="PATH", help="Save asciicast")
    parser.add_argument("--file", type=str, default=VIZ_FILE, help="Viz file path")
    args = parser.parse_args()

    if args.run:
        if not run_simulation():
            print("ERROR: Simulation failed.")
            sys.exit(1)

    if not os.path.exists(args.file):
        print(f"ERROR: No viz file at {args.file}")
        print("Use --run to generate one.")
        sys.exit(1)

    frames = parse_frames(args.file)
    if not frames:
        print(f"ERROR: No frames in {args.file}")
        sys.exit(1)

    animate(frames, delay=args.delay, loop=args.loop, record_path=args.record)


if __name__ == "__main__":
    main()