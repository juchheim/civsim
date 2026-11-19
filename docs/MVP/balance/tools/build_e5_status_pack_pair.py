#!/usr/bin/env python3
"""Build and audit draft/numeric E5 packs from a pinned, clean D source snapshot.

This helper never edits the repository content tree. It requires D HEAD/tree,
content-tree and content/src/game aggregate pins, and .NET/CLI hashes; checks them
before and after each Release pack build; and requires a new output root outside
the worktree. Optional --promotion-pack compares a separately built P worktree
pack byte-for-byte to this D-derived numeric pack.
"""
from __future__ import annotations
import argparse, filecmp, hashlib, json, os, re, shutil, subprocess, sys
from pathlib import Path
from typing import Any

EXPECTED_E5_ROWS=50
STANZA_START=re.compile(r"^  - id: (.+?)\s*$")
ERA_FIVE=re.compile(r"^    era: 5\s*$")
STATUS_DRAFT=re.compile(r"^(    status: )draft(\s*)$")


def read_lines(path: Path) -> list[str]:
    with path.open("r", encoding="utf-8", newline="") as f: return f.readlines()


def e5_status_lines(path: Path) -> list[tuple[int,str,str]]:
    lines=read_lines(path); starts=[i for i,l in enumerate(lines) if STANZA_START.match(l.rstrip("\r\n"))]
    out=[]
    for k,start in enumerate(starts):
        block=lines[start:(starts[k+1] if k+1<len(starts) else len(lines))]
        if not any(ERA_FIVE.match(l.rstrip("\r\n")) for l in block): continue
        match=STANZA_START.match(lines[start].rstrip("\r\n")); assert match
        statuses=[(start+j,STATUS_DRAFT.match(l.rstrip("\r\n"))) for j,l in enumerate(block) if l.startswith("    status:")]
        if len(statuses)!=1 or statuses[0][1] is None:
            raise ValueError(f"{path}: E5 stanza {match.group(1)} must have one `status: draft` field")
        out.append((statuses[0][0],match.group(1),"draft"))
    return out


def promote_e5_statuses(content_root: Path) -> list[str]:
    changed=[]
    for path in sorted((content_root/"catalog").glob("*.yaml")):
        rows=e5_status_lines(path)
        if not rows: continue
        lines=read_lines(path)
        for line_index,pid,_ in rows:
            m=STATUS_DRAFT.match(lines[line_index].rstrip("\r\n"))
            if not m: raise ValueError(f"{path}:{line_index+1}: expected draft status for {pid}")
            ending="\r\n" if lines[line_index].endswith("\r\n") else "\n" if lines[line_index].endswith("\n") else ""
            lines[line_index]=m.group(1)+"numeric"+m.group(2)+ending; changed.append(pid)
        path.write_text("".join(lines),encoding="utf-8",newline="")
    if len(changed)!=EXPECTED_E5_ROWS or len(set(changed))!=EXPECTED_E5_ROWS or "KEY-6" not in changed:
        raise ValueError(f"expected exactly 50 unique E5 rows including KEY-6, found {len(changed)}")
    return changed


def collect_status_paths(value: Any) -> list[tuple[str,str]]:
    found=[]
    if isinstance(value,dict):
        if "id" in value and "era" in value and "status" in value and value["era"]==5:
            found.append((str(value["id"]),str(value["status"])))
        for child in value.values(): found.extend(collect_status_paths(child))
    elif isinstance(value,list):
        for child in value: found.extend(collect_status_paths(child))
    return found


def structural_diffs(left: Any,right: Any,path: str="") -> list[str]:
    if type(left) is not type(right): return [path or "/"]
    if isinstance(left,dict):
        if left.keys()!=right.keys(): return [path+"/<keys>"]
        return [d for k in sorted(left) for d in structural_diffs(left[k],right[k],f"{path}/{k}")]
    if isinstance(left,list):
        if len(left)!=len(right): return [path+"/<length>"]
        return [d for i,(a,b) in enumerate(zip(left,right)) for d in structural_diffs(a,b,f"{path}[{i}]")]
    return [] if left==right else [path]


