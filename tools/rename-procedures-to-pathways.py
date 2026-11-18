#!/usr/bin/env python3
"""
Renames MedApp's "procedures" to "pathways", and its templates of stages to "pathway templates".

It rewrites file contents, file names and folder names, with one ordered list of rules. It is safe to run twice: a
second run changes nothing. Use it to bring a branch made before the rename across:

    python tools/rename-procedures-to-pathways.py            # rename, in the repository this file is in
    python tools/rename-procedures-to-pathways.py --dry-run  # say what would change, and change nothing
    python tools/rename-procedures-to-pathways.py --leftovers  # list every "procedure" still in the files it covers

What it does, in order, to each file it covers:
  1. Shields what must keep its spelling (PROTECTED): stored values, migration names, links to the review history,
     and "procedure" in its medical or everyday sense ("minor procedures", "the rights procedure").
  2. In the demo data only (and only on a tree from before the rename), moves the demo's own "Pathway" names out of
     the way, to "PathwayKind": the demo already called its kinds of work pathways.
  3. Renames the templates of stages: PatientProcedureStageTemplate, PatientProcedureTemplate and StageTemplate all
     become PathwayTemplate. Calendar day and week templates are a different thing and keep their names.
  4. Renames Procedure to Pathway, in every case: PatientProcedure, procedureId, patient-procedures, PROCEDURE_STATUSES.
  5. Changes the screen labels that are safe to change by their words alone: "Due date" to "Target date", and
     "My Tasks" to "My Worklist". The other labels (statuses, the admin pages' names) were changed by hand.

What it never touches (see is_excluded): build output, node_modules, existing migrations and the model snapshot (history
is never edited; the rename's own migration is written by hand), docs/reviews (history), lock files, generated files
(schema.d.ts, routeTree.gen.ts: regenerate them, or pass --generated to rename inside them), the deprecated Blazor
client, the glossary (which names both vocabularies) and a few files that read or write the old spellings on purpose.

The demo data (tools/MedApp.DevMcp/DemoData and its tests) is a story about a clinic, full of real clinical
procedures. There, only identifiers change: comments and text in quotes keep their words, except names of code
(ProcedureReplay) and addresses (api/procedures).

After running it on another branch: build, run the tests, and read `--leftovers`. A new use of "procedure" in the
medical sense needs a line in PROTECTED, or the next run renames it.
"""

from __future__ import annotations

import argparse
import os
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SELF = "tools/rename-procedures-to-pathways.py"

# ---------------------------------------------------------------------------------------------------------------------
# Which files
# ---------------------------------------------------------------------------------------------------------------------

EXCLUDED_DIR_NAMES = {
    ".git", "bin", "obj", "node_modules", "dist", "coverage", ".vs", ".idea", "TestResults", "test-results",
    "playwright-report", "blob-reports", "screenshots",
}

EXCLUDED_PREFIXES = (
    "MedApp.Infrastructure/Migrations/",  # history: never edited. The rename's own migration is written by hand.
    "docs/reviews/",                      # history
    "MedApp.Client.Deprecated/",          # kept as it was; it uses none of these names
    # Stale build output from an old layout (AGENTS.md).
    "Application/", "Domain/", "Infrastructure/", "UnitTests/", "IntegrationTests/", "E2ETests/",
)

EXCLUDED_FILES = {
    SELF,
    "docs/glossary.md",                   # names both vocabularies
    "docs/choices-made-2026-11-15.md",    # a dated record
    "project-structure.txt",              # a stale listing
    # These test migrations with the tables and columns as they were named then. Edited by hand.
    "MedApp.IntegrationTests/Migrations/ProcedureNotesMigrationTests.cs",
    "MedApp.IntegrationTests/Migrations/PathwayRenameMigrationTests.cs",
    # The one place in the web app that knows the old names: it reads old addresses, searches and pins as what they
    # are now. Written by hand.
    "MedApp.Web/src/app/beforePathways.ts",
    "MedApp.Web/src/app/beforePathways.test.tsx",
}

EXCLUDED_NAMES = {"package-lock.json", "packages.lock.json", "yarn.lock", "pnpm-lock.yaml"}

