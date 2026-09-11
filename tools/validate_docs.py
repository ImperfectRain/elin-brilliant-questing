"""Check local inline Markdown links in the living documentation graph (stdlib only).

This intentionally does not parse C#, remote links, reference-style links, or full Markdown.
Explicit file arguments extend the check to other repository documents.
"""
from pathlib import Path
import re
import sys
from urllib.parse import unquote, urlsplit

ROOT = Path(__file__).resolve().parents[1]
ENTRY_POINTS = (
    "AGENTS.md", "CLAUDE.md", "README.md", "docs/README.md", "docs/architecture.md",
    "docs/agent/workflow.md", "docs/agent/documentation.md", "docs/agent/validation.md",
    "docs/elin/README.md", "docs/elin/capabilities.md",
    "docs/living-world-roadmap.md", "docs/agent/bqa-roadmap-audit.md",
)
LINK = re.compile(r'!?\[[^\]\n]*\]\(\s*(?:<([^>]+)>|([^\s)]+))(?:\s+"[^"]*")?\s*\)')


def without_fences(text):
    lines = []
    fence = None
    for line in text.splitlines():
        marker = re.match(r"^\s{0,3}(`{3,}|~{3,})", line)
        if marker:
            token = marker.group(1)
            if fence is None:
                fence = token
            elif token[0] == fence[0] and len(token) >= len(fence):
                fence = None
            lines.append("")
        else:
            lines.append(line if fence is None else "")
    return "\n".join(lines)


def anchors(text):
    text = without_fences(text)
    result = set(re.findall(r'<a\s+(?:id|name)=["\']([^"\']+)["\']', text))
    used = set()
    for line in text.splitlines():
        heading = re.match(r"^ {0,3}#{1,6}\s+(.+?)\s*#*\s*$", line)
        if not heading:
            continue
        title = re.sub(r"\[([^]]+)\]\([^)]+\)", r"\1", heading.group(1))
        title = re.sub(r"<[^>]+>", "", title).lower()
        # GitHub-style ATX heading anchors for the simple headings used in this graph.
        base = re.sub(r"[^\w\- ]", "", title).replace(" ", "-")
        slug, number = base, 0
        while slug in used:
            number += 1
            slug = f"{base}-{number}"
        used.add(slug)
        result.add(slug)
    return result


def validate(root, files):
    root = root.resolve()
    errors = []
    cache = {}
    for source in files:
        source = source.resolve()
        if not source.is_relative_to(root) or not source.is_file():
            errors.append(f"Missing or outside repository: {source}")
            continue
        content = without_fences(source.read_text(encoding="utf-8-sig"))
        for match in LINK.finditer(content):
            target = match.group(1) or match.group(2)
            parsed = urlsplit(target)
            if parsed.scheme or parsed.netloc:
                continue
            path = unquote(parsed.path)
            dest = ((root / path.lstrip("/")) if path.startswith("/")
                    else (source.parent / path) if path else source).resolve()
            line = content.count("\n", 0, match.start()) + 1
            where = f"{source.relative_to(root)}:{line}: {target}"
            if not dest.is_relative_to(root) or not dest.exists():
                errors.append(f"{where}: missing or outside repository")
            elif parsed.fragment and dest.suffix.lower() == ".md":
                if dest not in cache:
                    cache[dest] = anchors(dest.read_text(encoding="utf-8-sig"))
                if unquote(parsed.fragment) not in cache[dest]:
                    errors.append(f"{where}: missing heading/anchor")
    return errors


def main(args):
    files = [ROOT / arg for arg in args] if args else (
        [ROOT / path for path in ENTRY_POINTS] + sorted((ROOT / "docs/systems").glob("*.md")))
    errors = validate(ROOT, files)
    for error in errors:
        print(error, file=sys.stderr)
    print(f"Documentation links: {len(files)} files, {len(errors)} errors.")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
