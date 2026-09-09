import importlib.util
from pathlib import Path
import tempfile
import unittest

SPEC = importlib.util.spec_from_file_location("validate_docs", Path(__file__).parents[1] / "validate_docs.py")
DOCS = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(DOCS)


class DocumentationLinksTests(unittest.TestCase):
    def test_paths_fragments_spaces_and_external_links(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "source.cs").write_text("class Example {}", encoding="utf-8")
            (root / "a page.md").write_text("# Hello, world!\n# Hello, world!\n", encoding="utf-8")
            source = root / "index.md"
            source.write_text(
                "[code](source.cs) [page](<a page.md#hello-world-1>)\n"
                "[encoded](a%20page.md#hello-world) [remote](https://example.com/missing)\n"
                "[root](/source.cs)\n", encoding="utf-8")
            self.assertEqual([], DOCS.validate(root, [source]))

    def test_missing_targets_and_bad_anchors_fail(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / "index.md"
            source.write_text("# Exists\n[bad](deleted.cs) [bad](#gone) [escape](../outside.md)", encoding="utf-8")
            errors = DOCS.validate(root, [source])
            self.assertEqual(3, len(errors))
            self.assertTrue(any("missing heading" in error for error in errors))

    def test_fences_do_not_create_links_or_headings(self):
        text = "# Real\n```text\n# Fake\n[bad](missing.md)\n```\n~~~\n# Also fake\n~~~\n"
        self.assertEqual({"real"}, DOCS.anchors(text))
        self.assertEqual([], list(DOCS.LINK.finditer(DOCS.without_fences(text))))

    def test_unicode_duplicate_and_explicit_anchors(self):
        self.assertEqual({"déjà--vu", "déjà--vu-1", "stable"},
                         DOCS.anchors('# Déjà — vu\n# Déjà — vu\n<a id="stable"></a>'))


if __name__ == "__main__":
    unittest.main()
