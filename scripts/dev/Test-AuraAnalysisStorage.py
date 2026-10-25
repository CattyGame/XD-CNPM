"""Guarded Analysis adapter checks against the existing local Azurite. Default is check-only."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import uuid

BASE_COMMIT = "87af21da20489eda6d4899f7e1f3a428b24960db"
PROJECT = "tests/AURA.Analysis.StorageChecks/AURA.Analysis.StorageChecks.csproj"
ENV_FILE = "infra/storage/storage.host.env.example"
ENV_HASH = "bb61b9fd9aa70db10b113691e2635a4eba06c55a08fb061e6f13e67771a71640"
SOURCES = [ENV_FILE, PROJECT, "tests/AURA.Analysis.StorageChecks/Program.cs",
           "src/services/AURA.Analysis.Api/AURA.Analysis.Api.csproj",
           "src/services/AURA.Analysis.Api/Upload/AzureBlobPrivateFileStorage.cs"]
RESULT = "RESULT: 12/12 Azurite adapter groups passed; four fixture blobs removed."


class CheckError(Exception):
    pass


def require(ok, message):
    if not ok:
        raise CheckError(message)


def command(argv, repo, env=None, timeout=30):
    try:
        return subprocess.run(argv, cwd=repo, env=env, capture_output=True, text=True,
                              encoding="utf-8", errors="replace", timeout=timeout, check=False)
    except subprocess.TimeoutExpired as exc:
        raise CheckError("Command timed out; outcome may be unknown. No automatic retry.") from exc


def hashes(repo):
    return {p: hashlib.sha256((repo / p).read_text(encoding="utf-8-sig").encode()).hexdigest() for p in SOURCES}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run", action="store_true")
    parser.add_argument("--repo", type=Path, default=Path.cwd())
    parser.add_argument("--container", default="aura-azurite-1")
    args = parser.parse_args()
    repo = args.repo.resolve()
    report = evidence = None

    def save():
        tmp = evidence.with_suffix(".tmp")
        tmp.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        os.replace(tmp, evidence)

    def invoke(label, argv, env=None, timeout=30):
        step = None
        if report is not None:
            step = {"name": label, "status": "PENDING"}
            report["steps"].append(step)
            save()
        try:
            result = command(argv, repo, env=env, timeout=timeout)
        except CheckError:
            if step is not None:
                step["status"] = "UNKNOWN"
                save()
            raise
        # Console checks intentionally never output SDK exception messages or URLs.
        if step is not None:
            step.update(status="PASS" if result.returncode == 0 else "FAIL", exitCode=result.returncode,
                        stdout=result.stdout, stderr=result.stderr)
            save()
        if result.returncode != 0:
            print(result.stdout[-6000:], file=sys.stderr)
            print(result.stderr[-6000:], file=sys.stderr)
        require(result.returncode == 0, label + " failed; later steps stopped.")
        return result.stdout.strip()

    try:
        require((repo / "AURA.sln").is_file(), "Run from the repository root.")
        invoke("Baseline ancestor", ["git", "merge-base", "--is-ancestor", BASE_COMMIT, "HEAD"])
        head = invoke("Source commit", ["git", "rev-parse", "HEAD"])
        source = hashes(repo)
        require(source[ENV_FILE] == ENV_HASH, "Local storage example changed; refusing an unreviewed storage target.")
        labels = json.loads(invoke("Container labels", ["docker", "inspect", "--format", "{{json .Config.Labels}}", args.container]))
        require(isinstance(labels, dict) and labels.get("com.docker.compose.project") == "aura"
                and labels.get("com.docker.compose.service") == "azurite", "Wrong Compose Azurite container.")
        image = invoke("Emulator image", ["docker", "inspect", "--format", "{{.Config.Image}}", args.container])
        require(image == "mcr.microsoft.com/azure-storage/azurite:3.33.0", "Expected the pinned local Azurite 3.33.0 image.")
        require(invoke("Emulator state", ["docker", "inspect", "--format", "{{.State.Running}}", args.container]) == "true",
                "Azurite is not running; start the existing service and rerun the preflight.")
        ports = invoke("Loopback port", ["docker", "port", args.container, "10000/tcp"]).splitlines()
        require(ports == ["127.0.0.1:10000"], "Azurite must expose only IPv4 loopback port 10000 for this fixture.")
        require(invoke(".NET SDK", ["dotnet", "--version"]).split('.')[0] == "10", ".NET SDK 10 is required.")
        print("PASS: exact local emulator configuration, Compose container, loopback port and .NET SDK verified.")
        if not args.run:
            print("CHECK ONLY: no build, blob upload/delete or SAS request was sent.")
            return 0
        run = uuid.uuid4().hex
        first = str(uuid.UUID(hex=run))
        second = str(uuid.UUID(bytes_le=hashlib.sha256((run + ":sibling").encode()).digest()[:16]))
        report = {"runId": run, "createdAt": datetime.now(timezone.utc).isoformat(), "sourceCommit": head,
                  "sourceHashes": source, "endpoint": "http://127.0.0.1:10000/devstoreaccount1",
                  "fixtureBlobs": [{"container": c, "key": f"{first}/{first}/{last}"}
                                   for c in ("aura-originals", "aura-annotations") for last in (first, second)],
                  "cleanup": "Only these four UUID blobs; no container, ACL, volume or application DB changes.",
                  "status": "PENDING", "steps": []}
        folder = Path.home() / ".aura" / "checks"
        folder.mkdir(parents=True, exist_ok=True)
        evidence = folder / ("m2-analysis-storage-" + run + ".json")
        require(not evidence.exists(), "Fixture plan already exists.")
        save()
        print("Plan saved before requests: " + str(evidence))
        output = invoke("Build storage checks", ["dotnet", "build", PROJECT, "--configuration", "Release", "--nologo"], timeout=120)
        print(output)
        require(hashes(repo) == source, "Source changed during build; no fixture blobs were created.")
        child = dict(os.environ, AURA_ANALYSIS_STORAGE_TEST_RUN=run)
        output = invoke("Azurite adapter checks", ["dotnet", "run", "--project", PROJECT, "--configuration", "Release",
                                                   "--no-build", "--no-restore"], env=child, timeout=100)
        print(output)
        require(RESULT in output.splitlines(), "Missing complete adapter result; inspect saved fixture plan.")
        report["status"] = "PASS"
        save()
        print("Evidence: " + str(evidence))
        return 0
    except Exception as exc:
        if report is not None:
            report["status"] = "STOPPED"
            save()
        print("STOP: " + (str(exc) if isinstance(exc, CheckError) else type(exc).__name__), file=sys.stderr)
        if evidence:
            print("Evidence: " + str(evidence), file=sys.stderr)
            print("Review the four saved UUID keys after failure/timeout; do not delete any container or volume.", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