# Generated from the API and the route files. Regenerate them (npm run gen:api, and the Vite plugin or `tsr generate`);
# --generated renames inside them instead, for when that isn't possible.
GENERATED_FILES = {"MedApp.Web/src/api/schema.d.ts", "MedApp.Web/src/routeTree.gen.ts"}


def is_excluded(path: str, include_generated: bool) -> bool:
    parts = path.split("/")
    if any(part in EXCLUDED_DIR_NAMES for part in parts[:-1]):
        return True
    if path.startswith(EXCLUDED_PREFIXES) or path in EXCLUDED_FILES or parts[-1] in EXCLUDED_NAMES:
        return True
    return path in GENERATED_FILES and not include_generated


# The demo data and its tests: a story, where only identifiers change (see the docstring).
STORY_PREFIXES = (
    "tools/MedApp.DevMcp/DemoData/",
    "MedApp.UnitTests/Tools/DemoData/",
    "MedApp.IntegrationTests/Tools/",
)
STORY_FILES = {"MedApp.UnitTests/Common/Constants/DemoDataTestConstants.cs"}


def is_story(path: str) -> bool:
    return path.endswith(".cs") and (path.startswith(STORY_PREFIXES) or path in STORY_FILES)


# ---------------------------------------------------------------------------------------------------------------------
# What keeps its spelling
# ---------------------------------------------------------------------------------------------------------------------

# Each entry: (where, pattern). `where` is a regular expression the file's path must contain (None for everywhere); it
# is written to match the path before and after the rename. Whatever the pattern matches is left exactly as it is.
PROTECTED: list[tuple[str | None, str]] = [
    # --- History ---
    (None, r"\b(?:\d{14}_)?(?:RenamedTasksToProcedures|AddedProcedureWorkflow|AddedProcedureNotes|RenamedProceduresToPathways)\b"),
    (None, r"docs/reviews/[\w./-]+"),
    (None, r"\breviews/pr-[\w.-]+"),
    (None, r"\bpr-\d+-[\w-]*procedure[\w-]*\.md"),
    (None, r"\bfeature/[\w-]*procedure[\w-]*"),
    (None, r"rename-procedures-to-pathways(?:\.py)?"),

    # --- Stored values, read back as they were written ---
    # Account settings (UserSettings.Json and the browser's localStorage): the keys and values the web app keeps.
    (None, r"medapp\.proceduresStartOnMine"),
    (None, r"pinnedProcedures"),
    (None, r"'medapp\.startPage', 'procedures'"),
    (r"app/navSettings\.tsx$", r"value: 'procedures'"),
    (r"(?:Procedures|Pathways)ListPage\.tsx$", r"'patientProcedures' : 'procedures'"),
    (r"governance/cookies-and-storage\.md$", r"`medapp\.pi[\w.]*`|`medapp\.pinned\w+[^`]*`"),

    # --- "Procedure" in its medical sense: what's done to a patient ---
    (None, r"\b[Mm]inor [Pp]rocedures?\b"),
    (None, r"\bMinorProcedures?\w*"),
    (None, r"\b[Dd]iagnostic procedures?\b"),
    (None, r"\b[Ss]urgical procedures?\b"),
    (None, r"\b[Cc]linical procedures?\b"),
    (None, r"\b[Mm]edical procedures?\b"),
    (None, r"\b[Dd]ay-case procedures?\b"),
    (None, r"\bPhysical therapy procedure\b"),
    (None, r"\b[Pp]re-procedure\b"),
    (None, r"\b[Pp]ost-procedure\b"),
    (None, r"\bPostProcedureReview\b"),
    (None, r"\bprevious procedure\b"),
    (None, r"\b[Oo]utpatient procedures?\b"),
    (None, r"\bSNOMED CT procedure\b"),
    (None, r"\bkind of procedure or finding\b"),
    (None, r"\b[Ii]nterventions and [Pp]rocedures\b"),
    (None, r"\bclassification of procedures\b"),
    (None, r"\bprocedure concept\b"),
    (None, r"""https?://[\w.-]*(?:nhs\.uk|gov\.uk|org\.uk)[^\s)>\]"'`]*"""),
    # SNOMED CT's own concept 71388002, "Procedure".
    (None, r"""71388002["'],? ?(?:term[:=] ?)?["']Procedure["']"""),
    (None, r"""\b(?:snomedTerm|SnomedTerm|term)(?:: | = |=)["']Procedure["']"""),
    (r"SnomedCodeTests\.cs$|SnomedConcept\.test\.tsx$|StageDefinitionsPage\.test\.tsx$", r"""["']Procedure["']"""),
    # A stage named "Procedure" (the stage in which the procedure is done).
    (None, r"""\b(?:stageName|StageName)(?:: | = )["']Procedure["']"""),
    (r"StageList\.test\.tsx$|(?:Procedure|Pathway)Page\.test\.tsx$", r"\bname: 'Procedure'"),
    (None, r"The 'Procedure' stage"),
    (r"\.test\.tsx?$|src/test/", r"\d+\\?\. Procedure\b|: Procedure\\?\$|\bThe longest is Procedure\b|\bProcedure (?:requires|suggests) a visit\b|\bFinish Procedure\\?\?"),
    (None, r"71388002 (?:\| |is )Procedure\b"),
    (r"(?:Procedures|Pathways)TestConstants\.cs$", r"""= "Procedure";"""),

    # --- "Procedure" in its everyday sense: a way of doing something ---
    (None, r"\b(?:rights|breach|complaints|manual|interim|local|written|documented|incident|[Ss]tored) procedures?\b"),
    (None, r"\bincident and breach procedure\b"),
    (None, r"\brights-procedure\b"),
    (None, r"\b[Aa] local procedure\b"),
    (None, r"\b[Aa] written procedure\b"),
    (None, r"\bProcedure written\b"),
    (None, r"\bDraft procedure\b"),
    (None, r"\bThe manual procedure\b"),
    (None, r"\b[Dd]owntime procedures?\b"),
    (r"governance/(?:breach-response|complaints)\.md$", r"\b[Tt]his procedure\b|^#+ (?:\d+\. )?The procedure$"),
    (r"governance/ropa-customer-template\.md$", r"\[Your procedure;"),
    (r"governance/dspt-readiness\.md$", r"\bProcedure drafted\b"),
    (r"governance/dpa-template\.md$", r"Processor's procedure\b|\bunder the procedure in\b"),

    # --- The demo data's story ---
    (r"DemoData/|DemoDataTestConstants\.cs$", r"\bProcedureKinds?\b"),
    (r"DemoData/NoteBuilder\.cs$", r"\bProcedure(?=\()"),
]