def sha256(path: Path) -> str:
    h=hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda:f.read(1<<20),b""): h.update(chunk)
    return h.hexdigest()


def content_tree_sha256(root: Path) -> str:
    rec=[(p.relative_to(root).as_posix(),sha256(p)) for p in sorted(root.rglob("*")) if p.is_file()]
    return hashlib.sha256(json.dumps(rec,separators=(",",":"),ensure_ascii=False).encode()).hexdigest()


def _git(repo: Path,*args: str) -> str:
    return subprocess.run(["git","-C",str(repo),*args],check=True,text=True,stdout=subprocess.PIPE,stderr=subprocess.PIPE).stdout.strip()


def source_snapshot(repo: Path) -> dict[str,Any]:
    repo=repo.resolve()
    if _git(repo,"rev-parse","--show-toplevel")!=str(repo): raise ValueError("repo root is not the Git worktree root")
    dirty=_git(repo,"status","--porcelain=v1","--untracked-files=all")
    if dirty: raise ValueError("source worktree must be clean")
    head=_git(repo,"rev-parse","HEAD"); tree=_git(repo,"rev-parse","HEAD^{tree}")
    paths=_git(repo,"ls-files","-z","--","content/","src/","game/").split("\0")
    records=[]
    for rel in sorted(p for p in paths if p):
        path=repo/rel
        if path.is_file(): records.append({"path":rel,"status":"clean","sha256":sha256(path)})
        else: records.append({"path":rel,"status":"clean","sha256":None})
    payload={"head":head,"files":records}
    aggregate=hashlib.sha256(json.dumps(payload,sort_keys=True,separators=(",",":")).encode()).hexdigest()
    return {"head":head,"tree":tree,"aggregate_sha256":aggregate,"content_tree_sha256":content_tree_sha256(repo/"content"),
            "file_count":len(records),"files":records}


def toolchain_fingerprint(dotnet_exe: str, info_bytes: bytes, version: str) -> dict[str,str]:
    exe=Path(dotnet_exe).resolve()
    return {"dotnet_path":str(exe),"dotnet_sha256":sha256(exe),"dotnet_version":version,
            "dotnet_info_sha256":hashlib.sha256(info_bytes).hexdigest()}


