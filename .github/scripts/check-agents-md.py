# Checks that condensing AGENTS.md, or moving its detail into docs/agents/, lost nothing.
#
# Usage: python .github/scripts/check-agents-md.py <old AGENTS.md> <new file>...
#   e.g. git show origin/main:AGENTS.md > /tmp/old.md
#        python .github/scripts/check-agents-md.py /tmp/old.md AGENTS.md docs/agents/*.md
#
# The new files are read together. It prints:
# - the size of each new file (the first is the core, which must stay at or under 40,000 characters)
# - headings of the old file whose text appears as no heading in the new files (comment lines inside the Commands
#   code block look like headings and are expected there)
# - links between the new files that point at a missing file or anchor
# - backticked names, and numbers, from the old file that appear nowhere in the new files
# Each name or number reported must still be covered in other words, or be an example dropped on purpose.
import os
import re
import sys

CORE_LIMIT = 40_000


def read(path):
    with open(path, encoding="utf-8") as f:
        return f.read()


def headings(text):
    """Heading texts, without their level, outside code blocks."""
    found, fence = [], False
    for line in text.splitlines():
        if line.startswith("```"):
            fence = not fence
        m = re.match(r"^#{1,6} (\S.*)$", line)
        if m and not fence:
            found.append(m.group(1).strip())
    return found


def slug(heading):
    s = heading.strip().lower()
    s = re.sub(r"[^\w\- ]", "", s)
    return s.replace(" ", "-")


def broken_links(paths):
    broken = []
    for path in paths:
        text = read(path)
        for target in re.findall(r"\]\(([^)\s]+)\)", text):
            if re.match(r"^[a-z]+:", target):
                continue  # an external URL
            file_part, _, anchor = target.partition("#")
            target_path = os.path.normpath(os.path.join(os.path.dirname(path), file_part)) if file_part else path
            if not os.path.exists(target_path):
                broken.append(f"{path}: {target} (no such file)")
            elif anchor and target_path.endswith(".md") and anchor not in {slug(h) for h in headings(read(target_path))}:
                broken.append(f"{path}: {target} (no such anchor)")
    return broken


def main():
    if len(sys.argv) < 3:
        sys.exit("Usage: python .github/scripts/check-agents-md.py <old AGENTS.md> <new file>...")
    old = read(sys.argv[1])
    new_paths = sys.argv[2:]
    texts = [read(p) for p in new_paths]
    new = "\n".join(texts)

    print(f"old: {len(old):,} characters; new, all files: {len(new):,}")
    for i, (path, text) in enumerate(zip(new_paths, texts)):
        flag = "  OVER THE LIMIT" if i == 0 and len(text) > CORE_LIMIT else ""
        print(f"  {path}: {len(text):,}{flag}")

    new_headings = set(headings(new))
    missing = [h for h in headings(old) if h not in new_headings]
    print(f"missing headings ({len(missing)}):")
    for h in missing:
        print("  ", h)

    broken = broken_links(new_paths)
    print(f"broken links ({len(broken)}):")
    for b in broken:
        print("  ", b)

    # A name counts as kept if its text still appears anywhere in the new files, backticked or not.
    gone = sorted({n for n in re.findall(r"`([^`\n]+)`", old) if n not in new})
    print(f"backticked names gone ({len(gone)}):")
    for n in gone:
        print("  ", n)

    number = r"\b\d+(?:[.,]\d+)?\b"
    gone_numbers = sorted(set(re.findall(number, old)) - set(re.findall(number, new)),
                          key=lambda x: float(x.replace(",", "")))
    print(f"numbers gone ({len(gone_numbers)}):", gone_numbers)


if __name__ == "__main__":
    main()