# ---------------------------------------------------------------------------------------------------------------------
# The renames
# ---------------------------------------------------------------------------------------------------------------------

# The templates of stages, longest first. Plain text, matched anywhere, case as written.
TEMPLATE_RENAMES: list[tuple[str, str]] = [
    ("PatientProcedureStageTemplate", "PathwayTemplate"),
    ("patientProcedureStageTemplate", "pathwayTemplate"),
    ("PatientProcedureTemplate", "PathwayTemplate"),
    ("patientProcedureTemplate", "pathwayTemplate"),
    ("ProcedureStageTemplate", "PathwayTemplate"),
    ("procedureStageTemplate", "pathwayTemplate"),
    ("StageTemplate", "PathwayTemplate"),
    ("stageTemplate", "pathwayTemplate"),
    ("stage-template", "pathway-template"),
    ("Stage Template", "Pathway Template"),
    ("Stage template", "Pathway template"),
    ("stage template", "pathway template"),
]

# The web app's pages for them, which were named plain "Templates" (word boundaries keep CalendarTemplatesPage,
# DayTemplateEditorPage and /admin/calendar-templates as they are).
TEMPLATE_PAGE_RENAMES: list[tuple[str, str]] = [
    (r"\bTemplatesPage(?=Props\b|\b)", "PathwayTemplatesPage"),
    (r"\bTemplateEditorPage(?=Props\b|\b)", "PathwayTemplateEditorPage"),
    (r"(?<![\w-])admin/templates\b", "admin/pathway-templates"),
]

# Everything else: Procedure becomes Pathway, in each case.
GENERIC_RENAMES: list[tuple[str, str]] = [
    ("PROCEDURE", "PATHWAY"),
    ("Procedure", "Pathway"),
    ("procedure", "pathway"),
]