def capture_toolchain(dotnet_cmd: str="dotnet") -> dict[str,str]:
    resolved=shutil.which(dotnet_cmd)
    if not resolved: raise ValueError(f"dotnet executable not found: {dotnet_cmd}")
    info=subprocess.run([resolved,"--info"],check=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
    version=subprocess.run([resolved,"--version"],check=True,text=True,stdout=subprocess.PIPE,stderr=subprocess.PIPE).stdout.strip()
    return toolchain_fingerprint(resolved,info.stdout,version)


def verify_pins(repo: Path, expected_head: str, expected_tree: str, expected_aggregate: str,
                expected_content_tree: str, cli_project: Path, expected_cli_sha: str,
                expected_toolchain: dict[str,str], dotnet_cmd: str) -> tuple[dict[str,Any],dict[str,str]]:
    source=source_snapshot(repo)
    observed=(source["head"],source["tree"],source["aggregate_sha256"],source["content_tree_sha256"])
    expected=(expected_head,expected_tree,expected_aggregate,expected_content_tree)
    if observed!=expected: raise ValueError(f"source pin mismatch: expected {expected}, got {observed}")
    if sha256(cli_project)!=expected_cli_sha: raise ValueError("CivSim.Cli.csproj SHA-256 mismatch")
    toolchain=capture_toolchain(dotnet_cmd)
    for key in ("dotnet_sha256","dotnet_version","dotnet_info_sha256"):
        if toolchain[key]!=expected_toolchain[key]: raise ValueError(f".NET toolchain pin mismatch for {key}")
    return source,toolchain


def ensure_fresh_external_output(raw: Path, repo: Path) -> Path:
    raw=Path(os.path.abspath(raw))
    if os.path.lexists(raw): raise ValueError(f"output directory already exists or is a symlink: {raw}")
    out=raw.resolve(); root=repo.resolve()
    if out==root or root in out.parents or out in root.parents:
        raise ValueError("output directory must be completely outside the repository worktree")
    return out


def compare_promotion_pack(expected_numeric: Path, promotion_pack: Path, expected_sha: str | None=None) -> dict[str,Any]:
    expected_numeric=expected_numeric.resolve(); promotion_pack=promotion_pack.resolve()
    if not expected_numeric.is_file() or not promotion_pack.is_file(): raise ValueError("numeric pack comparison input is missing")
    expected_hash=sha256(expected_numeric); actual_hash=sha256(promotion_pack)
    if expected_sha and actual_hash!=expected_sha: raise ValueError("P promotion pack SHA-256 mismatch")
    if not filecmp.cmp(expected_numeric,promotion_pack,shallow=False): raise ValueError("P numeric pack is not byte-for-byte identical to D-derived numeric pack")
    return {"expectedPath":str(expected_numeric),"expectedSha256":expected_hash,"promotionPath":str(promotion_pack),
            "promotionSha256":actual_hash,"byteForByteIdentical":True}


def main() -> int:
    ap=argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--repo-root",required=True,type=Path)
    ap.add_argument("--content-root",required=True,type=Path)
    ap.add_argument("--output-dir",required=True,type=Path)
    ap.add_argument("--expected-head",required=True)
    ap.add_argument("--expected-tree",required=True)
    ap.add_argument("--expected-source-aggregate-sha256",required=True)
    ap.add_argument("--expected-content-tree-sha256",required=True)
    ap.add_argument("--expected-cli-sha256",required=True)
    ap.add_argument("--dotnet",default="dotnet")
    ap.add_argument("--expected-dotnet-sha256",required=True)
    ap.add_argument("--expected-dotnet-version",required=True)
    ap.add_argument("--expected-dotnet-info-sha256",required=True)
    ap.add_argument("--promotion-pack",type=Path)
    ap.add_argument("--expected-promotion-pack-sha256")
    args=ap.parse_args()
    try:
        repo=args.repo_root.resolve(); content=args.content_root.resolve(); cli=repo/"src/CivSim.Cli/CivSim.Cli.csproj"
        if content!=(repo/"content").resolve() or not cli.is_file(): raise ValueError("content-root/repo-root/CLI project mismatch")
        expected_toolchain={"dotnet_sha256":args.expected_dotnet_sha256,"dotnet_version":args.expected_dotnet_version,
                            "dotnet_info_sha256":args.expected_dotnet_info_sha256}
        source_pre,toolchain_pre=verify_pins(repo,args.expected_head,args.expected_tree,args.expected_source_aggregate_sha256,
            args.expected_content_tree_sha256,cli,args.expected_cli_sha256,expected_toolchain,args.dotnet)
        out=ensure_fresh_external_output(args.output_dir,repo)
        if args.promotion_pack and not args.expected_promotion_pack_sha256: raise ValueError("--promotion-pack requires --expected-promotion-pack-sha256")
        if args.expected_promotion_pack_sha256 and not args.promotion_pack: raise ValueError("expected P pack SHA requires --promotion-pack")
        if args.promotion_pack and sha256(args.promotion_pack)!=args.expected_promotion_pack_sha256: raise ValueError("P promotion pack SHA-256 mismatch before build")
        out.mkdir(parents=True,exist_ok=False)
        snap=out/"source-snapshot"; shutil.copytree(content,snap)
        if content_tree_sha256(snap)!=source_pre['content_tree_sha256']:
            raise ValueError("D content snapshot copy differs from pinned content tree")
        draft=out/"content-draft"; numeric=out/"content-numeric-e5"; shutil.copytree(snap,draft); shutil.copytree(snap,numeric)
        expected_ids=promote_e5_statuses(numeric)
        draft_pack=out/"draft.pack"; numeric_pack=out/"numeric-e5.pack"
        commands=[]
        dotnet_exe=toolchain_pre["dotnet_path"]
        for inp,pack in ((draft,draft_pack),(numeric,numeric_pack)):
            # Check pinned source and toolchain immediately before and after each pack build.
            verify_pins(repo,args.expected_head,args.expected_tree,args.expected_source_aggregate_sha256,
                args.expected_content_tree_sha256,cli,args.expected_cli_sha256,expected_toolchain,args.dotnet)
            cmd=[dotnet_exe,"run","--no-restore","--project",str(cli),"-c","Release","--","build-pack","--content",str(inp),"--out",str(pack)]
            result=subprocess.run(cmd,cwd=repo,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,check=False)
            pre_after,tool_after=verify_pins(repo,args.expected_head,args.expected_tree,args.expected_source_aggregate_sha256,
                args.expected_content_tree_sha256,cli,args.expected_cli_sha256,expected_toolchain,args.dotnet)
            commands.append({"argv":cmd,"exitCode":result.returncode,"logSha256":hashlib.sha256(result.stdout).hexdigest(),"logBytes":len(result.stdout)})
            if result.returncode: raise ValueError(f"pinned Release pack build failed; log digest {commands[-1]['logSha256']}")
        draft_tree=json.loads(draft_pack.read_text(encoding="utf-8")); numeric_tree=json.loads(numeric_pack.read_text(encoding="utf-8"))
        diffs=structural_diffs(draft_tree,numeric_tree)
        allowed={f"/projects[{i}]/status" for i,p in enumerate(draft_tree["projects"]) if p.get("era")==5}
        if len(allowed)!=EXPECTED_E5_ROWS or set(diffs)!=allowed:
            raise ValueError(f"pack structural diff mismatch; unexpected={sorted(set(diffs)-allowed)}, missing={sorted(allowed-set(diffs))}")
        dstatus=dict(collect_status_paths(draft_tree)); nstatus=dict(collect_status_paths(numeric_tree))
        for pid in expected_ids:
            if dstatus.get(pid)!="draft" or nstatus.get(pid)!="numeric": raise ValueError(f"wrong pack status for {pid}")
        promo_comparison=None
        if args.promotion_pack:
            promo_comparison=compare_promotion_pack(numeric_pack,args.promotion_pack,args.expected_promotion_pack_sha256)
        source_post,toolchain_post=verify_pins(repo,args.expected_head,args.expected_tree,args.expected_source_aggregate_sha256,
            args.expected_content_tree_sha256,cli,args.expected_cli_sha256,expected_toolchain,args.dotnet)
        if source_post!=source_pre or toolchain_post!=toolchain_pre: raise ValueError("source/toolchain changed during pack preparation")
        report={"schema":2,"sourcePre":{k:v for k,v in source_pre.items() if k!="files"},
                "sourcePost":{k:v for k,v in source_post.items() if k!="files"},"sourceFileManifestSha256":hashlib.sha256(json.dumps(source_pre["files"],sort_keys=True,separators=(",",":")).encode()).hexdigest(),
                "toolchainPre":toolchain_pre,"toolchainPost":toolchain_post,"cliProject":str(cli),"cliSha256":sha256(cli),
                "helperSha256":sha256(Path(__file__).resolve()),"buildCommands":commands,"e5RowCount":len(expected_ids),
                "promotedIds":sorted(expected_ids),"structuralDiffPaths":sorted(diffs),"statusDiffOnly":True,
                "draftPackSha256":sha256(draft_pack),"numericPackSha256":sha256(numeric_pack),
                "promotionPackComparison":promo_comparison,"outputDir":str(out)}
        (out/"provenance.json").write_text(json.dumps(report,indent=2,sort_keys=True)+"\n",encoding="utf-8")
        print(json.dumps(report,indent=2,sort_keys=True)); return 0
    except (OSError,ValueError,KeyError,subprocess.SubprocessError,json.JSONDecodeError) as exc:
        print(f"build_e5_status_pack_pair: refused: {exc}",file=sys.stderr); return 2

if __name__=="__main__": raise SystemExit(main())
