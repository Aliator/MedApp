# Compares two versions of AGENTS.md after condensing it, to show what may have been lost.
#
# Usage: python .github/scripts/check-agents-md.py <old> <new>
#   e.g. git show origin/main:AGENTS.md > /tmp/old.md && python .github/scripts/check-agents-md.py /tmp/old.md AGENTS.md
#
# Prints the sizes, headings missing from the new version (comment lines inside the Commands code block look like
# headings and are expected there), #anchor links that no longer match a heading, backticked names whose text no
# longer appears anywhere, and numbers that disappeared. Each name or number reported must still be covered in other
# words, or be an example dropped on purpose. Claude Code loads at most 150,000 characters of the file.
import re
import sys

LIMIT = 150_000


def read(path):
    with open(path, encoding="utf-8") as f:
        return f.read()


def headings(text):
    return [line for line in text.splitlines() if re.match(r"^#{1,4} \S", line)]


def slug(heading):
    s = re.sub(r"^#+ ", "", heading).strip().lower()
    s = re.sub(r"[^\w\- ]", "", s)
    return s.replace(" ", "-")


def code_names(text):
    return set(re.findall(r"`([^`\n]+)`", text))


def numbers(text):
    return set(re.findall(r"\b\d+(?:[.,]\d+)?\b", text))


def main():
    if len(sys.argv) != 3:
        sys.exit("Usage: python .github/scripts/check-agents-md.py <old> <new>")
    old, new = read(sys.argv[1]), read(sys.argv[2])

    print(f"characters: {len(old):,} -> {len(new):,} ({100 * len(new) / len(old):.0f}%), limit {LIMIT:,}")
    if len(new) > LIMIT:
        print("  OVER THE LIMIT")

    new_headings = set(headings(new))
    missing = [h for h in headings(old) if h not in new_headings]
    print(f"missing headings ({len(missing)}):")
    for h in missing:
        print("  ", h)

    anchors = {slug(h) for h in new_headings}
    broken = sorted({a for a in re.findall(r"\]\(#([^)]+)\)", new) if a not in anchors})
    print(f"broken anchors ({len(broken)}):", broken)

    # A name counts as kept if its text still appears anywhere in the new version, backticked or not.
    gone = sorted(n for n in code_names(old) - code_names(new) if n not in new)
    print(f"backticked names gone ({len(gone)}):")
    for n in gone:
        print("  ", n)

    gone_numbers = sorted(numbers(old) - numbers(new), key=lambda x: float(x.replace(",", "")))
    print(f"numbers gone ({len(gone_numbers)}):", gone_numbers)


if __name__ == "__main__":
    main()
