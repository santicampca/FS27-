#!/bin/bash
# usage: Tools/make_meta.sh <file-or-folder>...   Creates the Unity .meta (random guid) for each path that has none (script: MonoImporter; folder: folderAsset).
for p in "$@"; do
  m="$p.meta"; [ -e "$m" ] && continue
  g=$(python3 -c 'import uuid;print(uuid.uuid4().hex)')
  if [ -d "$p" ]; then printf 'fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' "$g" > "$m"
  else printf 'fileFormatVersion: 2\nguid: %s\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' "$g" > "$m"; fi
done
