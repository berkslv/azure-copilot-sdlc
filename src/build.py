#!/usr/bin/env python3
"""
PyInstaller build script for azure-copilot-sdlc CLI tool.

Usage:
    python build.py          # Build single executable using spec file
    python build.py --onedir # Build as directory with dependencies
    python build.py --clean  # Clean build artifacts
"""

import argparse
import shutil
import subprocess
import sys
from pathlib import Path


def get_pyinstaller_args(one_dir: bool = False) -> list[str]:
    """Generate PyInstaller command arguments."""
    spec_file = Path("azure-copilot-sdlc.spec")
    
    if one_dir and spec_file.exists():
        # Modify spec file temporarily for onedir build
        args = [
            "pyinstaller",
            "--onedir",
            "--clean",
            str(spec_file),
        ]
    else:
        # Use spec file for default single-file build
        args = [
            "pyinstaller",
            "--onefile" if not one_dir else "--onedir",
            "--clean",
            str(spec_file) if spec_file.exists() else "cli.py",
        ]
    
    return args


def clean_build_artifacts():
    """Remove build artifacts."""
    artifacts = ["build", "dist", "azure_copilot_sdlc.spec", "__pycache__"]
    for artifact in artifacts:
        artifact_path = Path(artifact)
        if artifact_path.exists():
            if artifact_path.is_dir():
                shutil.rmtree(artifact_path)
                print(f"Removed directory: {artifact}")
            else:
                artifact_path.unlink()
                print(f"Removed file: {artifact}")


def build(one_dir: bool = False, clean_first: bool = True):
    """Build the executable using PyInstaller."""
    if clean_first:
        print("Cleaning previous builds...")
        clean_build_artifacts()
    
    args = get_pyinstaller_args(one_dir=one_dir)
    
    exe_type = "directory-based" if one_dir else "single-file"
    print(f"\nBuilding {exe_type} executable...")
    print(f"Command: {' '.join(args)}\n")
    
    result = subprocess.run(args, cwd=Path(__file__).parent)
    
    if result.returncode == 0:
        output_dir = "dist"
        exe_name = "azure-copilot-sdlc.exe" if sys.platform == "win32" else "azure-copilot-sdlc"
        exe_path = Path(output_dir) / (exe_name if one_dir else exe_name)
        print(f"\n✓ Build successful!")
        print(f"✓ {exe_type.capitalize()} executable created in '{output_dir}' directory")
        if one_dir:
            print(f"✓ Run with: ./{output_dir}/azure-copilot-sdlc/azure-copilot-sdlc --help")
        else:
            print(f"✓ Run with: ./{output_dir}/{exe_name} --help")
    else:
        print(f"\n✗ Build failed with exit code {result.returncode}")
        sys.exit(1)


def main():
    """Main entry point."""
    parser = argparse.ArgumentParser(
        description="Build azure-copilot-sdlc executable with PyInstaller"
    )
    parser.add_argument(
        "--onedir",
        action="store_true",
        help="Build as directory with dependencies (default: single-file executable)"
    )
    parser.add_argument(
        "--no-clean",
        action="store_true",
        help="Don't clean previous builds before building"
    )
    parser.add_argument(
        "--clean",
        action="store_true",
        help="Only clean build artifacts and exit"
    )
    
    args = parser.parse_args()
    
    if args.clean:
        print("Cleaning build artifacts...")
        clean_build_artifacts()
        print("Done!")
        return
    
    build(one_dir=args.onedir, clean_first=not args.no_clean)


if __name__ == "__main__":
    main()