# Screen labels that can be changed by their words alone (identifiers have no spaces, so DueDate and MyTasksPage stay).
LABEL_RENAMES: list[tuple[str, str]] = [
    (r"\bDue Date", "Target Date"),
    (r"\bDue date", "Target date"),
    (r"\bdue date", "target date"),
    (r"(?<!')\bMy (?:Tasks|Worklist)'(?= [a-z])", "My Worklist's"),  # "My Tasks' statuses"
    (r"\bMy Tasks\b", "My Worklist"),
    (r"\bMy tasks\b", "My worklist"),
]

# The demo data's own "Pathway" names (its kinds of work, one per template), moved out of the way first: the names of
# its types everywhere, the rest only in code (its comments and quoted text keep their words).
STORY_PATHWAY_TYPE_RENAMES: list[tuple[str, str]] = [
    (r"\bDemoPathways\b", "DemoPathwayKinds"),
    (r"\bPathwaySpec\b", "PathwayKindSpec"),
]
STORY_PATHWAY_CODE_RENAMES: list[tuple[str, str]] = [
    (r"\bCreatePathwaysAsync\b", "CreatePathwayKindsAsync"),
    (r"\bPathways(?=\b|_)", "PathwayKinds"),
    (r"\bPathway\b", "PathwayKind"),
    (r"\bpathway\b", "pathwayKind"),
    (r"\bpathways\b", "pathwayKinds"),
]

# In the story's comments and quoted text, only these change: names of code, and addresses.
STORY_IDENTIFIER = re.compile(r"[A-Za-z0-9_]*[Pp]rocedure[A-Za-z0-9_]*|PROCEDURE[A-Z0-9_]*")
STORY_PLAIN_WORDS = {"procedure", "procedures", "Procedure", "Procedures", "PROCEDURE", "PROCEDURES"}
STORY_ADDRESSES = re.compile(r"""(?<=/)procedures?\b|\bpatient-procedures?\b(?:-stage)?""")
# ...and one name that is code although it's quoted: the tests read the seed's summary by its keys, which are
# properties in the seed's code (and so renamed there).
STORY_SUMMARY_KEYS = re.compile(r"""(?<=GetProperty\(")procedures(?="\))""")

MARK_OPEN, MARK_CLOSE = "", ""


class Renamer:
    def __init__(self, before_rename: bool):
        # Whether the tree is from before the rename (step 2 applies only then: afterwards "Pathway" means something else).
        self.before_rename = before_rename
        self.protected = [(re.compile(where) if where else None, re.compile(pattern, re.MULTILINE)) for where, pattern in PROTECTED]

    # -- shielding --

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
        # Shielded text can sit inside shielded text, so until none is left.
        while pattern.search(text):
            text = pattern.sub(lambda match: kept[int(match.group(1))], text)
        return text

    # -- the rules --

    @staticmethod
    def _rename_names(text: str) -> str:
        for old, new in TEMPLATE_RENAMES:
            text = text.replace(old, new)
        for pattern, new in TEMPLATE_PAGE_RENAMES:
            text = re.sub(pattern, new, text)
        for old, new in GENERIC_RENAMES:
            text = text.replace(old, new)
        return text

    @staticmethod
    def _rename_labels(text: str) -> str:
        for pattern, new in LABEL_RENAMES:
            text = re.sub(pattern, new, text)
        return text

    def _rename_story_words(self, text: str) -> str:
        """Comments and quoted text in the demo data: names of code and addresses change, words don't."""
        text = STORY_ADDRESSES.sub(lambda match: self._rename_names(match.group(0)), text)
        return STORY_IDENTIFIER.sub(
            lambda match: match.group(0) if match.group(0) in STORY_PLAIN_WORDS else self._rename_names(match.group(0)),
            text,
        )

    def _rename_story(self, text: str) -> str:
        text = STORY_SUMMARY_KEYS.sub("pathways", text)
        pieces = []
        for kind, piece in split_csharp(text):
            if self.before_rename:
                for pattern, new in STORY_PATHWAY_TYPE_RENAMES + (STORY_PATHWAY_CODE_RENAMES if kind == "code" else []):
                    piece = re.sub(pattern, new, piece)
            pieces.append(self._rename_names(piece) if kind == "code" else self._rename_story_words(piece))
        return "".join(pieces)

    # -- public --

    def contents(self, path: str, text: str) -> str:
        kept: list[str] = []
        text = self._shield(path, text, kept)
        if is_story(path):
            text = self._rename_story(text)
        else:
            text = self._rename_labels(self._rename_names(text))
        return self._unshield(text, kept)

    def path(self, path: str) -> str:
        kept: list[str] = []
        text = self._shield(path, path, kept)
        if is_story(path) and self.before_rename:
            for pattern, new in STORY_PATHWAY_TYPE_RENAMES:
                text = re.sub(pattern, new, text)
        text = self._rename_names(text)
        return self._unshield(text, kept)

    def leftovers(self, path: str, text: str) -> list[tuple[int, str, bool]]:
        """Every line still saying "procedure", with whether a rule in PROTECTED accounts for all of it."""
        found = []
        for number, line in enumerate(text.splitlines(), 1):
            if "rocedure" not in line and "ROCEDURE" not in line:
                continue
            shielded = self._shield(path, line, [])
            found.append((number, line.strip(), re.search("rocedure|ROCEDURE", shielded) is None))
        return found


