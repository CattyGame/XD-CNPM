"""Build and check Analysis repositories on an isolated PostgreSQL 17 database. Default is check-only."""
import argparse
from datetime import datetime, timezone
import getpass
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import uuid

BASE_COMMIT = "87af21da20489eda6d4899f7e1f3a428b24960db"
PROJECT = "tests/AURA.Analysis.PersistenceChecks/AURA.Analysis.PersistenceChecks.csproj"
MIGRATION = "infra/postgres/migrations/analysis/001_analysis_core.sql"
ASSERTIONS = "infra/postgres/migrations/analysis/tests/test_001.sql"
SQL_HASHES = {MIGRATION: "68ef4480d6fa6bd862129f5491454db0a89800affec77049792a9c6f92517fa7",
              ASSERTIONS: "8c6b41c038af95b846cb6501dc6525c8c05b8fe06b031fcda09b10daa7d4a44f"}
SOURCES = [MIGRATION, ASSERTIONS, PROJECT,
           "tests/AURA.Analysis.PersistenceChecks/Program.cs",
           "src/services/AURA.Analysis.Api/AURA.Analysis.Api.csproj",
           "src/services/AURA.Analysis.Api/Upload/PostgresUploadRepository.cs",
           "src/services/AURA.Analysis.Api/Upload/PostgresFileAccessRepository.cs"]


class CheckError(Exception):
    pass


def require(condition, message):
    if not condition:
        raise CheckError(message)


def command(argv, repo, body=None, env=None, timeout=45):
    try:
        return subprocess.run(argv, cwd=repo, input=body, capture_output=True, text=True,
                              encoding="utf-8", errors="replace", env=env, timeout=timeout, check=False)
    except subprocess.TimeoutExpired as exc:
        raise CheckError("Command timed out; outcome may be unknown. No automatic retry.") from exc


