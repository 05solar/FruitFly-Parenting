"""Record actual isolated-project inputs and player hashes without claiming a clean commit."""
import argparse
import hashlib
import json
import subprocess
from datetime import datetime, timezone
from pathlib import Path


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def write_manifest(project, output, repository):
    inputs = {}
    for folder in ("Assets", "Packages", "ProjectSettings"):
        for path in sorted((project / folder).rglob("*")):
            if path.is_file():
                inputs[path.relative_to(project).as_posix()] = digest(path)
    combined = hashlib.sha256(json.dumps(inputs, sort_keys=True).encode()).hexdigest()
    artifacts = {}
    for relative in ("FruitFlyParenting.exe", "FruitFlyParenting_Data/globalgamemanagers",
                     "FruitFlyParenting_Data/Managed/FruitFlyParenting.Game.dll"):
        path = output / relative
        if not path.is_file():
            raise FileNotFoundError(path)
        artifacts[relative] = digest(path)
    def git(*args):
        return subprocess.check_output(["git", "-C", str(repository), *args], encoding="utf-8").strip()
    manifest = {
        "generatedUtc": datetime.now(timezone.utc).isoformat(),
        "sourceCommit": git("rev-parse", "HEAD"),
        "sourceHasUncommittedChanges": bool(git("status", "--porcelain")),
        "unityVersion": "6000.6.2f1", "target": "StandaloneWindows64",
        "settingsBaseline": "isolated OpenXR/XRI PC project; user Meta XR settings retained in repository",
        "inputFileCount": len(inputs), "inputSetSha256": combined,
        "inputHashes": inputs, "artifactHashes": artifacts,
        "humanAcceptance": "pending",
    }
    (output / "build-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    return {key: value for key, value in manifest.items() if key != "inputHashes"}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("project", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--repository", type=Path, default=Path.cwd())
    args = parser.parse_args()
    print(json.dumps(write_manifest(args.project, args.output, args.repository), indent=2))
