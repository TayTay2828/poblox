#!/usr/bin/env python3
"""Preflight PalCraft's sheets and generate fabric/src/main/java/dev/palcraft/gen/Sheets.java from them.

Usage: python3 tools/gen.py [--check]
Exits non-zero when a cell is unfilled or a reference between sheets does not resolve.
Rows marked verifiedInGame=false are listed as awaiting an in-game test (not a build error).
"""
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SHEETS = ROOT / "sheets"
OUT = ROOT / "fabric/src/main/java/dev/palcraft/gen/Sheets.java"
NAMES = ("pals", "items", "palworld_assets", "systems", "tests")


def load(n):
    return json.loads((SHEETS / f"{n}.json").read_text(encoding="utf-8"))


def j(v):
    if isinstance(v, bool):
        return "true" if v else "false"
    if isinstance(v, float):
        return f"{v}f"
    if isinstance(v, int):
        return str(v)
    if isinstance(v, list):
        return "java.util.List.of(" + ", ".join(j(x) for x in v) + ")"
    return json.dumps(str(v))


def preflight(s):
    errors, unverified = [], []
    for name, sh in s.items():
        seen = set()
        for row in sh["rows"]:
            key = row.get("id")
            for col in sh["columns"]:
                if col not in row or row[col] in (None, "", []):
                    errors.append(f"{name}[{key}].{col}: unfilled")
            for col in row:
                if col not in sh["columns"]:
                    errors.append(f"{name}[{key}].{col}: column not declared")
            if key in seen:
                errors.append(f"{name}[{key}]: duplicate id")
            seen.add(key)
            if row.get("verifiedInGame") is False:
                unverified.append(f"{name}[{key}]")
    ids = {n: {r["id"] for r in s[n]["rows"]} for n in s}
    for r in s["systems"]["rows"]:
        for ref in r["reads"]:
            if ref not in s:
                errors.append(f"systems[{r['id']}].reads: '{ref}' is not a sheet")
    for r in s["palworld_assets"]["rows"]:
        if r["usedBy"] not in ids["systems"]:
            errors.append(f"palworld_assets[{r['id']}].usedBy: '{r['usedBy']}' not in systems")
    for r in s["tests"]["rows"]:
        if r["system"] not in ids["systems"]:
            errors.append(f"tests[{r['id']}].system: '{r['system']}' not in systems")
    for r in s["pals"]["rows"]:
        if not (0 < r["catchBase"] <= 1):
            errors.append(f"pals[{r['id']}].catchBase: must be in (0, 1]")
        if r["groupMin"] > r["groupMax"]:
            errors.append(f"pals[{r['id']}].groupMin: larger than groupMax")
        if not r["id"].isidentifier() or r["id"] != r["id"].lower():
            errors.append(f"pals[{r['id']}].id: must be a lowercase identifier")
    return errors, unverified


def generate(s):
    L = ["// Generated from sheets/*.json by tools/gen.py. Edit the sheets, not this file.",
         "package dev.palcraft.gen;", "", "import java.util.List;", "",
         "public final class Sheets {", "\tprivate Sheets() {}", "",
         "\tpublic record Pal(String id, String tribe, String fallbackName, float health, float attack, float speed, float width, float height, float catchBase, List<String> spawnBiomes, int spawnWeight, int groupMin, int groupMax, float meshYaw) {}",
         "\tpublic record ItemRow(String id, String name, int maxStack, String texture, String palworldIcon, int starterCount) {}",
         "\tpublic record Test(String id, String passWhen, String system) {}", ""]
    L.append("\tpublic static final List<Pal> PALS = List.of(")
    L.append(",\n".join(f"\t\tnew Pal({j(r['id'])}, {j(r['tribe'])}, {j(r['fallbackName'])}, {float(r['health'])}f, {float(r['attack'])}f, {float(r['speed'])}f, {float(r['width'])}f, {float(r['height'])}f, {float(r['catchBase'])}f, {j(r['spawnBiomes'])}, {r['spawnWeight']}, {r['groupMin']}, {r['groupMax']}, {float(r['meshYaw'])}f)" for r in s["pals"]["rows"]))
    L.append("\t);")
    L.append("\tpublic static final List<ItemRow> ITEMS = List.of(")
    L.append(",\n".join(f"\t\tnew ItemRow({j(r['id'])}, {j(r['name'])}, {r['maxStack']}, {j(r['texture'])}, {j(r['palworldIcon'])}, {r['starterCount']})" for r in s["items"]["rows"]))
    L.append("\t);")
    L.append("\tpublic static final List<Test> TESTS = List.of(")
    L.append(",\n".join(f"\t\tnew Test({j(r['id'])}, {j(r['passWhen'])}, {j(r['system'])})" for r in s["tests"]["rows"]))
    L.append("\t);")
    L += ["}", ""]
    return "\n".join(L)


def main():
    s = {n: load(n) for n in NAMES}
    errors, unverified = preflight(s)
    print(f"preflight: {sum(len(x['rows']) for x in s.values())} rows across {len(s)} sheets")
    for e in errors:
        print("  ERROR", e)
    if unverified:
        print(f"  awaiting in-game test ({len(unverified)}): " + ", ".join(unverified))
    if errors:
        sys.exit(1)
    print("preflight clean")
    code = generate(s)
    if "--check" in sys.argv:
        sys.exit(0 if OUT.exists() and OUT.read_text(encoding="utf-8") == code else 1)
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(code, encoding="utf-8")
    print(f"wrote {OUT.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