class Runner:
    def __init__(self, repo, container):
        self.repo, self.container = repo, container
        self.report = self.evidence = None

    def save(self):
        tmp = self.evidence.with_suffix(".tmp")
        tmp.write_text(json.dumps(self.report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        os.replace(tmp, self.evidence)

    def invoke(self, label, argv, body=None, env=None, timeout=45):
        step = None
        if self.report is not None:
            step = {"name": label, "status": "PENDING"}
            self.report["steps"].append(step)
            self.save()
        try:
            result = command(argv, self.repo, body, env, timeout)
        except CheckError:
            if step is not None:
                step["status"] = "UNKNOWN"
                self.save()
            raise
        if step is not None:
            step.update(status="PASS" if result.returncode == 0 else "FAIL", exitCode=result.returncode,
                        stdout=result.stdout, stderr=result.stderr)
            self.save()
        if result.returncode != 0:
            if result.stdout:
                print(result.stdout[-6000:], file=sys.stderr)
            if result.stderr:
                print(result.stderr[-6000:], file=sys.stderr)
        require(result.returncode == 0, label + " failed; review saved evidence. Later steps stopped.")
        return result.stdout.strip()

    def sql(self, database, text, label, scalar=False):
        require(database == "postgres" or re.fullmatch(r"aura_m2_repo_[0-9a-f]{32}", database), "Refusing a non-test database.")
        argv = ["docker", "exec", "-i", self.container, "psql", "-X", "--no-password", "-v", "ON_ERROR_STOP=1",
                "-P", "pager=off", "-U", "aura_dev", "-d", database]
        if scalar:
            argv += ["-A", "-t", "-q"]
        return self.invoke(label, argv, text)


def source_hashes(repo):
    return {path: hashlib.sha256((repo / path).read_text(encoding="utf-8-sig").encode()).hexdigest() for path in SOURCES}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run", action="store_true")
    parser.add_argument("--repo", type=Path, default=Path.cwd())
    parser.add_argument("--container", default="aura-postgres-1")
    args = parser.parse_args()
    repo = args.repo.resolve()
    runner = Runner(repo, args.container)
    password = None
    child_env = None
    try:
        require((repo / "AURA.sln").is_file(), "Run from the repository root.")
        runner.invoke("Baseline ancestor", ["git", "merge-base", "--is-ancestor", BASE_COMMIT, "HEAD"])
        head = runner.invoke("Source commit", ["git", "rev-parse", "HEAD"])
        hashes = source_hashes(repo)
        require(all(hashes[p] == h for p, h in SQL_HASHES.items()), "SQL source changed; review the fixture runner.")
        labels = json.loads(runner.invoke("Container labels", ["docker", "inspect", "--format", "{{json .Config.Labels}}", args.container]))
        require(isinstance(labels, dict) and labels.get("com.docker.compose.project") == "aura"
                and labels.get("com.docker.compose.service") == "postgres", "Wrong Compose PostgreSQL container.")
        version = int(runner.sql("postgres", "SELECT current_setting('server_version_num');\n", "PostgreSQL version", scalar=True))
        require(version // 10000 == 17, "PostgreSQL 17 is required.")
        role = json.loads(runner.sql("postgres", """
SELECT json_build_object('login',rolcanlogin,'superuser',rolsuper,'createdb',rolcreatedb,'createrole',rolcreaterole)
FROM pg_roles WHERE rolname='aura_analysis_app';
""", "Runtime role", scalar=True))
        require(role.get("login") and not any(role.get(k) for k in ("superuser", "createdb", "createrole")), "Runtime role is missing or has administrative privileges.")
        mapping = runner.invoke("Loopback port", ["docker", "port", args.container, "5432/tcp"])
        match = re.search(r"(?m)^127\.0\.0\.1:(\d+)\s*$", mapping)
        require(match is not None, "PostgreSQL 5432 must have a loopback IPv4 host mapping.")
        port = int(match.group(1))
        require(1 <= port <= 65535, "Invalid mapped PostgreSQL port.")
        runner.invoke(".NET SDK", ["dotnet", "--version"])
        print("PASS: sources, PostgreSQL 17, restricted runtime role, loopback port and .NET SDK are ready.")
        if not args.run:
            print("CHECK ONLY: no build, database creation or repository check was executed.")
            return 0

        password = getpass.getpass("Mat khau PostgreSQL cua aura_analysis_app (an): ")
        require(bool(password), "Runtime password must not be empty.")
        check_id = uuid.uuid4().hex
        database, marker = "aura_m2_repo_" + check_id, "aura-m2-repo-check:" + check_id
        directory = Path.home() / ".aura" / "checks"
        directory.mkdir(parents=True, exist_ok=True)
        runner.evidence = directory / ("m2-analysis-persistence-" + check_id + ".json")
        runner.report = {"schemaVersion": 1, "startedAt": datetime.now(timezone.utc).isoformat(), "checkId": check_id,
                         "headCommit": head, "sourceHashes": hashes, "container": args.container,
                         "testDatabase": database, "status": "RUNNING", "cleanup": "NOT_ATTEMPTED", "steps": []}
        with runner.evidence.open("x", encoding="utf-8") as f:
            json.dump(runner.report, f, ensure_ascii=False, indent=2)
        print("Plan saved before build/database creation: " + str(runner.evidence))
        runner.invoke("Build repository checks", ["dotnet", "build", PROJECT, "--configuration", "Release"], timeout=120)
        require(source_hashes(repo) == hashes, "Source changed during build; no database was created.")
        print("PASS: repository checks build succeeded.")
        runner.sql("postgres", f'CREATE DATABASE "{database}" OWNER aura_dev TEMPLATE template0;\n', "Create isolated database")
        runner.sql("postgres", f'COMMENT ON DATABASE "{database}" IS \'{marker}\';\n', "Mark isolated database")
        for n in (1, 2):
            runner.sql(database, (repo / MIGRATION).read_text(encoding="utf-8-sig"), "Migration pass " + str(n))
        runner.sql(database, (repo / ASSERTIONS).read_text(encoding="utf-8-sig"), "Schema assertions")
        print("PASS: isolated database ready; migration twice and schema assertions passed.")
        # Only the child receives the connection string; it is never an argument, report field or console output.
        quoted_password = '"' + password.replace('"', '""') + '"'
        connection = (f"Host=127.0.0.1;Port={port};Database={database};Username=aura_analysis_app;"
                      f"Password={quoted_password};Timeout=5;Command Timeout=10;Include Error Detail=false")
        child_env = dict(os.environ)
        child_env["AURA_ANALYSIS_PERSISTENCE_TEST_CONNECTION"] = connection
        output = runner.invoke("Actual runtime repository checks", ["dotnet", "run", "--project", PROJECT,
                               "--configuration", "Release", "--no-build", "--no-restore"], env=child_env, timeout=90)
        require("RESULT: 13/13 PostgreSQL repository groups passed with aura_analysis_app." in output,
                "Repository checks did not report all expected groups.")
        print(output)
        owned = runner.sql("postgres", f"SELECT coalesce(shobj_description(oid,'pg_database'),'') FROM pg_database WHERE datname='{database}';\n", "Verify cleanup marker", scalar=True)
        require(owned == marker, "Test database marker changed; retain for review.")
        runner.report["cleanup"] = "PENDING"
        runner.save()
        runner.sql("postgres", f'DROP DATABASE "{database}";\n', "Remove isolated database")
        remaining = runner.sql("postgres", f"SELECT count(*) FROM pg_database WHERE datname='{database}';\n", "Verify cleanup", scalar=True)
        require(remaining == "0", "Cannot confirm test database cleanup.")
        runner.report.update(status="PASS", cleanup="CONFIRMED", finishedAt=datetime.now(timezone.utc).isoformat())
        runner.save()
        print("PASS: isolated repository test database removed.")
        print("RESULT: PostgreSQL repository checks passed; application databases and global roles were not modified.")
        print("Evidence: " + str(runner.evidence))
        return 0
    except (CheckError, OSError, ValueError, KeyError) as exc:
        if runner.report is not None:
            runner.report.update(status="FAILED_OR_UNKNOWN", failure=str(exc), finishedAt=datetime.now(timezone.utc).isoformat())
            runner.save()
            print("Evidence: " + str(runner.evidence), file=sys.stderr)
            print("Failed/unknown runs retain the named test database for review; do not delete application data.", file=sys.stderr)
        print("STOP: " + str(exc), file=sys.stderr)
        return 1
    finally:
        if child_env is not None:
            child_env.pop("AURA_ANALYSIS_PERSISTENCE_TEST_CONNECTION", None)
        password = None


if __name__ == "__main__":
    sys.exit(main())
