"""Generate the GitHub wiki from docs/ so the two never drift apart.

Usage: python scripts/sync_wiki.py <path-to-wiki-checkout>

Each docs page needs front matter with `wiki_title` (the wiki page name) and
`nav_order`. Links to other pages (`guides.md`, `playlists.md#anchor`) are
rewritten to wiki links, Just the Docs callouts become plain notes, and a
sidebar and footer are written.
"""

import pathlib
import re
import sys

SITE = "https://optimusprimeums.github.io/jellyfin-plugin-iptvhub/"
ROOT = pathlib.Path(__file__).resolve().parent.parent
DOCS = ROOT / "docs"


def parse(path):
    text = path.read_text(encoding="utf-8")
    match = re.match(r"^---\n(.*?)\n---\n", text, re.S)
    if not match:
        raise SystemExit(f"{path.name}: missing front matter")
    meta = dict(
        (k.strip(), v.strip().strip('"'))
        for k, v in (line.split(":", 1) for line in match.group(1).splitlines() if ":" in line)
    )
    if "wiki_title" not in meta:
        raise SystemExit(f"{path.name}: missing wiki_title")
    return meta, text[match.end():]


def main():
    if len(sys.argv) != 2:
        raise SystemExit(__doc__)
    wiki = pathlib.Path(sys.argv[1])
    if not (wiki / ".git").exists():
        raise SystemExit(f"{wiki} is not a git checkout of the wiki")

    pages = []
    for path in sorted(DOCS.glob("*.md")):
        meta, body = parse(path)
        pages.append((int(meta.get("nav_order", 99)), path.name, meta["wiki_title"], meta["title"], body))
    pages.sort()
    titles = {name: wiki_title for _, name, wiki_title, _, _ in pages}

    def relink(match):
        target, anchor = match.group(2), match.group(3) or ""
        if target not in titles:
            raise SystemExit(f"broken link to {target}")
        return f"]({titles[target]}{anchor})"

    # Remove pages we generated last time that no longer exist.
    for old in wiki.glob("*.md"):
        if old.stem not in titles.values() and not old.name.startswith("_"):
            old.unlink()

    for _, name, wiki_title, title, body in pages:
        body = re.sub(r"\]\((\./)?([a-z0-9-]+\.md)(#[^)]*)?\)", relink, body)
        body = re.sub(r"^\{: \.note \}\n(.+)$", r"> **Note:** \1", body, flags=re.M)
        page_url = SITE if name == "index.md" else SITE + name[:-3] + ".html"
        body = body.rstrip() + f"\n\n---\n_This page is generated from [`docs/{name}`](https://github.com/Optimusprimeums/jellyfin-plugin-iptvhub/blob/master/docs/{name}). Also on the [docs site]({page_url})._\n"
        (wiki / f"{wiki_title}.md").write_text(body.lstrip(), encoding="utf-8")

    sidebar = ["**[IPTV Hub](Home)**", ""]
    sidebar += [f"- [{title}]({wiki_title})" for _, _, wiki_title, title, _ in pages if wiki_title != "Home"]
    sidebar += ["", f"[Docs site]({SITE}) · [Releases](https://github.com/Optimusprimeums/jellyfin-plugin-iptvhub/releases)"]
    (wiki / "_Sidebar.md").write_text("\n".join(sidebar) + "\n", encoding="utf-8")
    (wiki / "_Footer.md").write_text(
        f"IPTV Hub · [Docs site]({SITE}) · Edit pages in [`docs/`](https://github.com/Optimusprimeums/jellyfin-plugin-iptvhub/tree/master/docs), not here.\n",
        encoding="utf-8",
    )
    print(f"wrote {len(pages)} pages + sidebar/footer to {wiki}")


if __name__ == "__main__":
    main()
