# SPDX-FileCopyrightText: 2026 Space Onyx Contributors
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Print read-only TSV inventory of fork integration points; never build or modify files."""

import argparse
import csv
from pathlib import Path
import re
import subprocess
import sys


MARKER = re.compile(r"</?Onyx-([^>]+)>")
IMPORT = re.compile(r"\busing\s+(?:static\s+)?(?:\w+\s*=\s*)?(Content\.[\w.]*\b_\w+(?:\.[\w.]+)?)\s*;")
REFERENCE = re.compile(r"\bContent\.[\w.]*\b_\w+(?:\.[\w.]+)?")
PARTIAL = re.compile(r"\bpartial\s+(?:class|struct|record(?:\s+struct|\s+class)?)\s+(\w+)")
NAMESPACE = re.compile(r"^\s*namespace\s+([\w.]+)", re.MULTILINE)
TYPE = re.compile(r"\b(?:class|struct|interface|enum|record(?:\s+struct|\s+class)?)\s+(\w+)")
EXTENSIONS = {".cs", ".csproj", ".props", ".targets", ".yml", ".yaml", ".xaml", ".xml", ".ftl", ".toml"}


def rows(path, text):
    isolated = any(part.startswith("_") for part in Path(path).parts[:-1])
    for number, line in enumerate(text.splitlines(), 1):
        if not isolated:
            for marker in MARKER.finditer(line):
                if not marker.group().startswith("</"):
                    yield ("marker", path, number, marker[1])
        if path.endswith(".cs"):
            for dependency in IMPORT.finditer(line):
                yield ("isolated-import" if isolated else "fork-import", path, number, dependency[1])
            if not IMPORT.search(line):
                for dependency in REFERENCE.finditer(line):
                    yield ("isolated-reference" if isolated else "fork-reference", path, number, dependency[0])
        if isolated and path.endswith(".cs"):
            for declaration in PARTIAL.finditer(line):
                yield ("isolated-partial", path, number, declaration[1])


def boundary_rows(sources):
    declarations = {}
    for path, text in sources.items():
        namespace = NAMESPACE.search(text)
        if namespace is None:
            continue
        isolated = any(part.startswith("_") for part in Path(path).parts[:-1])
        for number, line in enumerate(text.splitlines(), 1):
            if line.lstrip().startswith(("//", "/*", "*")):
                continue
            for declaration in TYPE.finditer(line):
                name = declaration[1]
                key = (namespace[1], name)
                declarations.setdefault(key, []).append((path, number, isolated))

    fork_names = set()
    for (namespace, name), entries in declarations.items():
        if all(entry[2] for entry in entries):
            fork_names.add(name)
        elif any(entry[2] for entry in entries):
            for path, number, isolated in entries:
                if isolated:
                    yield ("vanilla-extension", path, number, f"{namespace}.{name}")

    if not fork_names:
        return
    symbols = re.compile(r"\b(?:" + "|".join(re.escape(name) for name in sorted(fork_names)) + r")\b")
    for path, text in sources.items():
        if any(part.startswith("_") for part in Path(path).parts[:-1]):
            continue
        for number, line in enumerate(text.splitlines(), 1):
            if line.lstrip().startswith(("//", "/*", "*", "using ", "namespace ")):
                continue
            for name in sorted(set(symbols.findall(line))):
                yield ("fork-symbol-candidate", path, number, name)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--self-check", action="store_true", help="Run parser assertions instead of inventory")
    args = parser.parse_args()
    if args.self_check:
        assert list(rows("Content.Shared/A.cs", "// <Onyx-Test>\nx();\n// </Onyx-Test>")) == [
            ("marker", "Content.Shared/A.cs", 1, "Test")
        ]
        assert list(rows("Content.Shared/A.cs", "using Content.Shared._Onyx.Traits;")) == [
            ("fork-import", "Content.Shared/A.cs", 1, "Content.Shared._Onyx.Traits")
        ]
        assert list(rows("Content.Shared/_Onyx/A.cs", "public sealed partial class Example")) == [
            ("isolated-partial", "Content.Shared/_Onyx/A.cs", 1, "Example")
        ]
        assert list(rows("Content.Shared/A.cs", "new Content.Shared._Onyx.Traits.Example();")) == [
            ("fork-reference", "Content.Shared/A.cs", 1, "Content.Shared._Onyx.Traits.Example")
        ]
        assert list(rows("Content.Shared/_Goobstation/A.cs", "using Content.Shared._Onyx.Traits;")) == [
            ("isolated-import", "Content.Shared/_Goobstation/A.cs", 1, "Content.Shared._Onyx.Traits")
        ]
        assert list(rows("Content.Server/A.cs", "using X = Content.Shared._Goobstation.Example;")) == [
            ("fork-import", "Content.Server/A.cs", 1, "Content.Shared._Goobstation.Example")
        ]
        assert list(boundary_rows({
            "Content.Shared/A.cs": "namespace Content.Shared;\npublic partial class A {}",
            "Content.Shared/_Onyx/A.cs": "namespace Content.Shared;\npublic partial class A {}",
        })) == [("vanilla-extension", "Content.Shared/_Onyx/A.cs", 2, "Content.Shared.A")]
        assert list(boundary_rows({
            "Content.Shared/A.cs": "namespace Content.Shared;\nEnsureComp<ForkComponent>(uid);",
            "Content.Shared/_DV/B.cs": "namespace Content.Shared;\npublic class ForkComponent {}",
        })) == [("fork-symbol-candidate", "Content.Shared/A.cs", 2, "ForkComponent")]
        return

    root = Path(__file__).resolve().parents[2]
    result = subprocess.run(
        ["git", "ls-files", "--cached", "--others", "--exclude-standard", "-z"],
        cwd=root, check=True, capture_output=True,
    )
    writer = csv.writer(sys.stdout, delimiter="\t", lineterminator="\n")
    writer.writerow(("kind", "path", "line", "detail"))
    sources = {}
    # ponytail: lexical inventory, not C# dependency resolution; use Roslyn for symbol-level migration checks.
    for path in sorted(set(result.stdout.decode("utf-8").split("\0"))):
        parts = Path(path).parts
        if not parts or parts[0] == "RobustToolbox":
            continue
        file = root / path
        if file.is_file():
            folders = [part for part in parts[:-1] if part.startswith("_")]
            if folders:
                writer.writerow(("isolated-file", path, 0, "/".join(folders)))
            if file.suffix in EXTENSIONS:
                text = file.read_text(encoding="utf-8-sig")
                writer.writerows(rows(path, text))
                if file.suffix == ".cs":
                    sources[path] = text
    writer.writerows(boundary_rows(sources))


if __name__ == "__main__":
    main()
