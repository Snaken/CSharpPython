#!/usr/bin/env python3
"""Generate deterministic Unity .meta files for the UPM package (src/CSharpPython.Core).

GUID = md5 of the package-relative path, so reruns are stable and diffs stay small.
Usage: generate-unity-meta.py [--check]
  --check   write nothing; exit 1 if any .meta is missing or stale (used by CI).
"""
import hashlib
import os
import sys

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "CSharpPython.Core")
ROOT = os.path.normpath(ROOT)
SKIP_DIRS = {"bin", "obj"}


def guid(rel):
    return hashlib.md5(rel.encode("utf-8")).hexdigest()


def file_meta(rel):
    g = guid(rel)
    ext = os.path.splitext(rel)[1]
    if ext == ".cs":
        body = ("MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n"
                "  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    elif ext == ".asmdef":
        body = "AssemblyDefinitionImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    elif os.path.basename(rel) == "package.json":
        body = "PackageManifestImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    elif ext in (".md", ".txt", "", ".xml", ".json"):
        body = "TextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    else:
        body = "DefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    return "fileFormatVersion: 2\nguid: %s\n%s" % (g, body)


def folder_meta(rel):
    return ("fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n"
            "  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % guid(rel))


def expected():
    out = {}
    for dirpath, dirnames, filenames in os.walk(ROOT):
        dirnames[:] = sorted(d for d in dirnames if d not in SKIP_DIRS and not d.startswith("."))
        for d in dirnames:
            rel = os.path.relpath(os.path.join(dirpath, d), ROOT).replace(os.sep, "/")
            out[rel + ".meta"] = folder_meta(rel)
        for f in filenames:
            if f.endswith(".meta") or f.startswith(".") or f.endswith("~"):
                continue
            rel = os.path.relpath(os.path.join(dirpath, f), ROOT).replace(os.sep, "/")
            out[rel + ".meta"] = file_meta(rel)
    return out


def main():
    check = "--check" in sys.argv
    want = expected()
    bad = []
    for rel, text in want.items():
        path = os.path.join(ROOT, rel)
        have = open(path, newline="").read() if os.path.exists(path) else None
        if have != text:
            bad.append(rel)
            if not check:
                with open(path, "w", newline="\n") as fh:
                    fh.write(text)
    # stale metas whose asset is gone
    for dirpath, dirnames, filenames in os.walk(ROOT):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        for f in filenames:
            if f.endswith(".meta"):
                rel = os.path.relpath(os.path.join(dirpath, f), ROOT).replace(os.sep, "/")
                if rel not in want:
                    bad.append(rel + " (stale)")
                    if not check:
                        os.remove(os.path.join(dirpath, f))
    if check and bad:
        print("Unity .meta files out of date; run scripts/generate-unity-meta.py:\n  " + "\n  ".join(bad))
        return 1
    print("%d .meta files %s" % (len(want), "ok" if check else "generated (%d changed)" % len(bad)))
    return 0


sys.exit(main())
