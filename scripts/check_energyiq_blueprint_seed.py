#!/usr/bin/env python3
"""Guards the EnergyIQ.Base blueprint seed (AB#7996). Stdlib only, read-only.

Checks:
1. Every YAML file under seed-data/ is listed in blueprint.yaml seedDataPaths and every
   listed path exists, stays inside the blueprint folder (no '..', no rooted path) and has no glob.
2. The seed holds product content only: every entity's ckTypeId is on the allow-list
   (archives, rollups, boards, widgets, queries, tree navigation). Site/BIM entities,
   adapters, deployment sites, pipelines and Loxone/MQTT/AI configurations fail the check.
2a. Block style only: a flow-style list item (`- {...}`, `- [...]`) or an inline `entities: [...]`
   fails, because its keys would escape the line checks.
3. No secret attribute values (Password, ClientSecret, ApiKey, Token, Secret).
4. No rtId appears twice across the seed files.
5. scripts/om_importrt.ps1 imports exactly the seed files from the blueprint folder
   (one copy) and nothing in data/ duplicates a seed file name.

Run from anywhere: python3 scripts/check_energyiq_blueprint_seed.py
"""
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
BP_DIR = ROOT / "src" / "blueprints" / "EnergyIQ.Base"
SEED_DIR = BP_DIR / "seed-data"
IMPORT_SCRIPT = ROOT / "scripts" / "om_importrt.ps1"

ALLOWED_CK_TYPES = {
    "System.StreamData/RawArchive",
    "System.StreamData/RollupArchive",
    "System.UI/Dashboard",
    "System.UI/DashboardWidget",
    "System.UI/TreeNavigationConfiguration",
    "System/SimpleRtQuery",
    "System/SimpleSdQuery",
    "System/AggregationSdQuery",
    "System/GroupingAggregationSdQuery",
}

# The checker is stdlib-only (no YAML parser on the agents), so it is FAIL-CLOSED: a line that
# carries one of the keys below but does not match the one accepted form is an error, never skipped.
# Accepted forms allow optional quotes and a trailing comment.
_TAIL = r"['\"]?\s*(#.*)?$"
CK_TYPE_KEY = re.compile(r"^\s*(-\s*)?['\"]?ckTypeId['\"]?\s*:")
CK_TYPE = re.compile(r"^\s*(-\s*)?ckTypeId:\s*['\"]?([A-Za-z0-9_.]+/[A-Za-z0-9_.]+?)(-\d+)?" + _TAIL)
RT_ID_KEY = re.compile(r"^\s*(-\s*)?['\"]?rtId['\"]?\s*:")
RT_ID = re.compile(r"^\s*(-\s*)?rtId:\s*['\"]?([0-9a-fA-F]{24})" + _TAIL)
SECRET_NAME = re.compile(r"(Password|ClientSecret|ApiKey|AccessToken|Token|Secret)", re.IGNORECASE)
# Flow-style list items or an inline entities list would hide entity keys from the line checks
# above; the runtime-model seed files use block style only, so these forms are rejected outright.
# (JSON inside quoted string values, e.g. the dashboard selector blobs, is not affected.)
FLOW_STYLE = re.compile(r"^\s*(-\s*[\{\[]|entities\s*:\s*[\{\[])")
ATTRIBUTE_ID_KEY = re.compile(r"^\s*(-\s*)?['\"]?id['\"]?\s*:\s*(.*)$")


def seed_data_paths(blueprint_text: str) -> list[str]:
    paths, inside = [], False
    for line in blueprint_text.splitlines():
        if re.match(r"^seedDataPaths:\s*$", line):
            inside = True
            continue
        if inside:
            m = re.match(r"^\s*-\s*(\S+)\s*$", line)
            if m:
                paths.append(m.group(1).strip("'\""))
            elif line.strip() and not line.lstrip().startswith("#"):
                break
    return paths


