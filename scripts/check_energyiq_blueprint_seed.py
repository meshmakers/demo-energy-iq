#!/usr/bin/env python3
"""Guards the EnergyIQ.Base blueprint seed (AB#7996). Stdlib only, read-only.

Checks:
1. Every YAML file under seed-data/ is listed in blueprint.yaml seedDataPaths and every
   listed path exists, stays inside the blueprint folder (no '..', no rooted path) and has no glob.
2. The seed holds product content only: every entity's ckTypeId is on the allow-list
   (archives, rollups, boards, widgets, queries, tree navigation). Site/BIM entities,
   adapters, deployment sites, pipelines and Loxone/MQTT/AI configurations fail the check.
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
    "System/Query",
    "System/SimpleRtQuery",
    "System/SimpleSdQuery",
    "System/AggregationSdQuery",
    "System/GroupingAggregationSdQuery",
}

SECRET_ATTRIBUTE = re.compile(
    r"^\s*-?\s*id:\s*\S*/(\S*\.)?(Password|ClientSecret|ApiKey|AccessToken|Token|Secret)(-\d+)?\s*$",
    re.IGNORECASE,
)
CK_TYPE = re.compile(r"^\s*-?\s*ckTypeId:\s*['\"]?([A-Za-z0-9_.]+/[A-Za-z0-9_.]+?)(-\d+)?['\"]?\s*$")
RT_ID = re.compile(r"^\s*-\s*rtId:\s*['\"]?([0-9a-fA-F]{24})['\"]?\s*$")


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
        if p.startswith("/") or ".." in Path(p).parts or "." in Path(p).parts or any(c in p for c in "*?["):
            errors.append(f"seedDataPaths entry '{p}' must be a plain path inside the blueprint folder")
        elif not (BP_DIR / p).is_file():
            errors.append(f"seedDataPaths entry '{p}' does not exist")

    on_disk = sorted(str(f.relative_to(BP_DIR)) for f in SEED_DIR.rglob("*.yaml"))
    for f in on_disk:
        if f not in listed:
            errors.append(f"{f} is under seed-data/ but not listed in seedDataPaths")

    seen_rt_ids: dict[str, str] = {}
    entity_count = 0
    for f in on_disk:
        for no, line in enumerate((BP_DIR / f).read_text(encoding="utf-8").splitlines(), 1):
            if line.lstrip().startswith("#"):
                continue
            m = CK_TYPE.match(line)
            if m and m.group(1) not in ALLOWED_CK_TYPES:
                errors.append(f"{f}:{no}: ckTypeId {m.group(1)} is not product content (allow-list)")
            if SECRET_ATTRIBUTE.match(line):
                errors.append(f"{f}:{no}: secret attribute in the seed: {line.strip()}")
            r = RT_ID.match(line)
            if r:
                entity_count += 1
                rt_id = r.group(1).lower()
                if rt_id in seen_rt_ids:
                    errors.append(f"{f}:{no}: rtId {rt_id} already used in {seen_rt_ids[rt_id]}")
                seen_rt_ids[rt_id] = f"{f}:{no}"

    script = IMPORT_SCRIPT.read_text(encoding="utf-8")
    imported = set(re.findall(r'Join-Path \$seedPath "([^"]+)"', script))
    expected = {str(Path(p).relative_to("seed-data")) for p in listed if p.startswith("seed-data/")}
    if imported != expected:
        errors.append(f"om_importrt.ps1 imports {sorted(imported)} from the blueprint, expected {sorted(expected)}")

    seed_names = {Path(p).name for p in listed}
    for dup in (ROOT / "data").rglob("*.yaml"):
        if dup.name in seed_names:
            errors.append(f"{dup.relative_to(ROOT)} duplicates a blueprint seed file")

    if errors:
        for e in errors:
            print(f"ERROR: {e}", file=sys.stderr)
        return 1
    print(f"EnergyIQ.Base seed OK: {len(on_disk)} files, {entity_count} entities, allow-listed types only, no secrets")
    return 0


if __name__ == "__main__":
    sys.exit(main())
