#!/usr/bin/env python3
"""
Renames MedApp's "specialties" to "teams" (docs/multi-clinic-plan.md, MC2): Specialty becomes Team, StaffSpecialty
becomes TeamMembership, SpecialtyId becomes TeamId, api/specialties becomes api/teams, and so on.

It rewrites file contents, file names and folder names, with one ordered list of rules. It is safe to run twice: a
second run changes nothing. Use it to bring a branch made before the rename across (merge main first, then run it):

    python tools/rename-specialties-to-teams.py              # rename, in the repository this file is in
    python tools/rename-specialties-to-teams.py --dry-run    # say what would change, and change nothing
    python tools/rename-specialties-to-teams.py --leftovers  # list every "specialty" still in the files it covers
    python tools/rename-specialties-to-teams.py --generated  # also rename inside schema.d.ts and routeTree.gen.ts

What it does, in order, to each file it covers:
  1. Shields what must keep its spelling (PROTECTED): stored names (the keys of audit and referral history details),
     migration names and links to the review history.
  2. Renames staff memberships: StaffSpecialty becomes TeamMembership, and the activity action
     StaffSpecialtiesChanged becomes TeamMembershipChanged (its stored number, 42, is unchanged).
  3. Renames Specialty to Team, in every case and number: SpecialtyId, specialties, SPECIALTY_NAMES.

What it never touches (see is_excluded): build output, node_modules, existing migrations and the model snapshot (the
rename's migration is RenamedSpecialtiesToTeams), docs other than docs/agents (plans, governance and reviews are a
record of their day), the generated files (unless --generated), the deprecated Blazor client, and a few files that
read or write the old spellings on purpose (the glossary, the old-names module in the web app, migration tests).

After running it on another branch: build, run the tests for what the branch changed, and read --leftovers. A new
stored key holding "specialty" needs a line in PROTECTED, or the next run renames it.
"""

from __future__ import annotations

import argparse
import os
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SELF = "tools/rename-specialties-to-teams.py"

EXCLUDED_DIR_NAMES = {
    ".git", "bin", "obj", "node_modules", "dist", "dist-demo", "coverage", ".vs", ".idea", "TestResults",
    "test-results", "playwright-report", "blob-reports", "screenshots",
}

EXCLUDED_PREFIXES = (
    "MedApp.Infrastructure/Migrations/",  # history: never edited
    "MedApp.Client.Deprecated/",
    "deploy/helm/medapp/README.md",       # its migration notes are a record of each release
    # Stale build output from an old layout (AGENTS.md).
    "Application/", "Domain/", "Infrastructure/", "UnitTests/", "IntegrationTests/", "E2ETests/",
)

EXCLUDED_FILES = {
    SELF,
    "tools/rename-procedures-to-pathways.py",
    # These test migrations with the tables and columns as they were named then. Edited by hand.
    "MedApp.IntegrationTests/Migrations/SpecialtiesMigrationTests.cs",
    "MedApp.IntegrationTests/Migrations/WaitingClockMigrationTests.cs",
    "MedApp.IntegrationTests/Migrations/TeamRenameMigrationTests.cs",
    # The one place in the web app that knows the old names. Written by hand.
    "MedApp.Web/src/app/beforePathways.ts",
    "MedApp.Web/src/app/beforePathways.test.tsx",
}

EXCLUDED_NAMES = {"package-lock.json", "packages.lock.json", "yarn.lock", "pnpm-lock.yaml"}

GENERATED_FILES = {"MedApp.Web/src/api/schema.d.ts", "MedApp.Web/src/routeTree.gen.ts"}


def is_excluded(path: str, include_generated: bool) -> bool:
    parts = path.split("/")
    if any(part in EXCLUDED_DIR_NAMES for part in parts[:-1]):
        return True
    if path.startswith(EXCLUDED_PREFIXES) or path in EXCLUDED_FILES or parts[-1] in EXCLUDED_NAMES:
        return True
    # Docs: only the agents' area guides; the rest is a record of its day (the glossary is edited by hand).
    if path.startswith("docs/") and not path.startswith("docs/agents/"):
        return True
    return path in GENERATED_FILES and not include_generated


