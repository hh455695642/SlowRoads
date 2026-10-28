"""Inventory the Hills module's external and embedded model/texture dependencies.
Read-only for Assets. Writes a temporary audit report only; --stage also extracts source files for repair.
"""
import base64, hashlib, io, json, re, sys
from pathlib import Path
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
GAME=Path('C:/Users/ruze/OneDrive/Desktop/Slow Roads')
SRC=GAME/'extracted_correct/build'
ART=ROOT/'Assets/SlowRoads/Art/Reference/SlowRoads'
OUT=ROOT/'Temp/HillsAssetAudit'; OUT.mkdir(parents=True,exist_ok=True)
STAGE='--stage' in sys.argv
manifest=json.loads((GAME/'asset_manifest.json').read_text('utf-8-sig'))
by_src={x['src']:x['dst'] for x in manifest}
modules=['HillsHeightmap.2938f21e.js','tree_04.6b3d8a5d.js']
items=[]; seen=set()
def category(name):
 if name.startswith('tree_') and name.endswith('.obj'): return 'Models/Trees'
 if name.endswith('.obj'): return 'Models/Props'
 if any(s in name for s in ['wall_','barrier','drystone']): return 'Textures/Walls'
 if any(s in name for s in ['road','bridge','signs']): return 'Textures/Road'
 if 'cloud' in name: return 'Textures/Sky'
 if 'sea_waves' in name: return 'Textures/Water'
 if any(s in name for s in ['noise_','placeholder_','snow_particle']): return 'Textures/Utility'
 if any(s in name for s in ['trees','imposter','bush','heather','forest','grasses']): return 'Textures/Vegetation'
 return 'Textures/Terrain'
def add(source,name,data,origin,old=None):
 if source in seen:return
 seen.add(source)
 if name.endswith('.webp'):
  image=Image.open(io.BytesIO(data)).convert('RGBA'); buf=io.BytesIO();image.save(buf,format='PNG');data=buf.getvalue();name=name[:-5]+'.png'
 target='Assets/SlowRoads/Art/Reference/SlowRoads/'+category(name)+'/'+name
 if old: old='Assets/SlowRoads/Art/Reference/SlowRoads/'+re.sub(r'\.webp$','.png',old)
 stage=OUT/name
 if STAGE: stage.write_bytes(data)
 existing=ROOT/old if old else ROOT/target
 if not existing.exists() and (ROOT/target).exists(): old=target;existing=ROOT/target
 if old is None and existing.exists(): old=target
 status='missing'
 if existing.exists():
  if name.endswith('.png'):
   a=Image.open(existing).convert('RGBA');b=Image.open(io.BytesIO(data)).convert('RGBA');same=a.size==b.size and a.tobytes()==b.tobytes()
  else:same=existing.read_bytes()==data
  status='match' if same else 'incorrect'
 items.append(dict(source=source,origin=origin,path=target,old=old if existing.exists() else None,stage=str(stage) if STAGE else None,status=status,sha256=hashlib.sha256(data).hexdigest()))
for module in modules:
 text=(SRC/'_app/immutable/chunks'/module).read_text('utf-8')
 for url in re.findall(r'new URL\("([^\"]+)"',text):
  if not url.endswith(('.obj','.glb','.webp','.png','.jpg')):continue
  rel='_app/immutable/'+url.removeprefix('../'); name=re.sub(r'\.[a-f0-9]{8}(\.[^.]+)$',r'\1',Path(url).name)
  add(rel,name,(SRC/rel).read_bytes(),module,by_src.get(rel))
 if module.startswith('Hills'):
  names={'Ee':'wall_barrier_normal.webp','Te':'wall_fence_normal.webp','Z':'placeholder_white.webp','is':'snow_particle.webp','_s':'verge_marker.obj'}
  for symbol,mime,payload in re.findall(r'([\w$]+)="data:([^;]+);base64,([^\"]+)"',text):
   if symbol not in names:raise ValueError('Unclassified inline asset '+symbol)
   add(module+'#'+symbol,names[symbol],base64.b64decode(payload),module+' embedded '+symbol)
kept_old={x['old'] for x in items if x['old']}
remove=[]
for folder in ['Models','Textures']:
 for p in (ART/folder).rglob('*'):
  if p.is_file() and p.suffix!='.meta' and p.relative_to(ROOT).as_posix() not in kept_old:
   remove.append(p.relative_to(ROOT).as_posix())
# Corresponding obsolete generated materials/prefabs must be removed with their source models.
for p in list(remove):
 if '/Models/' in p:
  relative=Path(p.split('/Models/')[1]);
  for folder,ext in [('Materials','.mat'),('Prefabs','.prefab')]:
   q=ART/folder/relative.with_suffix(ext)
   if q.exists():remove.append(q.relative_to(ROOT).as_posix())
report=dict(scope='Hills four seasons + shared tree module + embedded dependencies; no vehicles/California/OffWorld/UI backgrounds',modules=modules,items=items,remove=remove)
(OUT/'audit.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),'utf-8')
from collections import Counter
print('Expected',len(items),Counter(x['status'] for x in items),'Remove',len(remove))
for x in items:
 if x['status']!='match':print(x['status'],x['path'])
print('Remove models:',[p.split('/Models/')[1] for p in remove if '/Models/' in p])