# ---------------------------------------------------------------------------------------------------------------------
# C#, split into code, comments and quoted text (for the demo data)
# ---------------------------------------------------------------------------------------------------------------------

def split_csharp(text: str) -> list[tuple[str, str]]:
    """
    Splits C# source into ("code" | "text", piece) pairs that join back into the source. Comments, strings and
    characters are "text"; the expressions inside an interpolated string's braces are "code".
    """
    pieces: list[tuple[str, str]] = []
    _split_code(text, 0, pieces, until_brace=False)
    merged: list[tuple[str, str]] = []
    for kind, piece in pieces:
        if not piece:
            continue
        if merged and merged[-1][0] == kind:
            merged[-1] = (kind, merged[-1][1] + piece)
        else:
            merged.append((kind, piece))
    return merged


def _split_code(text: str, start: int, pieces: list[tuple[str, str]], until_brace: bool) -> int:
    """Reads code from `start`; inside an interpolation, stops at the brace that closes it. Returns where it stopped."""
    i, begin, depth, length = start, start, 0, len(text)
    while i < length:
        char = text[i]
        if until_brace and char == "{":
            depth += 1
        elif until_brace and char == "}":
            if depth == 0:
                break
            depth -= 1
        elif text.startswith("//", i):
            end = text.find("\n", i)
            end = length if end < 0 else end
            pieces.append(("code", text[begin:i]))
            pieces.append(("text", text[i:end]))
            i = begin = end
            continue
        elif text.startswith("/*", i):
            end = text.find("*/", i + 2)
            end = length if end < 0 else end + 2
            pieces.append(("code", text[begin:i]))
            pieces.append(("text", text[i:end]))
            i = begin = end
            continue
        elif char == "'":
            end = _end_of_quoted(text, i, "'")
            pieces.append(("code", text[begin:i]))
            pieces.append(("text", text[i:end]))
            i = begin = end
            continue
        elif char == '"' or (char in "$@" and re.match(r'[$@]+"', text[i:i + 4])):
            pieces.append(("code", text[begin:i]))
            i = begin = _split_string(text, i, pieces)
            continue
        i += 1
    pieces.append(("code", text[begin:i]))
    return i


def _end_of_quoted(text: str, start: int, quote: str) -> int:
    """The index after a quoted run with backslash escapes, or the end of its line if it never closes."""
    i = start + 1
    while i < len(text) and text[i] != "\n":
        if text[i] == "\\":
            i += 2
            continue
        if text[i] == quote:
            return i + 1
        i += 1
    return i


def _split_string(text: str, start: int, pieces: list[tuple[str, str]]) -> int:
    """Reads one string literal of any kind from `start` (its prefix), and returns the index after it."""
    prefix = re.match(r"[$@]*", text[start:]).group(0)
    dollars, verbatim = prefix.count("$"), "@" in prefix
    i = start + len(prefix)
    quotes = len(re.match(r'"+', text[i:]).group(0))
    raw = quotes >= 3
    if not raw:
        quotes = 1
    i += quotes
    begin = start
    closing = '"' * quotes
    while i < len(text):
        if raw:
            if text.startswith(closing, i):
                i += quotes
                break
            if dollars and text.startswith("{" * dollars, i) and not text.startswith("{" * (dollars + 1), i):
                pieces.append(("text", text[begin:i + dollars]))
                i = _split_code(text, i + dollars, pieces, until_brace=True)
                begin = i
                continue
            i += 1
            continue
        char = text[i]
        if char == '"':
            if verbatim and text.startswith('""', i):
                i += 2
                continue
            i += 1
            break
        if char == "\\" and not verbatim:
            i += 2
            continue
        if char == "\n" and not verbatim:
            break  # an unclosed string: give up at the end of its line
        if dollars and char == "{":
            if text.startswith("{{", i):
                i += 2
                continue
            pieces.append(("text", text[begin:i + 1]))
            i = _split_code(text, i + 1, pieces, until_brace=True)
            begin = i
            continue
        if dollars and text.startswith("}}", i):
            i += 2
            continue
        i += 1
    pieces.append(("text", text[begin:i]))
    return i


