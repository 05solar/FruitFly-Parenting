"""Check packaged IL2CPP metadata and ARM64 binaries; does not claim device execution."""
import argparse
import hashlib
import json
import re
import subprocess
import zipfile
from pathlib import Path


def inspect(apk, aapt, development=False):
    badging = subprocess.check_output([str(aapt), "dump", "badging", str(apk)], text=True, encoding="utf-8")
    manifest = subprocess.check_output([str(aapt), "dump", "xmltree", str(apk), "--file", "AndroidManifest.xml"], text=True, encoding="utf-8")
    with zipfile.ZipFile(apk) as archive:
        names = archive.namelist()
        metadata = archive.read("assets/bin/Data/Managed/Metadata/global-metadata.dat")
        abis = sorted({name.split("/")[1] for name in names if name.startswith("lib/") and name.endswith(".so")})
        excluded = {name: name.encode() not in metadata for name in (
            "DebugHud", "RoomCoordinateGizmos", "FruitFlyParenting.Connectome", "ConnectomeViewer")}
        capture = b"QuestPerformanceCapture" in metadata
        runtime = b"QuestRuntime" in metadata
        debuggable = re.search(r"android:debuggable\([^)]*\)=(?:true|\(type 0x12\)0xffffffff)", manifest) is not None
        checks = {
            "arm64_only": abis == ["arm64-v8a"],
            "il2cpp": "lib/arm64-v8a/libil2cpp.so" in names,
            "runtime_present": runtime,
            "profiling_matches_build_type": capture == development,
            "debuggable_matches_build_type": debuggable == development,
            "quest_head_tracking": "android.hardware.vr.headtracking" in badging,
            "package_id": "name='com.fruitflyparenting.game'" in badging,
            "min_api_29": "minSdkVersion:'29'" in badging,
            "target_api_34": "targetSdkVersion:'34'" in badging,
            **{name + "_excluded": value for name, value in excluded.items()},
        }
    with apk.open("rb") as source:
        digest = hashlib.file_digest(source, "sha256").hexdigest()
    return {"apk": apk.name, "bytes": apk.stat().st_size, "sha256": digest,
            "development": development, "abis": abis, "checks": checks, "passed": all(checks.values()),
            "device_installation": "not tested: no connected HMD"}


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("apk", type=Path)
    parser.add_argument("--aapt", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--development", action="store_true")
    args = parser.parse_args()
    result = inspect(args.apk, args.aapt, args.development)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(result, indent=2))
    raise SystemExit(0 if result["passed"] else 1)
