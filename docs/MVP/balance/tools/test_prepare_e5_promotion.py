from __future__ import annotations
import json, os, subprocess, tempfile, unittest
from unittest.mock import patch
from pathlib import Path
from prepare_e5_promotion import prepare, catalog_rows

IDS = [f"E5-{i:02d}" for i in range(1, 50)] + ["KEY-6"]

def manifest(ids: list[str]) -> dict:
    return {"schema": 1, "rows": [
        {"id": pid, "accepted": True, "numeric_values": {"candidate": 1.25},
         "evidence": [{"accepted": True, "reference": f"evidence/{pid}.txt"}]}
        for pid in ids]}

class PromotionTransactionTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.repo = self.root / "repo"
        self.repo.mkdir()
        subprocess.run(["git", "init", "-q", str(self.repo)], check=True)
        subprocess.run(["git", "-C", str(self.repo), "config", "user.email", "fixture@example.invalid"], check=True)
        subprocess.run(["git", "-C", str(self.repo), "config", "user.name", "Fixture"], check=True)
        self.content = self.repo / "content"
        (self.content / "catalog").mkdir(parents=True)
        lines = ["schema: 1\n", "projects:\n"]
        for pid in IDS:
            lines += [f"  - id: {pid}\n", "    era: 5\n", "    type: M\n", "    status: draft\n",
                      "    effects:\n", "      - {kind: mod, value: 1.25}\n",
                      "    practice: [{action: craft, share: 100}]\n", "    flavor: fixture\n", "    moment: fixture\n"]
        (self.content / "catalog/fixture.yaml").write_text("".join(lines), encoding="utf-8")
        (self.content / "registries").mkdir()
        (self.content / "registries/fixture.yaml").write_text("registry: fixture\n", encoding="utf-8")
        self.evidence_root = self.root / "evidence-root"
        (self.evidence_root / "evidence").mkdir(parents=True)
        for pid in IDS:
            (self.evidence_root / "evidence" / f"{pid}.txt").write_text(f"accepted evidence for {pid}\n")
        self.manifest = self.root / "acceptance.json"
        self.output = self.root / "review"
        self.write_manifest(manifest(IDS))
        subprocess.run(["git", "-C", str(self.repo), "add", "content"], check=True)
        subprocess.run(["git", "-C", str(self.repo), "commit", "-qm", "draft D fixture"], check=True)
        self.head = subprocess.check_output(["git", "-C", str(self.repo), "rev-parse", "HEAD"], text=True).strip()

    def tearDown(self) -> None:
        self.temp.cleanup()

    def write_manifest(self, data: dict) -> None:
        self.manifest.write_text(json.dumps(data), encoding="utf-8")

    def run_prepare(self, output: Path | None = None, head: str | None = None):
        return prepare(self.content, self.manifest, output or self.output, self.head if head is None else head,
                       self.repo, self.evidence_root)

    def test_incomplete_manifest_refuses_before_output(self) -> None:
        self.write_manifest(manifest(IDS[:-1]))
        with self.assertRaisesRegex(ValueError, "ID set mismatch"):
            self.run_prepare()
        self.assertFalse(self.output.exists())
        self.assertFalse(list(self.root.glob(".review.staging-*")))

    def test_invalid_numeric_value_refuses(self) -> None:
        data = manifest(IDS); data["rows"][0]["numeric_values"]["candidate"] = "1.25"
        self.write_manifest(data)
        with self.assertRaisesRegex(ValueError, "invalid numeric value"):
            self.run_prepare()
        self.assertFalse(self.output.exists())

    def test_rejected_evidence_refuses(self) -> None:
        data = manifest(IDS); data["rows"][12]["evidence"][0]["accepted"] = False
        self.write_manifest(data)
        with self.assertRaisesRegex(ValueError, r"evidence\[0\] is not marked accepted"):
            self.run_prepare()
        self.assertFalse(self.output.exists())

    def test_missing_evidence_file_refuses(self) -> None:
        data = manifest(IDS); data["rows"][0]["evidence"][0]["reference"] = "evidence/missing.txt"
        self.write_manifest(data)
        with self.assertRaisesRegex(ValueError, "file does not exist"):
            self.run_prepare()

    def test_empty_evidence_file_refuses(self) -> None:
        data = manifest(IDS)
        (self.evidence_root / "evidence" / f"{IDS[0]}.txt").write_bytes(b"")
        self.write_manifest(data)
        with self.assertRaisesRegex(ValueError, "file is empty"):
            self.run_prepare()

    def test_traversal_and_absolute_evidence_paths_refuse(self) -> None:
        for ref in ("../outside.txt", "/tmp/outside.txt", "evidence\\escape.txt", "C:/outside.txt"):
            data=manifest(IDS); data["rows"][0]["evidence"][0]["reference"]=ref; self.write_manifest(data)
            with self.subTest(ref=ref), self.assertRaisesRegex(ValueError, "reference"):
                self.run_prepare()

    def test_evidence_change_during_staging_refuses(self) -> None:
        import prepare_e5_promotion as module
        original=module._evidence_ref; changed=False
        def mutate(root, reference, project_id, index):
            nonlocal changed
            result=original(root, reference, project_id, index)
            if project_id==IDS[0] and not changed:
                (root / reference).write_text("changed during staging\n")
                changed=True
            return result
        with patch.object(module, "_evidence_ref", side_effect=mutate):
            with self.assertRaisesRegex(ValueError, "evidence changed during staging"):
                self.run_prepare()
        self.assertFalse(self.output.exists())
        self.assertFalse(list(self.root.glob(".review.staging-*")))

    def test_fake_or_stale_head_refuses(self) -> None:
        for head in ("pretend-head", "0" * 40):
            with self.subTest(head=head), self.assertRaisesRegex(ValueError, "--head mismatch"):
                self.run_prepare(head=head)

    def test_dirty_source_worktree_refuses(self) -> None:
        (self.repo / "untracked.txt").write_text("dirty\n")
        with self.assertRaisesRegex(ValueError, "worktree must be clean"):
            self.run_prepare()

    def test_in_repo_output_root_refuses_before_output(self) -> None:
        out=self.repo / "review"
        with self.assertRaisesRegex(ValueError, "separate from the repository"):
            self.run_prepare(output=out)
        self.assertFalse(out.exists())

    def test_output_parent_of_repository_refuses(self) -> None:
        with self.assertRaisesRegex(ValueError, "already exists|separate from the repository"):
            self.run_prepare(output=self.repo.parent)

    def test_evidence_symlink_cannot_escape_evidence_root(self) -> None:
        outside=self.root / "outside.txt"; outside.write_text("outside")
        link=self.evidence_root / "evidence" / "escape.txt"
        link.symlink_to(outside)
        data=manifest(IDS); data["rows"][0]["evidence"][0]["reference"]="evidence/escape.txt"; self.write_manifest(data)
        with self.assertRaisesRegex(ValueError, "escapes evidence root"):
            self.run_prepare()

    def test_broken_symlink_output_root_refuses(self) -> None:
        link=self.root / "broken-review"
        link.symlink_to(self.root / "missing-target", target_is_directory=True)
        with self.assertRaisesRegex(ValueError, "already exists or is a symlink"):
            self.run_prepare(output=link)
        self.assertTrue(link.is_symlink())

    def test_all_50_staging_changes_status_only_and_pins_evidence_and_source(self) -> None:
        before=(self.content / "catalog/fixture.yaml").read_bytes()
        report=self.run_prepare()
        staged=self.output / "content/catalog/fixture.yaml"
        patch=(self.output / "promotion.patch").read_text(encoding="utf-8")
        saved=json.loads((self.output / "report.json").read_text())
        self.assertEqual(50, report["e5_row_count"]); self.assertEqual(50, len(report["promoted_ids"]))
        self.assertTrue(report["status_changes_only"]); self.assertEqual(self.head, report["head"])
        self.assertEqual(50, patch.count("-    status: draft")); self.assertEqual(50, patch.count("+    status: numeric"))
        self.assertEqual(before, (self.content / "catalog/fixture.yaml").read_bytes())
        self.assertEqual(50, sum(status == "numeric" for _, _, status in catalog_rows(self.output / "content").values()))
        self.assertEqual(50, len(saved["evidence_files"]))
        self.assertEqual(self.head, saved["head"])
        self.assertEqual(b"registry: fixture\n", (self.output / "content/registries/fixture.yaml").read_bytes())

    def test_output_never_overwrites(self) -> None:
        self.output.mkdir(); sentinel=self.output / "keep.txt"; sentinel.write_text("keep")
        with self.assertRaisesRegex(ValueError, "already exists or is a symlink"):
            self.run_prepare()
        self.assertEqual("keep", sentinel.read_text())

if __name__ == "__main__": unittest.main()
