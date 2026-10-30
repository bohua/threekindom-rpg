#!/usr/bin/env bash
# Refinement inputs for one region (chapter 1, user 2026-10-28): adds the Codex ground/detail and wild sets to the
# region's hubs json, prepares the greybox data and bakes the painted ground. Usage: refine_region.sh <ID> <concept> <name>
set -e
ID=$1; CONCEPT=$2; NAME=$3
A=D:/BuildArtifacts/Codex/hd2d-jrpg/art
p=$(echo "$ID" | tr 'A-Z' 'a-z'); r=$(echo "$CONCEPT" | cut -d- -f1)
cd "$(dirname "$0")"
python - "$ID" "$p" "$r" <<'PY'
import json, os, sys
ID, p, r = sys.argv[1:4]
f = f'hubs_{ID}.json'
d = json.load(open(f, encoding='utf-8')) if os.path.exists(f) else {}
d['_wild'] = {**d.get('_wild', {}), 'set': f'{r}-blender-wild', 'layout': f'_checks/{p}_wild_placements.json'}
d['_detail'] = {'set': f'{r}-hd-ground', 'layout': f'_checks/{p}_detail_placements.json'}
d['_dropPads'] = d.get('_dropPads', ['foundation_house', 'foundation_village', 'foundation_ruin', 'foundation_post', 'mud_camp'])
json.dump(d, open(f, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
PY
export PYTHONIOENCODING=utf-8
python prepare_region.py "$A/$CONCEPT" "$ID" "$NAME" "hubs_$ID.json"
python bake_ground.py "$A/$r-hd-ground" "$A/$r-hd-ground/${ID}_TERRAIN_MASK.png" "$A/$r-hd-ground/terrain_mask_classes.json" "$A/$CONCEPT" "$A/$r-blender-wild/_checks/${p}_wild_placements.json"
python densify_detail.py "$A/$r-hd-ground/_checks/${p}_detail_placements.json" "$A/$CONCEPT/greybox"
python bake_rock.py "$A/$CONCEPT/greybox"