# Each entry: (where, pattern). `where` must match the file's path (before or after the rename; None for everywhere).
# Whatever the pattern matches is left exactly as it is.
PROTECTED: list[tuple[str | None, str]] = [
    # --- History ---
    (None, r"\b(?:\d{14}_)?(?:AddedSpecialties|RenamedSpecialtiesToTeams)\b"),
    (None, r"\bSpecialtiesMigrationTests\b"),
    (None, r"docs/reviews/[\w./-]+"),
    (None, r"\bpr-\d+-[\w-]*specialt[\w-]*\.md"),
    (None, r"\bfeature/[\w-]*specialt[\w-]*"),
    (None, r"rename-specialties-to-teams(?:\.py)?"),

    # --- Stored names: the keys of JSON details, read back as they were written ---
    # A referral's history (ReferralEvent.Details): the specialty's ID and name, before and after, and the name of
    # the field an edit changed ("specialty"), which is also a clock target's cause.
    (r"\.cs$", r"\"(?:previousSpecialty|specialty)(?:Id|Name)\""),
    (r"(?:ReferralChange|TimelineMapper|ReferralCommonTests|ReferralMapperTests)\.cs$", r"\"specialty\""),
    (r"ReferralChange\.cs$", r"\b(?:previousSpecialty|specialty)(?:Id|Name)(?= = )"),
    # An MDT item's audit entries (PatientPathwayAuditEntry.Details): the specialty's name, under "specialty".
    (r"MdtAudit\.cs$", r"(?m)^\s+specialty(?= = )"),
    # The web app reads the same keys: the field names of an edit, and the export's raw history.
    (r"src/api/referrals\.ts$", r"(?m)^\s+specialty(?=: ')"),
    (r"src/demo/handlers/referrals\.ts$", r"(?<=push\()'specialty'|(?<=includes\()'specialty'"),
    (r"PatientExportPage\.tsx$", r"\bd\.(?:previousSpecialty|specialty)Name\b"),
    (r"PatientExportPage\.test\.tsx$", r"\"(?:previousSpecialty|specialty)Name\""),
    (r"src/api/(?:referrals|timeline)\.test\.ts$|src/demo/handlers/referrals\.test\.ts$",
     r"(?<=changedFields: \[)[^\]]*'specialty'[^\]]*|(?<=field: )'specialty'|(?<=toContain\()'specialty'"),
]

# Applied before shielding: an anonymous member written by shorthand, whose name is a stored key, is spelt out so the
# variable can be renamed and the key kept.
BEFORE: list[tuple[str, str, str]] = [
    (r"MdtAudit\.cs$", r"(?m)^(\s+)specialty,(\r?)$", r"\1specialty = specialty,\2"),
]

SPECIFIC_RENAMES: list[tuple[str, str]] = [
    ("StaffSpecialtiesChanged", "TeamMembershipChanged"),
    ("StaffSpecialties", "TeamMemberships"),
    ("staffSpecialties", "teamMemberships"),
    ("StaffSpecialty", "TeamMembership"),
    ("staffSpecialty", "teamMembership"),
]

GENERIC_RENAMES: list[tuple[str, str]] = [
    ("SPECIALTIES", "TEAMS"),
    ("Specialties", "Teams"),
    ("specialties", "teams"),
    ("SPECIALTY", "TEAM"),
    ("Specialty", "Team"),
    ("specialty", "team"),
]

MARK_OPEN, MARK_CLOSE = "", ""


class Renamer:
    def __init__(self) -> None:
        self.protected = [(re.compile(where) if where else None, re.compile(pattern)) for where, pattern in PROTECTED]
        self.before = [(re.compile(where), re.compile(pattern), new) for where, pattern, new in BEFORE]

    def _shield(self, path: str, text: str, kept: list[str]) -> str:
        for where, pattern in self.protected:
            if where is not None and not where.search(path):
                continue

            def keep(match: re.Match[str]) -> str:
                kept.append(match.group(0))
                return f"{MARK_OPEN}{len(kept) - 1}{MARK_CLOSE}"

            text = pattern.sub(keep, text)
        return text

    @staticmethod
    def _unshield(text: str, kept: list[str]) -> str:
        pattern = re.compile(f"{MARK_OPEN}(\\d+){MARK_CLOSE}")
        while pattern.search(text):
            text = pattern.sub(lambda match: kept[int(match.group(1))], text)
        return text

    @staticmethod
    def _rename(text: str) -> str:
        for old, new in SPECIFIC_RENAMES + GENERIC_RENAMES:
            text = text.replace(old, new)
        return text

    def contents(self, path: str, text: str) -> str:
        for where, pattern, new in self.before:
            if where.search(path):
                text = pattern.sub(new, text)
        kept: list[str] = []
        text = self._shield(path, text, kept)
        return self._unshield(self._rename(text), kept)

    def path(self, path: str) -> str:
        kept: list[str] = []
        return self._unshield(self._rename(self._shield(path, path, kept)), kept)

    def leftovers(self, path: str, text: str) -> list[tuple[int, str, bool]]:
        found = []
        for number, line in enumerate(text.splitlines(), 1):
            if not re.search("specialt", line, re.IGNORECASE):
                continue
            shielded = self._shield(path, line, [])
            found.append((number, line.strip(), re.search("specialt", shielded, re.IGNORECASE) is None))
        return found