def main() -> int:
    errors: list[str] = []
    listed = seed_data_paths((BP_DIR / "blueprint.yaml").read_text(encoding="utf-8"))
    if not listed:
        errors.append("blueprint.yaml has no seedDataPaths entries")

    for p in listed:
        parts = Path(p).parts
        if p.startswith("/") or ".." in parts or "." in parts or any(c in p for c in "*?["):
            errors.append(f"seedDataPaths entry '{p}' must be a plain path inside the blueprint folder")
        elif not p.startswith("seed-data/") or Path(p).suffix not in (".yaml", ".yml"):
            errors.append(f"seedDataPaths entry '{p}' must be a .yaml/.yml file under seed-data/")
        elif not (BP_DIR / p).is_file():
            errors.append(f"seedDataPaths entry '{p}' does not exist")

    on_disk = sorted(
        str(f.relative_to(BP_DIR)) for f in SEED_DIR.rglob("*") if f.is_file() and f.suffix in (".yaml", ".yml")
    )
    for f in on_disk:
        if f not in listed:
            errors.append(f"{f} is under seed-data/ but not listed in seedDataPaths")

    # Scan every declared file AND every file on disk, so nothing that install would read escapes the checks.
    to_scan = sorted(set(on_disk) | {p for p in listed if (BP_DIR / p).is_file()})
    seen_rt_ids: dict[str, str] = {}
    entity_count = 0
    for f in to_scan:
        for no, line in enumerate((BP_DIR / f).read_text(encoding="utf-8").splitlines(), 1):
            if line.lstrip().startswith("#"):
                continue
            if FLOW_STYLE.match(line):
                errors.append(f"{f}:{no}: flow-style list/entity syntax is not allowed in the seed: {line.strip()[:80]}")
            if CK_TYPE_KEY.match(line):
                m = CK_TYPE.match(line)
                if not m:
                    errors.append(f"{f}:{no}: ckTypeId in a form the check cannot verify: {line.strip()}")
                elif m.group(2) not in ALLOWED_CK_TYPES:
                    errors.append(f"{f}:{no}: ckTypeId {m.group(2)} is not product content (allow-list)")
            a = ATTRIBUTE_ID_KEY.match(line)
            if a and SECRET_NAME.search(a.group(2).split("#", 1)[0]):
                errors.append(f"{f}:{no}: secret attribute in the seed: {line.strip()}")
            if RT_ID_KEY.match(line):
                r = RT_ID.match(line)
                if not r:
                    errors.append(f"{f}:{no}: rtId in a form the check cannot verify: {line.strip()}")
                    continue
                entity_count += 1
                rt_id = r.group(2).lower()
                if rt_id in seen_rt_ids:
                    errors.append(f"{f}:{no}: rtId {rt_id} already used in {seen_rt_ids[rt_id]}")
                seen_rt_ids[rt_id] = f"{f}:{no}"

    script = IMPORT_SCRIPT.read_text(encoding="utf-8")
    imported = set(re.findall(r'Join-Path \$seedPath "([^"]+)"', script))
    expected = {str(Path(p).relative_to("seed-data")) for p in listed if p.startswith("seed-data/")}
    if imported != expected:
        errors.append(f"om_importrt.ps1 imports {sorted(imported)} from the blueprint, expected {sorted(expected)}")

    seed_names = {Path(p).name for p in listed}
    for dup in (p for p in (ROOT / "data").rglob("*") if p.suffix in (".yaml", ".yml")):
        if dup.name in seed_names:
            errors.append(f"{dup.relative_to(ROOT)} duplicates a blueprint seed file")

    if errors:
        for e in errors:
            print(f"ERROR: {e}", file=sys.stderr)
        return 1
    print(f"EnergyIQ.Base seed OK: {len(to_scan)} files, {entity_count} entities, allow-listed types only, no secrets")
    return 0


if __name__ == "__main__":
    sys.exit(main())