# ---------------------------------------------------------------------------------------------------------------------
# Running it
# ---------------------------------------------------------------------------------------------------------------------

def git(*args: str) -> str:
    return subprocess.run(["git", *args], cwd=ROOT, check=True, capture_output=True, text=True, encoding="utf-8").stdout


def listed_files() -> tuple[list[str], set[str]]:
    """Every file git knows or would add (not ignored ones), and which of them it tracks."""
    tracked = set(filter(None, git("ls-files", "-z").split("\0")))
    untracked = set(filter(None, git("ls-files", "-z", "--others", "--exclude-standard").split("\0")))
    return sorted(tracked | untracked), tracked


def read_text(path: Path) -> str | None:
    """The file's text, exactly (line endings and any byte order mark kept); None for a file that isn't UTF-8 text."""
    data = path.read_bytes()
    if b"\0" in data:
        return None
    try:
        return data.decode("utf-8")
    except UnicodeDecodeError:
        return None


def main() -> int:
    parser = argparse.ArgumentParser(description="Renames MedApp's procedures to pathways. Safe to run twice.")
    parser.add_argument("--dry-run", action="store_true", help="say what would change, and change nothing")
    parser.add_argument("--leftovers", action="store_true", help='list every "procedure" left in the files it covers')
    parser.add_argument("--generated", action="store_true", help="also rename inside the generated files (schema.d.ts, routeTree.gen.ts)")
    options = parser.parse_args()
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

    files, tracked = listed_files()
    files = [path for path in files if not is_excluded(path, options.generated) and (ROOT / path).is_file()]
    renamer = Renamer(before_rename=(ROOT / "MedApp.Domain" / "Procedures").is_dir())

    if options.leftovers:
        unexplained = 0
        for path in files:
            text = read_text(ROOT / path)
            if text is None:
                continue
            if re.search("rocedure|ROCEDURE", path):
                print(f"{path}: (its name)")
            for number, line, explained in renamer.leftovers(path, text):
                story = is_story(path)
                unexplained += not (explained or story)
                print(f"{path}:{number}: [{'kept' if explained else 'story' if story else 'NOT EXPLAINED'}] {line[:200]}")
        print(f"\n{unexplained} line(s) not explained by a rule.", file=sys.stderr)
        return 1 if unexplained else 0

    # Work everything out before changing anything, so a clash stops it with nothing done.
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

    # In two steps, so a file can take the name another is leaving (DemoProcedures.cs becomes DemoPathways.cs, whose
    # own file becomes DemoPathwayKinds.cs).
    for old, _ in moves:
        os.replace(ROOT / old, ROOT / f"{old}.renaming")
    for old, new in moves:
        (ROOT / new).parent.mkdir(parents=True, exist_ok=True)
        os.replace(ROOT / f"{old}.renaming", ROOT / new)

    # Folders the moves emptied.
    for folder in sorted({Path(old).parent.as_posix() for old, _ in moves}, key=len, reverse=True):
        directory = ROOT / folder
        while directory != ROOT and directory.is_dir() and not any(directory.iterdir()):
            directory.rmdir()
            directory = directory.parent

    # Tell git about the moves of files it tracks, as `git mv` would (the edits are left for you to add).
    staged = [path for old, new in moves if old in tracked for path in (old, new)]
    if staged:
        subprocess.run(["git", "add", "-A", "--pathspec-from-file=-", "--pathspec-file-nul"], cwd=ROOT, check=True,
                       input="\0".join(staged).encode("utf-8"))

    print("Done. Regenerate MedApp.Web/src/api/schema.d.ts and src/routeTree.gen.ts, then build and run the tests.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