def git(*args: str) -> str:
    return subprocess.run(["git", *args], cwd=ROOT, check=True, capture_output=True, text=True, encoding="utf-8").stdout


def listed_files() -> tuple[list[str], set[str]]:
    tracked = set(filter(None, git("ls-files", "-z").split("\0")))
    untracked = set(filter(None, git("ls-files", "-z", "--others", "--exclude-standard").split("\0")))
    return sorted(tracked | untracked), tracked


def read_text(path: Path) -> str | None:
    data = path.read_bytes()
    if b"\0" in data:
        return None
    try:
        return data.decode("utf-8")
    except UnicodeDecodeError:
        return None


def main() -> int:
    parser = argparse.ArgumentParser(description="Renames MedApp's specialties to teams. Safe to run twice.")
    parser.add_argument("--dry-run", action="store_true", help="say what would change, and change nothing")
    parser.add_argument("--leftovers", action="store_true", help='list every "specialty" left in the files it covers')
    parser.add_argument("--generated", action="store_true", help="also rename inside schema.d.ts and routeTree.gen.ts")
    options = parser.parse_args()
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

    files, tracked = listed_files()
    files = [path for path in files if not is_excluded(path, options.generated) and (ROOT / path).is_file()]
    renamer = Renamer()

    if options.leftovers:
        unexplained = 0
        for path in files:
            text = read_text(ROOT / path)
            if text is None:
                continue
            if re.search("specialt", path, re.IGNORECASE):
                print(f"{path}: (its name)")
            for number, line, explained in renamer.leftovers(path, text):
                unexplained += not explained
                print(f"{path}:{number}: [{'kept' if explained else 'NOT EXPLAINED'}] {line[:200]}")
        print(f"\n{unexplained} line(s) not explained by a rule.", file=sys.stderr)
        return 1 if unexplained else 0

    moves: list[tuple[str, str]] = []
    edits: list[tuple[str, str]] = []
    for path in files:
        text = read_text(ROOT / path)
        if text is not None:
            renamed = renamer.contents(path, text)
            if renamed != text:
                edits.append((path, renamed))
        new_path = renamer.path(path)
        if new_path != path:
            moves.append((path, new_path))

    targets = [new for _, new in moves]
    leaving = {old.lower() for old, _ in moves}
    clashes = sorted({new for new in targets if targets.count(new) > 1}
                     | {new for new in targets if (ROOT / new).exists() and new.lower() not in leaving})
    if clashes:
        print("Stopped, nothing changed. These renamed files would land on another file:", file=sys.stderr)
        for clash in clashes:
            print(f"  {clash}  <-  {', '.join(old for old, new in moves if new == clash)}", file=sys.stderr)
        return 2

    print(f"{len(edits)} file(s) to edit, {len(moves)} to move.")
    if options.dry_run:
        for old, new in moves:
            print(f"  move  {old}  ->  {new}")
        for path, _ in edits:
            print(f"  edit  {path}")
        return 0

    for path, renamed in edits:
        (ROOT / path).write_bytes(renamed.encode("utf-8"))

    for old, _ in moves:
        os.replace(ROOT / old, ROOT / f"{old}.renaming")
    for old, new in moves:
        (ROOT / new).parent.mkdir(parents=True, exist_ok=True)
        os.replace(ROOT / f"{old}.renaming", ROOT / new)

    for folder in sorted({Path(old).parent.as_posix() for old, _ in moves}, key=len, reverse=True):
        directory = ROOT / folder
        while directory != ROOT and directory.is_dir() and not any(directory.iterdir()):
            directory.rmdir()
            directory = directory.parent

    staged = [path for old, new in moves if old in tracked for path in (old, new)]
    if staged:
        subprocess.run(["git", "add", "-A", "--pathspec-from-file=-", "--pathspec-file-nul"], cwd=ROOT, check=True,
                       input="\0".join(staged).encode("utf-8"))

    print("Done. Regenerate MedApp.Web/src/api/schema.d.ts and src/routeTree.gen.ts (or run with --generated), add a "
          "migration only if the branch's own entities changed, then build and run the tests.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
