"""Adaptive CI: fail-closed diff policy, portable document/security checks and final gate.

Only Git reads, repository inspection and explicitly requested CI commands.
No product imports, ROM access, release publication or dependency installation.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import subprocess
import sys
import time
import unicodedata
import xml.etree.ElementTree as ET

PS_JOBS = ("ci0-static", "ci1-runtime", "ci2-pester", "ci3-e1-integration",
           "ci4-preflight-synthetic")
NEEDS = {"policy", "ci-docs", "ci-dotnet", *PS_JOBS}
REQUIRED_DOCS = {"README.md", "CHANGELOG.md", "CONTRIBUTING.md", "SECURITY.md",
                 "docs/product-status.md", "docs/roadmap.md", "docs/developer/workflow.md"}
DOC = r"(?:README|CHANGELOG|CONTRIBUTING|BRANDING|LEGAL)\.md|(?:docs|Docs)/(?:[A-Za-z0-9_.-]+/)*[A-Za-z0-9_.-]+\.md|Export/README\.md|assets/branding/README\.md"
NET = r"(?:src|app)/RomothequeManager\.V2/(?:[A-Za-z0-9_.-]+/)*[A-Za-z0-9_.-]+\.(?:cs|csproj|sln|axaml|resx|json|png|svg|ico)|tests/dotnet/RomothequeManager\.V2\.Tests/(?:[A-Za-z0-9_.-]+/)*[A-Za-z0-9_.-]+\.(?:cs|csproj|json)"
SHARED = r"(?:contracts|schemas)/(?:[A-Za-z0-9_.-]+/)*[A-Za-z0-9_.-]+\.json|adapters/shared/(?:[A-Za-z0-9_.-]+/)*[A-Za-z0-9_.-]+\.(?:cs|ps1|psm1|json)|Modules/(?:Identity|PersistentIdentity|DecisionSchema|StateCodec)\.psm1"
PS = r"Initialize\.ps1|Modules/[A-Za-z][A-Za-z0-9]*\.psm1|Export/[^/]+\.(?:ps1|psm1)|Preflight/[^/]+\.ps1|tests/Unit/[^/]+\.Tests\.ps1"
SENSITIVE = (
    r"(?i)[A-Z]:\\Users\\[^\s\"']+",
    r"(?i)[A-Z]:\\ROM[oô]th[eè]que",
    r"(?i)(?:github_pat_|gh[pousr]_|AKIA)[A-Za-z0-9_]{16,}",
    r"(?i)(?:password|secret|api[_-]?key)\s*[:=]\s*[\"'][^\"']{4,}",
)


class PolicyError(Exception):
    pass


def require(condition, code):
    if not condition:
        raise PolicyError(code)


def portable_path(path):
    reserved = {"CON", "PRN", "AUX", "NUL", *("COM" + str(i) for i in range(1, 10)),
                *("LPT" + str(i) for i in range(1, 10))}
    return bool(path) and all(part not in ("", ".", "..") and not part.endswith((".", " "))
        and not re.search(r'[<>:"\\|?*\x00-\x1f]', part)
        and part.split(".", 1)[0].upper() not in reserved for part in path.split("/"))


def git(root, *args):
    p = subprocess.run(["git", "--no-optional-locks", "--no-replace-objects",
                        "-C", str(root), *args], capture_output=True)
    require(p.returncode == 0, "GIT_READ_FAILED")
    return p.stdout


def commit(root, sha):
    require(isinstance(sha, str) and re.fullmatch(r"[0-9a-f]{40}", sha)
            and sha != "0" * 40, "SHA_INVALID")
    require(git(root, "rev-parse", "--verify", sha + "^{commit}").decode().strip() == sha,
            "COMMIT_UNAVAILABLE")
    return sha


def tree(root, ref="HEAD"):
    entries = {}
    for row in git(root, "ls-tree", "-rz", ref).split(b"\0"):
        if row:
            meta, path = row.split(b"\t", 1)
            mode, kind, sha = meta.decode("ascii").split()
            name = path.decode("utf-8", errors="strict")
            require(name not in entries, "DUPLICATE_TREE_PATH")
            entries[name] = (mode, kind, sha)
    require(entries, "EMPTY_TREE")
    return entries


def diff_records(raw):
    require(raw.endswith(b"\0") and raw != b"\0", "DIFF_EMPTY_OR_TRUNCATED")
    parts = raw[:-1].split(b"\0")
    require(len(parts) % 2 == 0, "DIFF_RECORD_INVALID")
    records = []
    for status, path in zip(parts[::2], parts[1::2]):
        require(status in (b"A", b"M", b"D", b"T") and path, "DIFF_STATUS_INVALID")
        name = path.decode("utf-8", errors="strict")
        require(not any(ord(c) < 32 for c in name) and not name.startswith("/"),
                "DIFF_PATH_INVALID")
        require(all(p not in ("", ".", "..") for p in name.split("/")), "DIFF_PATH_INVALID")
        records.append((status.decode(), name))
    require(len({p for _, p in records}) == len(records), "DIFF_DUPLICATE")
    return sorted(records)


def event_range(root, event, payload, env):
    checkout = commit(root, env.get("GITHUB_SHA"))
    require(git(root, "rev-parse", "HEAD").decode().strip() == checkout, "CHECKOUT_MISMATCH")
    # Native Actions always supplies repository identity. Never infer the source
    # from an old remote or a frozen commit ID: the repository may be renamed.
    if env.get("GITHUB_ACTIONS") == "true":
        repo = payload.get("repository")
        require(isinstance(repo, dict), "EVENT_REPOSITORY_MISSING")
        require(repo.get("full_name") == env.get("GITHUB_REPOSITORY")
                and bool(repo.get("full_name")), "EVENT_REPOSITORY_MISMATCH")
        require(str(repo.get("id", "")) == env.get("GITHUB_REPOSITORY_ID"),
                "EVENT_REPOSITORY_ID_MISMATCH")
    if event == "workflow_dispatch":
        return {"event": event, "checkout": checkout, "base": checkout,
                "source": checkout, "from": checkout, "changes": [], "root_push": False}
    if event == "pull_request":
        pr = payload["pull_request"]
        require(pr["base"]["ref"] == env.get("GITHUB_BASE_REF") == "main", "PR_BASE_INVALID")
        number = str(payload["number"])
        require(re.fullmatch(r"[1-9][0-9]*", number), "PR_NUMBER_INVALID")
        base, source = commit(root, pr["base"]["sha"]), commit(root, pr["head"]["sha"])
        ref = env.get("GITHUB_REF")
        if checkout == source:
            require(ref == "refs/pull/" + number + "/head", "PR_REF_INVALID")
        else:
            require(ref == "refs/pull/" + number + "/merge", "PR_REF_INVALID")
            parents = git(root, "show", "-s", "--format=%P", checkout).decode().split()
            require(parents == [base, source], "MERGE_PARENTS_INVALID")
        origin = git(root, "merge-base", "--all", base, source).decode().strip()
        commit(root, origin)
        is_fork = (isinstance(pr.get("head", {}).get("repo"), dict)
                   and isinstance(payload.get("repository"), dict)
                   and pr["head"]["repo"].get("id") != payload["repository"].get("id"))
        # A fork never qualifies for a shortened, docs-only route.
    elif event == "push":
        require(payload["ref"] == env.get("GITHUB_REF") == "refs/heads/main", "PUSH_REF_INVALID")
        require(payload.get("forced", False) is False, "FORCED_PUSH")
        source = commit(root, payload["after"])
        require(source == checkout, "PUSH_AFTER_CHECKOUT_MISMATCH")
        before = payload["before"]
        if before == "0" * 40:
            require(not git(root, "show", "-s", "--format=%P", source).decode().strip(),
                    "ROOT_PUSH_NONROOT_COMMIT")
            base, origin, root_push = None, None, True
        else:
            base = commit(root, before)
            require(base != source, "PUSH_NO_ADVANCE")
            git(root, "merge-base", "--is-ancestor", base, source)
            origin, root_push = base, False
        is_fork = False
    else:
        raise PolicyError("EVENT_UNSUPPORTED")
    if event == "pull_request":
        root_push = False
    if root_push:
        # Git's root diff (not a fabricated parent) is FULL, with every
        # tracked path explicitly classified as an addition.
        raw = git(root, "diff-tree", "--root", "--no-commit-id", "-r",
                  "--no-renames", "--name-status", "-z", source, "--")
    else:
        raw = git(root, "diff", "--no-ext-diff", "--no-textconv", "--no-renames",
                  "--name-status", "-z", "--diff-filter=ACMDTUXB", origin, source, "--")
    changes = diff_records(raw)
    tree(root, source)
    return {"event": event, "checkout": checkout, "base": base, "source": source,
            "from": origin, "changes": changes, "root_push": root_push, "fork": is_fork}


def classify(changes, force=False):
    if force or not changes:
        return "INFRA_SECURITY_UNKNOWN"
    kinds = set()
    for _, path in changes:
        if not portable_path(path):
            return "INFRA_SECURITY_UNKNOWN"
        if re.fullmatch(DOC, path):
            kinds.add("docs")
        elif re.fullmatch(SHARED, path):
            kinds.add("shared")
        elif re.fullmatch(PS, path):
            kinds.add("ps")
        elif re.fullmatch(NET, path):
            kinds.add("net")
        else:
            return "INFRA_SECURITY_UNKNOWN"
    if "shared" in kinds or {"ps", "net"} <= kinds:
        return "SHARED_OR_MIXED"
    return "PS_ONLY" if "ps" in kinds else "DOTNET_ONLY" if "net" in kinds else "DOCS_ONLY"


def job_plan(category, has_dotnet, net_changed=False):
    full = category in ("SHARED_OR_MIXED", "INFRA_SECURITY_UNKNOWN")
    require(category in ("DOCS_ONLY", "PS_ONLY", "DOTNET_ONLY", "SHARED_OR_MIXED", "INFRA_SECURITY_UNKNOWN"),
            "CLASS_UNKNOWN")
    ps = category == "PS_ONLY" or full
    net = category == "DOTNET_ONLY" or (full and (has_dotnet or net_changed))
    required = ["policy", "ci-docs"] + (list(PS_JOBS) if ps else []) + (["ci-dotnet"] if net else [])
    return {"ps": ps, "dotnet": net, "required": sorted(required),
            "not_required": {j: ("NOT_APPLICABLE_NO_DOTNET_PROJECT" if j == "ci-dotnet" and full
                                  else "NOT_REQUIRED_BY_CLASSIFIER") for j in sorted(NEEDS - set(required))}}


def make_plan(root, event, payload, env):
    try:
        context = event_range(root, event, payload, env)
        error = None
    except (PolicyError, KeyError, ValueError, UnicodeError, TypeError) as exc:
        # FULL does not mean safe to pass: the error must also fail docs/final gate.
        context = {"event": event, "changes": []}
        error = str(exc) if isinstance(exc, PolicyError) else "EVENT_OR_GIT_DATA_INVALID"
    entries = tree(root)
    # Match the exact JSON representation transported between GitHub jobs.
    context["changes"] = [list(record) for record in context["changes"]]
    has_net = any(p.endswith(".csproj") and re.fullmatch(NET, p) for p in entries)
    category = classify(context["changes"], error is not None or event == "workflow_dispatch" or context.get("root_push", False) or context.get("fork", False))
    return {"schema": 1, "class": category, "context": context, "error": error,
            "jobs": job_plan(category, has_net, any(re.fullmatch(NET, path)
                or (re.fullmatch(SHARED, path) and path.endswith(".cs")) for _, path in context["changes"]))}


def final_gate(plan, needs):
    require(not plan["error"], "CLASSIFICATION_SCOPE_UNPROVEN")
    require(set(needs) == NEEDS, "NEEDS_SET_MISMATCH")
    reported = json.loads(needs["policy"].get("outputs", {}).get("policy", "null"))
    require(reported == plan, "CLASSIFIER_OUTPUT_MISMATCH")
    for job in sorted(NEEDS):
        result = needs[job].get("result")
        if job in plan["jobs"]["required"]:
            require(result == "success", "REQUIRED_JOB_NOT_SUCCESS:" + job + ":" + str(result))
        else:
            require(result == "skipped", "UNEXPECTED_NON_REQUIRED_JOB:" + job)
    for job, reason in plan["jobs"]["not_required"].items():
        print("POLICY_JOB " + job + " " + reason)
    return True


def check_currency(changes, entries):
    require(REQUIRED_DOCS <= set(entries), "DOC_REQUIRED_MISSING")
    updated = {p for s, p in changes if s in ("A", "M") and p in entries}
    code = [p for _, p in changes if re.fullmatch(
        r"Initialize\.ps1|Modules/.+\.psm1|Export/.+\.(?:ps1|psm1)|Preflight/[^/]+\.ps1", p)
        or re.fullmatch(NET, p) or re.fullmatch(SHARED, p)]
    infra = [p for _, p in changes if p.startswith(("tests/", ".github/workflows/"))]
    if code:
        require({"docs/product-status.md", "CHANGELOG.md"} <= updated, "DOC_CODE_CURRENCY_REQUIRED")
    elif infra:
        require("docs/product-status.md" in updated, "DOC_INFRA_CURRENCY_REQUIRED")
    print("DOCS_SYNC_PORTABLE_PASS " + json.dumps({"changed": len(changes), "code": len(code), "testsAndCI": len(infra)}))


def path_rules(root):
    text = (root / "tests/Invoke-CiStatic.ps1").read_text(encoding="utf-8-sig")
    block = re.search(r"\$allowed = @\((.*?)\n    \)", text, re.S)
    require(block is not None, "PATH_POLICY_UNAVAILABLE")
    rules = re.findall(r"'([^']+)'", block[1])
    require(len(rules) >= 15, "PATH_POLICY_INCOMPLETE")
    return rules, text


def audit_entries(root, entries, rules):
    seen = set()
    for path, (mode, kind, oid) in entries.items():
        require(any(re.fullmatch(r, path) for r in rules), "TRACKED_PATH_NOT_ALLOWED:" + path)
        require(portable_path(path), "WINDOWS_PATH_INVALID:" + path)
        key = unicodedata.normalize("NFC", path).lower()
        require(key not in seen, "CASE_UNICODE_COLLISION")
        seen.add(key)
        require(mode in ("100644", "100755") and kind == "blob", "NON_REGULAR_GIT_FILE")
        target = root / path
        require(target.is_file() and not target.is_symlink(), "FILE_MISSING_OR_REPARSE")
        require(not any(p.is_symlink() for p in target.parents if p != root.parent), "REPARSE_PARENT")
        data = git(root, "cat-file", "blob", oid)
        require(len(data) < 2000000, "SUSPICIOUS_SIZE")
        if not path.endswith((".png", ".ico")):
            text = data.decode("utf-8-sig", errors="strict")
            require(not any(re.search(pattern, text) for pattern in SENSITIVE), "SENSITIVE_CONTENT:" + path)
            if path.endswith(".json"):
                def unique_members(pairs):
                    result = {}
                    for key, value in pairs:
                        require(key not in result, "JSON_DUPLICATE_MEMBER:" + path)
                        result[key] = value
                    return result
                def invalid_constant(_):
                    raise PolicyError("JSON_NONFINITE_NUMBER:" + path)
                json.loads(text, object_pairs_hook=unique_members, parse_constant=invalid_constant)


def audit_baseline(root, static_text, entries):
    block = re.search(r"\$baseline = @'\n(.*?)\n'@", static_text, re.S)
    require(block is not None, "BASELINE_UNAVAILABLE")
    hashes = re.findall(r"^([0-9a-f]{64})  ([^\r\n]+)", block[1], re.M)
    require(len(hashes) == 19, "BASELINE_19_REQUIRED")
    for digest, path in hashes:
        require(path in entries and hashlib.sha256(git(root, "cat-file", "blob", entries[path][2])).hexdigest() == digest,
                "BASELINE_HASH_CHANGED:" + path)
    for file, approved in (("tests/ci-export-sources.json", {"Export/ExportE1.psm1", "Export/Invoke-ExportE1InventoryReadOnly.ps1"}),
                           ("tests/ci-preflight-sources.json", {"Preflight/Invoke-E1RealLibraryPreflightReadOnly.ps1"})):
        manifest = json.loads(git(root, "cat-file", "blob", entries[file][2]))
        require(manifest["schemaVersion"] == "1.0.0", "SOURCE_MANIFEST_VERSION")
        sources = manifest["sources"]
        require(len(sources) == len(approved) and {s["path"] for s in sources} == approved, "SOURCE_MANIFEST_SCOPE")
        for s in sources:
            require(s["path"] in entries and hashlib.sha256(git(root, "cat-file", "blob", entries[s["path"]][2])).hexdigest() == s["sha256"], "SOURCE_HASH_CHANGED")
    require(len([p for p in entries if re.fullmatch(r"Modules/[^/]+\.psm1", p)]) == 18, "EXPECTED_18_MODULES")


def check_links(root, paths, entries):
    from urllib.parse import unquote, urlsplit
    count = 0
    for path in paths:
        if not path.endswith(".md") or path not in entries:
            continue
        text = git(root, "cat-file", "blob", entries[path][2]).decode("utf-8-sig")
        # Inline links and reference definitions. Fences are ignored, not executed.
        text = re.sub(r"(?ms)^```.*?^```[^\n]*", "", text)
        targets = re.findall(r"\]\(([^)]+)\)", text) + re.findall(r"(?m)^\[[^]]+\]:\s*(\S+)", text)
        for dest in targets:
            dest = dest.strip().split(' "', 1)[0].strip("<>")
            if not dest or dest.startswith("#") or urlsplit(dest).scheme:
                continue
            name = unquote(urlsplit(dest).path)
            require(not name.startswith(("/", "\\")) and "\\" not in name, "ABSOLUTE_DOC_LINK")
            parts = list(PurePosixPath(path).parent.parts)
            for part in name.split("/"):
                if part == "..":
                    require(parts, "DOC_LINK_OUTSIDE_REPOSITORY")
                    parts.pop()
                elif part not in ("", "."):
                    parts.append(part)
            target = "/".join(parts)
            require(target in entries or any(p.startswith(target.rstrip("/") + "/") for p in entries),
                    "BROKEN_DOC_LINK:" + path + ":" + target)
            count += 1
    print("DOC_LINKS_PASS count=" + str(count))


def documents(root, plan):
    require(not plan["error"], "CLASSIFICATION_SCOPE_UNPROVEN")
    entries = tree(root)
    rules, static = path_rules(root)
    audit_entries(root, entries, rules)
    audit_baseline(root, static, entries)
    check_currency(plan["context"]["changes"], entries)
    # A deletion must not leave links from unchanged documents dangling.
    check_links(root, [p for p in entries if p.endswith(".md")], entries)
    print("DOC_SECURITY_PASS baseline=19 export=2 preflight=1 no_product_execution=true")


def dotnet(root):
    entries = tree(root)
    projects = sorted(p for p in entries if p.endswith(".csproj") and re.fullmatch(NET, p))
    test_projects = []
    require(projects, "DOTNET_PROJECT_OR_TEST_MISSING")
    for p in projects:
        xml = ET.fromstring(git(root, "cat-file", "blob", entries[p][2]))
        if any(e.tag.split("}")[-1] == "IsTestProject" and (e.text or "").strip().lower() == "true" for e in xml.iter()):
            test_projects.append(p)
        require(str(PurePosixPath(p).parent / "packages.lock.json") in entries, "DOTNET_LOCKFILE_REQUIRED")
    require(test_projects, "DOTNET_PROJECT_OR_TEST_MISSING")
    for p in projects:
        for args in (("restore", p, "--locked-mode"), ("build", p, "--no-restore", "--configuration", "Release")):
            command = ["dotnet", *args]
            if args[0] == "restore":
                command += ["-p:NuGetAudit=true", "-p:NuGetAuditMode=all", "-p:TreatWarningsAsErrors=true"]
            if args[0] == "build":
                command += ["-p:TreatWarningsAsErrors=true", "-p:EnableNETAnalyzers=true"]
            subprocess.run(command, cwd=root, check=True)
    # A green command with zero discovered tests is not a test result.
    import tempfile
    with tempfile.TemporaryDirectory(prefix="rm-ci-dotnet-") as tmp:
        require(Path(tmp).resolve().parent == Path(tempfile.gettempdir()).resolve()
                and not Path(tmp).is_symlink(), "DOTNET_TEMP_BOUNDARY")
        for i, p in enumerate(test_projects):
            subprocess.run(["dotnet", "test", p, "--no-build", "--configuration", "Release", "--logger",
                            "trx;LogFileName=tests.trx", "--results-directory", str(Path(tmp) / str(i))], cwd=root, check=True)
            reports = list((Path(tmp) / str(i)).glob("*.trx"))
            require(len(reports) == 1, "DOTNET_TEST_REPORT_MISSING")
            counters = [e for e in ET.parse(reports[0]).iter() if e.tag.split("}")[-1] == "Counters"]
            require(len(counters) == 1, "DOTNET_TEST_COUNTERS_MISSING")
            counts = counters[0]
            executed = int(counts.get("executed", "0"))
            require(executed > 0 and int(counts.get("passed", "0")) == executed
                    and int(counts.get("total", "0")) == executed
                    and int(counts.get("failed", "1")) == 0, "DOTNET_ZERO_OR_FAILED_TESTS")
    print("DOTNET_TESTS_PASS projects=" + str(len(projects)))


def release_boundary(candidate):
    # Interface checks for a FUTURE separate publisher; never returns publication authority.
    require(candidate.get("ref") == "refs/heads/main" and candidate.get("event") != "pull_request", "RELEASE_REF_DENIED")
    sha = candidate.get("commit", "")
    require(re.fullmatch(r"[0-9a-f]{40}", sha) and sha != "0" * 40, "RELEASE_SHA_INVALID")
    require(candidate.get("main_sha") == candidate.get("tag_sha") == sha, "RELEASE_SHA_MISMATCH")
    require(candidate.get("ci_policy_gate") == "success" and candidate.get("dotnet_tests_executed", 0) > 0,
            "RELEASE_CI_UNPROVEN")
    require(candidate.get("rid") == "win-x64" and candidate.get("self_contained") is True, "RELEASE_PACKAGE_PROFILE")
    require(re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_.-]*\.zip", candidate.get("zip_name", ""))
            and re.fullmatch(r"[0-9a-f]{64}", candidate.get("zip_sha256", "")), "RELEASE_MANIFEST_INVALID")
    return "NOT_AUTHORIZED_BY_CI"  # no tag/create/upload API, no contents:write job


def environment_plan(root):
    event = os.environ.get("GITHUB_EVENT_NAME", "")
    try:
        payload = json.loads(Path(os.environ["GITHUB_EVENT_PATH"]).read_text(encoding="utf-8"))
    except (KeyError, OSError, ValueError):
        payload = {}
    return make_plan(root, event, payload, os.environ)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("command", choices=("classify", "docs", "gate", "dotnet"))
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    started = time.monotonic()
    try:
        plan = environment_plan(root)
        if args.command == "classify":
            print("CI_POLICY " + json.dumps(plan, sort_keys=True))
            if os.environ.get("GITHUB_OUTPUT"):
                with open(os.environ["GITHUB_OUTPUT"], "a", encoding="utf-8") as out:
                    out.write("policy=" + json.dumps(plan, sort_keys=True) + "\n")
                    for key in ("ps", "dotnet"):
                        out.write(key + "=" + str(plan["jobs"][key]).lower() + "\n")
            # Error scope still routes FULL; docs/final gate refuse to claim verification.
        elif args.command == "docs":
            documents(root, plan)
        elif args.command == "dotnet":
            require(plan["jobs"]["dotnet"] and not plan["error"], "DOTNET_NOT_ADMITTED")
            dotnet(root)
        else:
            final_gate(plan, json.loads(os.environ.get("CI_NEEDS_JSON", "null")))
            print("CI_POLICY_GATE_PASS class=" + plan["class"])
            if os.environ.get("GITHUB_STEP_SUMMARY"):
                with open(os.environ["GITHUB_STEP_SUMMARY"], "a", encoding="utf-8") as summary:
                    summary.write("## CI policy gate\n\n```json\n" + json.dumps(plan, indent=2, sort_keys=True)
                                  + "\n```\n\nAll required jobs executed successfully. Nonrequired jobs were skipped with the reasons above.\n")
        print("CI_POLICY_TIMING command=" + args.command + " seconds=" + format(time.monotonic() - started, ".3f"))
        return 0
    except (PolicyError, ValueError, KeyError, TypeError, OSError, ET.ParseError, subprocess.CalledProcessError) as exc:
        print("CI_POLICY_FAIL " + type(exc).__name__ + ": " + str(exc), file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
