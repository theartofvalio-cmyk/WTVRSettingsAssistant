from __future__ import annotations

import argparse
import hashlib
import json
import subprocess
from pathlib import Path


EXPECTED_VERSION = "2.0.6"
EXPECTED_INSTALLER_SHA256 = "EF569A3105CD301B89580F18F60C66B339E95296ACF2C0DFCAF4B4BBF8AB68FE"
EXPECTED_NATIVE_SHA256 = "D746C531DD8D66460B2F3544B8D7BF6C242C3560884DFE1BF5DB0B8002669EC3"


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest().upper()


def read_manifest(path: Path) -> dict[str, str]:
    if not path.exists():
        return {}
    return json.loads(path.read_text(encoding="utf-8"))


def file_version(exe: Path) -> str:
    script = "& { param($p) (Get-Item -LiteralPath $p).VersionInfo.FileVersion }"
    result = subprocess.run(
        ["powershell", "-NoProfile", "-Command", script, str(exe)],
        check=True,
        text=True,
        capture_output=True,
    )
    return result.stdout.strip()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("release", type=Path)
    parser.add_argument("--before", type=Path)
    parser.add_argument("--after", type=Path)
    args = parser.parse_args()

    release = args.release
    exe = release / "WTVRSettingsAssistant.exe"
    installer = release / "Drivers" / "vJoy" / "vJoySetup.exe"
    native = release / "vJoyInterface.dll"
    single_file_exe = exe.exists() and exe.stat().st_size > 50 * 1024 * 1024

    checks = [
        exe.exists(),
        file_version(exe).startswith(EXPECTED_VERSION),
        installer.exists() and sha256(installer) == EXPECTED_INSTALLER_SHA256,
        native.exists() and sha256(native) == EXPECTED_NATIVE_SHA256,
        (release / "VTrim.Embedded.dll").exists() or single_file_exe,
    ]

    before = read_manifest(args.before) if args.before else {}
    after = read_manifest(args.after) if args.after else {}
    if before or after:
        checks.append(before == after)

    if not all(checks):
        print("FAIL: release verification failed")
        return 1
    print("PASS: release 2.0.6 payload, vJoy files and preserved user folders verified.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
