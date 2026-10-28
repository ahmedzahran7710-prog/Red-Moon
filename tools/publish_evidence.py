#!/usr/bin/env python3
"""Publish small sanitized test reports as GitHub annotations, readable via its API.

The sandbox cannot download Azure-hosted Actions artifacts. This is an additional
first-party view of our own test results, not a replacement for failing exit codes.
Never include raw SQL, configuration, credentials, binary files, or arbitrary logs.
"""
import base64
import gzip
import hashlib
import json
import os
from pathlib import Path
import re
import sys


def main():
    directory = Path(sys.argv[1])
    builds = []
    for configuration in ("debug", "release"):
        path = directory / f"build-{configuration}.log"
        if not path.exists():
            continue
        log = path.read_text(encoding="utf-8-sig", errors="replace")
        warnings = re.findall(r"(?m)^\s*(\d+) Warning\(s\)", log)
        errors = re.findall(r"(?m)^\s*(\d+) Error\(s\)", log)
        builds.append({"configuration": configuration, "succeeded": "Build succeeded." in log,
                       "warnings": int(warnings[-1]) if warnings else None,
                       "errors": int(errors[-1]) if errors else None,
                       "log_sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
    if builds:
        (directory / "build-results.json").write_text(json.dumps({"sdk_requested": os.environ.get("REDMOON_SDK", "3.1.426"), "builds": builds}, indent=2) + "\n", encoding="utf-8")
    for filename in ("managed-results.json", "sql-results.json", "build-results.json", "integration-results.json", "package-results.json", "bootstrap-results.json", "startup-diagnostics.json", "auth-probe-results.json", "stop-diagnostics.json"):
        path = directory / filename
        if not path.exists():
            continue
        if path.stat().st_size > 40000:
            raise ValueError("Refusing oversized evidence annotation")
        raw = path.read_bytes()
        # Check annotations are truncated at 4096 characters in the current API
        # transport. Compress repeated test scopes, then use bounded ASCII chunks.
        encoded = base64.b64encode(gzip.compress(raw, mtime=0)).decode("ascii")
        pieces = [encoded[i:i + 2000] for i in range(0, len(encoded), 2000)]
        for index, piece in enumerate(pieces):
            envelope = {"evidence_schema": 3, "run_id": os.environ.get("GITHUB_RUN_ID"),
                        "run_attempt": os.environ.get("GITHUB_RUN_ATTEMPT"),
                        "head_sha": os.environ.get("GITHUB_SHA"), "file": filename,
                        "file_sha256": hashlib.sha256(raw).hexdigest(),
                        "encoding": "gzip+base64", "part": index, "parts": len(pieces),
                        "payload_chunk": piece}
            message = json.dumps(envelope, separators=(",", ":"))
            if len(message) >= 3500:
                raise ValueError("Evidence envelope must remain below transport truncation")
            print(f"::notice title=FOX_EVIDENCE_{filename}_{index}::{message}")



if __name__ == "__main__":
    main()
