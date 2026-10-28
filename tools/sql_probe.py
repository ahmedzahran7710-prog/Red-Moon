#!/usr/bin/env python3
"""CI-only import of original SQL into an EMPTY, disposable local MariaDB service.

Raw SQL/stdout/stderr are NOT printed or uploaded: seed account data is not report data.
This is not a migration tool for the owner's PC. Do not invoke against an existing DB.
"""
import argparse
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    args = parser.parse_args()
    if os.environ.get("GITHUB_ACTIONS") != "true" or os.environ.get("FOX_DISPOSABLE_SQL") != "1":
        sys.exit("Refusing SQL execution outside the explicitly isolated CI job")
    client = shutil.which("mysql") or shutil.which("mariadb")
    if not client:
        sys.exit("No SQL command-line client installed")
    command = [client, "--host=127.0.0.1", "--port=3306", "--protocol=TCP", "--user=root",
               "--batch", "--skip-column-names", "--default-character-set=utf8mb4"]

    def run(sql, schema=None):
        return subprocess.run(command + ([schema] if schema else []), input=sql,
                              stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=120)

    def query(sql, schema=None):
        result = run(sql.encode("utf-8"), schema)
        if result.returncode:
            raise RuntimeError("Metadata query failed; raw error withheld to protect seed values")
        return result.stdout.decode("utf-8").strip()

    existing = query("SELECT SCHEMA_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME NOT IN ('mysql','information_schema','performance_schema','sys');")
    if existing:
        sys.exit("Refusing import: the service is not empty")
    version = query("SELECT VERSION();")
    if not version.startswith("10.4.32-MariaDB"):
        sys.exit("Expected MariaDB 10.4.32; will not test a different server silently")
    query("CREATE DATABASE accountzf CHARACTER SET utf8mb4; CREATE DATABASE zf CHARACTER SET utf8mb4;")
    results = []
    baseline_ok = True
    for filename, schema in [("accountzf.sql", "accountzf"), ("zf.sql", "zf"),
                             ("fix_cq_levexp.sql", "zf"), ("cq_action_mod.sql", "zf")]:
        if not baseline_ok:
            results.append({"file": filename, "status": "SKIPPED", "reason": "base import failed"})
            continue
        raw = (args.source / "Database" / filename).read_bytes()
        # The archive is hash-pinned; this additional guard is intentionally conservative.
        text = raw.decode("utf-8-sig")
        unsafe = re.search(r"(?im)^\s*(?:USE\s|(?:CREATE|DROP)\s+DATABASE\s|(?:CREATE|ALTER)\s+USER\s|GRANT\s|INSTALL\s+PLUGIN\s|LOAD\s+DATA\s|SET\s+GLOBAL\s)", text)
        if unsafe:
            raise RuntimeError("Import stopped at an unexpected administrative SQL statement")
        result = run(raw, schema)
        entry = {"file": filename, "status": "PASS" if result.returncode == 0 else "FAIL", "exit_code": result.returncode}
        if result.returncode:
            error = re.search(rb"ERROR (\d+) \(([A-Z0-9]+)\)(?: at line (\d+))?", result.stderr)
            if error:
                entry.update({"error_code": error.group(1).decode(), "sqlstate": error.group(2).decode(),
                              "line": int(error.group(3)) if error.group(3) else None})
            entry["raw_error_withheld"] = True
            if filename in ("accountzf.sql", "zf.sql"):
                baseline_ok = False
        results.append(entry)
    counts = {}
    if baseline_ok:
        for schema, tables in {"accountzf": ["account"], "zf": ["cq_user", "cq_item", "cq_arena", "cq_log", "cq_levexp"]}.items():
            counts[schema + ".tables"] = int(query("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA='" + schema + "';"))
            counts[schema + ".procedures"] = int(query("SELECT COUNT(*) FROM information_schema.ROUTINES WHERE ROUTINE_SCHEMA='" + schema + "';"))
            for table in tables:
                counts[schema + "." + table] = int(query("SELECT COUNT(*) FROM `" + table + "`;", schema))
    report = {"schema_version": 1, "database_version": version, "fresh_disposable_service": True,
              "game_server_started": False, "seed_values_logged": False, "imports": results, "counts_only": counts,
              "limits": "SQL syntax/schema import only. Procedures were defined, not invoked; no NHibernate/server login or persistence/concurrency tests."}
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2))
    return 0 if all(item["status"] == "PASS" for item in results) else 1


if __name__ == "__main__":
    sys.exit(main())
