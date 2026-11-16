from __future__ import annotations
import hashlib, subprocess, tempfile, unittest
from pathlib import Path
import build_e5_status_pack_pair as pair

class PackPairGuardTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(); self.root=Path(self.temp.name); self.repo=self.root/'repo'; self.repo.mkdir()
        subprocess.run(['git','init','-q',str(self.repo)],check=True)
        subprocess.run(['git','-C',str(self.repo),'config','user.email','fixture@example.invalid'],check=True)
        subprocess.run(['git','-C',str(self.repo),'config','user.name','Fixture'],check=True)
        for d in ('content','src','game'):
            (self.repo/d).mkdir(); (self.repo/d/'fixture.txt').write_text(d)
        subprocess.run(['git','-C',str(self.repo),'add','.'],check=True)
        subprocess.run(['git','-C',str(self.repo),'commit','-qm','freeze'],check=True)
    def tearDown(self): self.temp.cleanup()

    def test_source_snapshot_pins_clean_tree_aggregate_and_content(self):
        snap=pair.source_snapshot(self.repo)
        self.assertEqual(subprocess.check_output(['git','-C',str(self.repo),'rev-parse','HEAD'],text=True).strip(),snap['head'])
        self.assertEqual(3,snap['file_count']); self.assertEqual(64,len(snap['aggregate_sha256']))
        self.assertEqual(pair.content_tree_sha256(self.repo/'content'),snap['content_tree_sha256'])

    def test_dirty_source_refuses(self):
        (self.repo/'src/fixture.txt').write_text('changed')
        with self.assertRaisesRegex(ValueError,'worktree must be clean'): pair.source_snapshot(self.repo)

    def test_external_output_only_and_existing_roots_refuse(self):
        out=self.root/'outside'; self.assertEqual(out.resolve(),pair.ensure_fresh_external_output(out,self.repo))
        with self.assertRaisesRegex(ValueError,'outside the repository'):
            pair.ensure_fresh_external_output(self.repo/'out',self.repo)
        out.mkdir()
        with self.assertRaisesRegex(ValueError,'already exists or is a symlink'):
            pair.ensure_fresh_external_output(out,self.repo)

    def test_broken_symlink_output_refuses(self):
        link=self.root/'broken'; link.symlink_to(self.root/'missing',target_is_directory=True)
        with self.assertRaisesRegex(ValueError,'already exists or is a symlink'):
            pair.ensure_fresh_external_output(link,self.repo)

    def test_promotion_pack_must_be_byte_identical(self):
        d=self.root/'D.pack'; p=self.root/'P.pack'; d.write_bytes(b'{"status":"numeric"}\n'); p.write_bytes(d.read_bytes())
        expected=hashlib.sha256(p.read_bytes()).hexdigest()
        report=pair.compare_promotion_pack(d,p,expected)
        self.assertTrue(report['byteForByteIdentical']); self.assertEqual(expected,report['promotionSha256'])
        p.write_bytes(b'{"status":"numeric"} \n')
        with self.assertRaisesRegex(ValueError,'not byte-for-byte identical'):
            pair.compare_promotion_pack(d,p)

    def test_toolchain_fingerprint_pins_dotnet_binary_and_info(self):
        exe=self.root/'dotnet'; exe.write_bytes(b'fixture dotnet executable')
        fp=pair.toolchain_fingerprint(str(exe),b'fixture --info output','10.0.100')
        self.assertEqual(hashlib.sha256(exe.read_bytes()).hexdigest(),fp['dotnet_sha256'])
        self.assertEqual(hashlib.sha256(b'fixture --info output').hexdigest(),fp['dotnet_info_sha256'])
        self.assertEqual('10.0.100',fp['dotnet_version'])

    def test_structural_diff_helper_identifies_only_status_path(self):
        left={'projects':[{'id':'X','era':5,'status':'draft','cost':3}]}
        right={'projects':[{'id':'X','era':5,'status':'numeric','cost':3}]}
        self.assertEqual(['/projects[0]/status'],pair.structural_diffs(left,right))
        right['projects'][0]['cost']=4
        self.assertEqual({'/projects[0]/status','/projects[0]/cost'},set(pair.structural_diffs(left,right)))

if __name__=='__main__': unittest.main()
