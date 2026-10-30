// Hash-pinned Ll/i_ oracle. SourceAtlas is evidence, never executable input.
const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm'),assert=require('node:assert/strict');
const base=require('./heightfield-reference.cjs'),mid=require('./midline-reference.cjs'),road=require('./road-mesh-reference.cjs');
const crypto=require('node:crypto'),zlib=require('node:zlib');const sha=s=>crypto.createHash('sha256').update(s).digest('hex');
const fields=['h','gfa','gt','ga','rd','w','d','y','blend','slope'];
const bits=v=>{const b=Buffer.alloc(8);b.writeDoubleBE(v);return b.toString('hex');};
function install(runtime,lane,installed=null){
 const {ctx,input}=runtime,h=input.sources.Hills.toString(),n=input.sources.Normals.toString();
 const width=base.section(h,'xl=t=>{',',cr=()=>{');
 const source=base.section(h,'const wl=new be(', ',Hl=Object.freeze');
 const project=base.section(n,'i_=(a,e,t)=>',',Lx=');
 const mesh=installed??road.install(runtime,lane,false);
 ctx.blendLane=lane;ctx.blendWidth=mesh.configuration.halfWidth;
 // Execute xl itself, including N/F/We/Ve/me; do not derive oracle flags from C#.
 vm.runInContext('globalThis.blend=(function(){const be=Gradient;let N=false,F=false,We=3,Ve=2,me=.2,O,re=.4,Lt,yt,ra,Ea,pe,na,la,Oe,He,xl;'+
  'const te={roads:{style:{3:roadProfile},laneWidth}},Cr=laneCounts,z=laneEnum,Mt=400,cr=()=>{yt=20-O;};'+width+';xl({roadStyle:3,laneStyle:blendLane});'+source+
  ';return {evaluate:Ll,diagnostics:()=>({...u}),curve:gr,noise:(x,z)=>[wl,jt,bl,Ml].map(n=>n.getXZ(x,z)),config:{N,F,We,Ve,me,O,re,Lt,yt}};})()',ctx);
 vm.runInContext('globalThis.project=(function(){const x={};const '+project+';return i_;})()',ctx);
 return {configuration:JSON.parse(JSON.stringify(ctx.blend.config)),sections:{Ll:sha(source),projection:sha(project),width:sha(width)}};
}
function link(nodes){nodes.forEach((n,i)=>{n.prev=nodes[i-1];n.next=nodes[i+1];});return nodes;}
function encode(n){const v={i:n.i,p:[n.p.x,n.p.y,n.p.z],side:[n.n.x,n.n.y,n.n.z],da:n.da,g:n.g,gfa:n.gfa,h:n.h,blend:n.blend,bank:n.bank,bridge:!!n.bridge};v.bits=[...v.p,...v.side,v.da,v.g,v.gfa,v.h,v.blend,v.bank].map(bits);return v;}
function query(ctx,nodes,x,z,H,start=3,distance=null){
 const p=ctx.project(x,z,nodes[start]);if(distance!==null)p.d=distance;const before={index:p.n.i,d:p.d,y:p.y,da:p.da,s:p.s,g:p.g,t:p.t};
 const height=ctx.blend.evaluate(H,p,x,z);
 assert(Number.isFinite(height),'Non-finite reference output');
 const v={x,z,H,start,direct:distance!==null,...before,height,values:fields.map(k=>p[k]??0),present:fields.map(k=>Object.hasOwn(p,k)),noise:Array.from(ctx.blend.noise(x,z))};
 v.bits=[x,z,H,before.d,before.y,before.da,before.s,before.g,before.t,height,...v.values,...v.noise].map(bits);return v;
}
function build(){return mid.createReference('style3','a',false,'archive-heightfield',{updates:0,full:true,consumeRuntime:runtime=>{
 const {ctx}=runtime;const cases=[],sections=[];
 for(let lane=0;lane<5;lane++){
  const info=install(runtime,lane);sections.push(info);
  for(let bridge=0;bridge<5;bridge++)for(const bank of [-.7,0,.7])for(const grade of [-5,0,5]){
   const nodes=link(Array.from({length:9},(_,i)=>({i,p:{x:i*10-45,y:6+i*.15,z:-25},n:{x:0,y:0,z:1},da:i*.02,g:grade+i*.1,gfa:i*.07,h:i*.13,blend:i/10,bank,bridge:bridge===1?i>=3:bridge===2?i>=2&&i<=4:bridge===3?i>=4&&i<=5:bridge===4?i>=2&&i<=6:false})));
   const c={lane,nodes:nodes.map(encode),queries:[]},O=info.configuration.O;
   const distances=[0,.01,1.7,O/2,O-.4,O,O+.4,O+1,O+4,O+5,O+16,O+20];
   for(const d of distances)for(const epsilon of [-1e-7,0,1e-7])for(const side of [-1,1]){
    const x=-10.35,z=-25+side*Math.max(0,d+epsilon),H=bridge%2?12:-3;
    c.queries.push(query(ctx,nodes,x,z,H));
   }
   // Exact Ll branch boundaries: independent synthetic projection distances, not
   // a claim that these are physically projected distances at the selected XZ.
   for(const side of [-1,1]){
    const x=-10.35,z=-25+side*3,p=ctx.project(x,z,nodes[3]),k=info.configuration;
    const gt=p.n.g+p.t*(p.n.next.g-p.n.g),ga=p.s===Math.sign(p.g)?Math.max(0,Math.abs(gt)-1)/3:0;
    const w=k.O+(k.N?(k.F?k.re*2:k.re+ga):k.re+.25+ga),blend=p.n.blend+p.t*(p.n.next.blend-p.n.blend),rm=k.yt*(.4+.6*blend);
    const shift=k.N?.4+(k.F?.2:0)+ctx.blend.noise(x,z)[1]*(k.F?.5:1):0;
    for(const target of [k.Lt,w,w-k.Ve,w+5,w+rm,w+16,k.O+k.re+4,1.7])for(const epsilon of [-1e-7,0,1e-7])
     if(target+epsilon-shift>=0)c.queries.push(query(ctx,nodes,x,z,bridge%2?12:-3,3,target+epsilon-shift));
   }
   cases.push(c);
  }
 }
 return {schema:1,hashes:runtime.sourceHashes,sections,fields,cases,curve:[0,.3333-1e-7,.3333,.3333+1e-7,1].map(t=>({t,value:ctx.blend.curve(t)}))};
}});}
if(require.main===module&&process.argv.includes('--terrain-layout')){
 const value=terrainContextBuild(false,{seed:'a',viewDistance:0,tileX:0,tileZ:0,layoutOnly:true}),json=JSON.stringify(value)+'\n';
 const file=path.join(__dirname,'Fixtures/RoadBlend/terrain-layout.json');
 if(process.argv.includes('--write'))fs.writeFileSync(file,json);else assert.equal(fs.readFileSync(file,'utf8'),json);
 console.log('original U/Ma finite corridor collection PASS');
}
else if(require.main===module&&process.argv.includes('--terrain-visual')){terrainVisualMain().catch(e=>{console.error(e);process.exitCode=1;});}
else if(require.main===module&&process.argv.includes('--terrain-material-gpu')){terrainMaterialGpuMain();}
else if(require.main===module&&process.argv.includes('--terrain-material')){terrainMaterialMain();}
else if(require.main===module&&process.argv.includes('--terrain-streaming')){terrainStreamingMain();}
else if(require.main===module&&process.argv.includes('--terrain-boundaries')){terrainBoundaryMain();}
else if(require.main===module&&process.argv.includes('--terrain-chunk-source')){
 const at=process.argv.indexOf('--terrain-chunk-source'),spec=JSON.parse(fs.readFileSync(process.argv[at+1],'utf8'));
 fs.writeFileSync(process.argv[at+2],JSON.stringify(terrainContextBuild(false,{...spec,sourceChunk:true,viewDistance:2,tileX:0,tileZ:0})));
}
else if(require.main===module&&process.argv.includes('--terrain-chunk-input')){
 const at=process.argv.indexOf('--terrain-chunk-input'),input=JSON.parse(fs.readFileSync(process.argv[at+1],'utf8'));
 assert.equal(input.schema,1);assert(input.nodes.length<=256&&input.cells.length<=100);
 const value=terrainContextBuild(false,{seed:input.seed,viewDistance:2,tileX:0,tileZ:0,chunkInput:input});
 fs.writeFileSync(process.argv[at+2],JSON.stringify(value));
}
else if(require.main===module&&process.argv.includes('--terrain-tile')){terrainTileMain();}
else if(require.main===module&&process.argv.includes('--terrain-topology')){terrainTopologyMain();}
else if(require.main===module&&process.argv.includes('--terrain-context')){terrainContextMain();}
else if(require.main===module&&process.argv.includes('--terrain-query')){terrainMain();}
else if(require.main===module){
 if(process.argv.includes('--self-test')){const a=base.readArchive(['Normals']);base.verifyHashes(a.sources,['Dev','Hills','Settings','Rand','Normals']);const bad={...a.sources,Hills:Buffer.from('bad')};assert.throws(()=>base.verifyHashes(bad));for(const s of ['none','start end start','end start'])assert.throws(()=>base.section(s,'start','end'));console.log('blend extraction rejection self-test PASS');process.exit(0);}
 const file=path.join(__dirname,'Fixtures/RoadBlend/hills-road-blend.json');const value=build(),json=JSON.stringify(value)+'\n';
 if(process.argv.includes('--write')){fs.mkdirSync(path.dirname(file),{recursive:true});fs.writeFileSync(file,json);}else assert.equal(fs.readFileSync(file,'utf8'),json,'Road blend reference drift');
 console.log(JSON.stringify({cases:value.cases.length,queries:value.cases.reduce((s,c)=>s+c.queries.length,0),sections:value.sections}));}
module.exports={install,query,link,encode,fields};


// T0/T1 extension: same archive, VM, Pl, midline and Ll. No translated JS oracle.
function terrainInstall(runtime,lane){
 const info=install(runtime,lane),{ctx,input}=runtime;
 const normals=input.sources.Normals.toString(),settings=input.sources.Settings.toString();
 const fine=base.section(normals,'$u=(a,e,t)=>',',i_=(a,e,t)=>');
 const uSource=mid.classSection(settings,'class U{').source;
 const bilerp=base.section(uSource,'static bilerpHeight(e,t,a){','static bilerpCurvature(');
 const complete=base.section(uSource,'static getCompleteHeight(e,t,a,n,d,p=!0,u=!1){','loadCustom0(){');
 const addCell=base.section(uSource,'addCell(e,t,a){','hasCell(e,t){');
 const cellClass=mid.classSection(settings,'class At{').source;
 vm.runInContext('globalThis.terrain=(function(){const x={},Ve=shared,xd=false;const '+fine+';const m2=$u,Z4=project,S1={},d0={};let t0=20;'+
 'const calls=[],a0={getXZ:(x,z)=>{const v=terrainHeight(x,z);calls.push([x,z,v]);return v;}},Tt=(h,p,x,z)=>{before={...p};inputHeight=h;return blend.evaluate(h,p,x,z);};let before,inputHeight;'+
 'class U{'+bilerp+complete+'}const C=10,H=840,_=0,h1=10000,k1=100,g={},s=(o,k,v)=>o[k]=v;'+cellClass+
 ';const It=At;class Chunk{'+addCell+'}return {project:$u,complete:(cell,lx,lz,x,z,p,u,res)=>{t0=res;calls.length=0;const height=U.getCompleteHeight(cell,lx,lz,x,z,p,u);return {height,baseHeight:inputHeight,projection:d0.m,before,trace:blend.diagnostics(),calls:calls.slice()};},'+
 'candidates:(nodes,indices,cx,cz)=>{const chunk={parentTileMap:{},lastSeenIndex:-1,cellMap:{},prevChunk:{cellMap:{}},cellPool:[],bounds:{},cellCount:0};'+
 'for(const i of indices)Chunk.prototype.addCell.call(chunk,cx,cz,nodes[i]);const c=chunk.cellMap[cx+h1][cz+h1];return {closest:c.closestNode.i,others:c.closeNodes.map(n=>n.i)};}};})()',ctx);
 const tail={i:0,p:{x:0,y:0,z:0},n:{x:0,z:1},next:{p:{x:10,z:0}}};
 assert.throws(()=>ctx.terrain.project(0,0,tail),/Midline failed to generate beyond tail/);
 const far=ctx.terrain.project(1000,0,tail);assert.equal(far.t1,0); // Original far-tail fallback; production rejects incomplete support.
 return {...info,terrainSections:{fine:sha(fine),bilerp:sha(bilerp),complete:sha(complete),addCell:sha(addCell),cell:sha(cellClass)},source:{fine,bilerp,complete,addCell}};
}
function terrainBuild(){
 const groups=[];
 function collect(runtime,lane,style,label,seed='a'){
  const {ctx,generator}=runtime,info=terrainInstall(runtime,lane);
  ctx.terrainHeight=(x,z)=>generator.heightmap.getXZ(x,z);
  const all=[];for(let n=ctx.shared.head;n;n=n.next)all.push(n);
  const head=all[0].i,tail=ctx.shared.fineTail.i;
  const usable=all.filter(n=>n.i>=head+5&&n.i<tail-8),sharp=usable.reduce((a,n)=>!a||Math.abs(n.da)>Math.abs(a.da)?n:a,null);
  const crest=usable.find(n=>n.prev?.g>0&&n.next?.g<=0),trough=usable.find(n=>n.prev?.g<0&&n.next?.g>=0);
  const targets=[head+12,head+25,...[sharp,crest,trough].filter(Boolean).map(n=>n.i),...all.filter(n=>n.bridge&&n.i<tail-4).map(n=>n.i)];
  const queries=[];const used=new Set();
  for(const target of targets){
   if(target>=tail-4||target<head+3)continue;
   // Include bridge approach and departure; bounded natural data, not whole history.
   for(const offset of [-2,0,1,2]){
    const n=all.find(n=>n.i===target+offset);if(!n?.nextFine?.nextFine||n.i>=tail-3||used.has(n.i))continue;used.add(n.i);
    for(const lateral of [-25,-4,-.0000001,0,.0000001,4,25])for(const location of (lateral===0?['sample','tl','tr','bl','br','outside']:['sample'])){
     let x=n.p.x+(n.nextFine.p.x-n.p.x)*.43+n.n.x*lateral,z=n.p.z+(n.nextFine.p.z-n.p.z)*.43+n.n.z*lateral;
     const cx=Math.floor(x/10),cz=Math.floor(z/10),ox=cx*10,oz=cz*10;
     if(location!=='sample'){x=ox+(location==='tr'||location==='br'?10:location==='outside'?-0.5:0);z=oz+(location==='bl'||location==='br'?10:0);}
     const order=[n.i-1,n.i+1,n.i-1]; // Actual addCell consumes and preserves this order.
     const candidates=ctx.terrain.candidates(Object.fromEntries(all.map(n=>[n.i,n])),order,cx,cz);
     const corners=[generator.heightmap.getXZ(ox,oz),generator.heightmap.getXZ(ox+10,oz),generator.heightmap.getXZ(ox,oz+10),generator.heightmap.getXZ(ox+10,oz+10)];
     const cell={closestNode:all.find(n=>n.i===candidates.closest),closeNodes:candidates.others.map(i=>all.find(n=>n.i===i)),hTL:corners[0],hTR:corners[1],hBL:corners[2],hBR:corners[3]};
     for(const fine of [false,true])for(const bilinear of [false,true]){
      const resolution=location==='outside'?16:info.configuration.N?24:style==='style0'?10:20;
      const lx=(x-ox)*resolution/10,lz=(z-oz)*resolution/10;
      const r=ctx.terrain.complete(cell,lx,lz,x,z,bilinear,fine,resolution),p=r.projection,b=r.before;
      const nf=p.nf??p.n;const fi=nf.i??nf.prev.i,ordinal=nf.i!=null?0:(()=>{let j=0;for(let a=nf.prev;a&&a!==nf;a=a.nextFine)j++;return j;})();
      const nums=[x,z,lx,lz,...corners,r.height,b.d,b.y,b.da,b.s,b.g,b.t,b.t1??0,...fields.map(k=>p[k]??0)];
      const inside=p.d<p.w+r.trace.rm,center=!info.configuration.N&&p.d<info.configuration.Lt&&!p.n.bridge;
      queries.push({resolution,baseHeight:bits(r.baseHeight),trace:{rm:r.trace.rm,widthLerp:info.configuration.N?r.trace.widthLerp:1,weight:inside?(center?0:r.trace.lt):1,inside,center:inside&&center,bridge:inside&&!center&&!!p.n.bridge&&!!p.n.next.bridge},input:nums.map(bits),candidates:[candidates.closest,...candidates.others],fine,bilinear,index:p.n.i,fineIndex:fi,ordinal,present:fields.map(k=>Object.hasOwn(p,k)),calls:r.calls.map(v=>({bits:v.map(bits)}))});
     }
    }
   }
  }
  // Export only the referenced bounded chain, including exact IEEE input bits.
  const nodes=all.filter(n=>n.i>=head&&n.i<=Math.max(...used)+4).map(n=>({...encode(n),fine:[]}));
  for(const n of nodes){let original=all.find(a=>a.i===n.i);for(let f=original.nextFine;f&&f.i==null;f=f.nextFine)n.fine.push({bits:[f.p.x,f.p.y,f.p.z,f.n.x,f.n.y,f.n.z].map(bits)});}
  groups.push({lane,style,label,seed,classification:{maxAbsHeadingDelta:Math.abs(sharp?.da??0),sharp:sharp?.i??null,crest:crest?.i??null,trough:trough?.i??null},midlineConfig:JSON.parse(JSON.stringify(generator.config)),configuration:info.configuration,sections:info.terrainSections,nodes,queries});
 }
 for(const lane of [4,1,0]){
 for(const style of ['style3','style0'])mid.createReference(style,'a',false,'archive-heightfield',{updates:4100,full:true,halfWidth:lane===4?3.7:lane===1?1.8:3.6,consumeRuntime:r=>collect(r,lane,style,'initial')});
 mid.createReference('style3','a',false,'archive-heightfield',{updates:0,full:true,oneDir:true,fineHorizon:400,halfWidth:lane===4?3.7:lane===1?1.8:3.6,consumeRuntime:r=>{
 const {ctx,generator}=r;
 function warm(){let k=0;for(;k<60000&&(ctx.shared.tail.i<ctx.shared.vehicleIndex+2500||ctx.shared.fineTail.i<ctx.shared.vehicleIndex+400);k++)generator.update();assert(k<60000,'terrain reference warmup limit');}
 warm();for(const observer of [2300,4950]){while(ctx.shared.vehicleIndex<observer){const next=Math.min(observer,ctx.shared.vehicleIndex+32);ctx.shared.vehicleIndex=next;let at=ctx.shared.head;while(at.next&&at.next.i<=next)at=at.next;ctx.shared.vehicleNode=at;warm();}assert(ctx.shared.head.i>=observer-30,'unexpected generation restart');collect(r,lane,'style3','oneDir-observer-'+observer);}
 }});
 }
 // Supplemental natural seeds and paved widths, without repeating the long bridge trace.
 for(const seed of ['1337','hills-c4'])for(const lane of [2,3,4])
  mid.createReference('style3',seed,false,'archive-heightfield',{updates:4100,full:true,halfWidth:lane===2?1.8:lane===3?3.6:3.7,
   consumeRuntime:r=>collect(r,lane,'style3','C4-natural',seed)});
 return {schema:1,kind:'U.getCompleteHeight/$u/Ll',groups};
}
function terrainEvidence(){
 const input=base.readArchive(['Normals']);const hashes=base.verifyHashes(input.sources,['Dev','Hills','Settings','Rand','Normals']);assert.equal(input.version,'1.0.2');
 // Supplementary line map: break after each semicolon outside quoted strings.
 // It is intentionally NOT the historical full-module formatter/Atlas line numbering.
 const text=input.sources.Normals.toString();const mappings=[];
 for(const [symbol,start,end] of [['$u','$u=(a,e,t)=>',',i_=(a,e,t)=>'],['i_','i_=(a,e,t)=>',',Lx=']]){
  const source=base.section(text,start,end),offset=base.uniqueIndex?base.uniqueIndex(text,start):text.indexOf(start);
  let quote=null,escaped=false,line='',rawStart=offset,lines=[];
  for(let i=0;i<source.length;i++){const c=source[i];line+=c;if(quote){if(escaped)escaped=false;else if(c==='\\')escaped=true;else if(c===quote)quote=null;}else if(c==='"'||c==="'"||c==='`')quote=c;else if(c===';'){lines.push({line:lines.length+1,start:rawStart,end:offset+i+1,text:line});line='';rawStart=offset+i+1;}}
  if(line)lines.push({line:lines.length+1,start:rawStart,end:offset+source.length,text:line});
  assert.equal(lines.map(l=>l.text).join(''),source);mappings.push({symbol,offset,sha256:sha(source),lines});
 }
 const hills=input.sources.Hills.toString(),settings=input.sources.Settings.toString();
 const graphics=base.literal(hills,'{viewDistance:[{tileSize:240');
 assert(settings.includes('$e(I0._value),this.loadGraphicsSettings()'));
 assert(settings.includes('loadGraphicsSettings(){Xe(R.viewDistance,R.detail)'));
 const expanded=graphics.viewDistance.map((r,viewDistance)=>{const H=r.tileSize,B4=r.lod1Horizon,l2=Math.max(40,Math.sqrt(H)*4),H4=l2*1.25;
 return {viewDistance,tileSize:H,cellSize:20,cellWidth:10,fineHorizon:r.midlineFineHorizon,B4,l2,H4,Mt:B4*4,s5:Math.min(-6,-.4*B4),W4:Math.min(-12,-.4*l2)};});
 const archiveHash=crypto.createHash('sha256'),fd=fs.openSync(path.join(__dirname,'../../../Slow Roads/resources/app.asar'),'r'),chunk=Buffer.alloc(1024*1024);try{let n;while((n=fs.readSync(fd,chunk,0,chunk.length,null))>0)archiveHash.update(chunk.subarray(0,n));}finally{fs.closeSync(fd);}
 return {schema:1,version:input.version,archiveSHA256:archiveHash.digest('hex'),hashes,graphics:{expanded,cellRes:graphics.detail.map(d=>d.cellRes),offroadCellRes:24,order:'xl(world), then _l(viewDistance,detail); Settings L50077-L50078 / L49942'},
 geometryContext:{fixture:'terrain-context-smoke.json',command:'road-blend-reference.cjs --terrain-context',symbols:{f0:'Settings L41004-L41194',U:'Settings L41203-L41782',Dt:'Settings L42616-L43040',i7:'Settings L44973-L45050',s0:'Settings L46126-L46217'},
 contract:'Constructors and minimal height/normal/index execution only. Content managers and sea storage are explicit constructor stubs; no full mesh acceptance. Native genRow & !1 preserved. Cell registration including prevChunk must finish before capturing a batch; parent Tile must be Staged before full U generation.'},
 formatter:'terrain-semicolon-map-v1 (raw statements, quoted strings preserved)',lineNamespace:'supplementary TQ-L; not SourceAtlas L',mappings};
}
function terrainMain(){
 const evidence=terrainEvidence();const evidenceFile=path.join(__dirname,'../TerrainAudit/terrain-source-map.json');
 const evidenceJson=JSON.stringify(evidence,null,2)+'\n';
 if(process.argv.includes('--write'))fs.writeFileSync(evidenceFile,evidenceJson);else assert.equal(fs.readFileSync(evidenceFile,'utf8'),evidenceJson);
 if(process.argv.includes('--evidence-only')){console.log('terrain identity/line-map PASS');return;}
 const result=terrainBuild();result.evidence=sha(evidenceJson);
 validateTerrainFixture(result,result.evidence);
 const sample={...result,groups:[result.groups[0]]};
 for(const mutate of [v=>v.schema=99,v=>delete v.groups[0].queries[0].input,v=>v.groups[0].queries[0].input[0]='bad',v=>v.groups[0].sections.fine='0'.repeat(64),v=>v.evidence='0'.repeat(64)]){
  const bad=JSON.parse(JSON.stringify(sample));mutate(bad);assert.throws(()=>validateTerrainFixture(bad,result.evidence));
 }
 const file=path.join(__dirname,'Fixtures/RoadBlend/terrain-query.json.gz'),json=JSON.stringify(result)+'\n';
 if(process.argv.includes('--write'))fs.writeFileSync(file,zlib.gzipSync(json));else assert.equal(zlib.gunzipSync(fs.readFileSync(file)).toString(),json,'Terrain reference drift');
 console.log(JSON.stringify({groups:result.groups.length,queries:result.groups.reduce((s,g)=>s+g.queries.length,0),evidence:result.evidence}));
}

function validateTerrainFixture(value,evidence){
 const schema=JSON.parse(fs.readFileSync(path.join(__dirname,'Fixtures/RoadBlend/terrain-query.schema.json'),'utf8'));
 function check(v,s){
  if(s.$ref)s=schema.$defs[s.$ref.split('/').pop()];
  if(s.const!==undefined)assert.equal(v,s.const);
  if(s.enum)assert(s.enum.includes(v));
  if(s.type==='object'){assert(v&&typeof v==='object'&&!Array.isArray(v));for(const k of s.required??[])assert(Object.hasOwn(v,k),'missing '+k);for(const [k,p]of Object.entries(s.properties??{}))if(Object.hasOwn(v,k))check(v[k],p);}
  if(s.type==='array'){assert(Array.isArray(v));if(s.minItems!==undefined)assert(v.length>=s.minItems);if(s.maxItems!==undefined)assert(v.length<=s.maxItems);if(s.items)for(const a of v)check(a,s.items);}
  if(s.type==='string'){assert.equal(typeof v,'string');if(s.pattern)assert(new RegExp(s.pattern).test(v));}
  if(s.type==='integer'){assert(Number.isInteger(v));if(s.minimum!==undefined)assert(v>=s.minimum);if(s.maximum!==undefined)assert(v<=s.maximum);}
  if(s.type==='number')assert(Number.isFinite(v));
  if(s.type==='boolean')assert.equal(typeof v,'boolean');
 }
 check(value,schema);assert.equal(value.evidence,evidence);
 const a=base.readArchive(['Normals']),s=a.sources.Settings.toString(),n=a.sources.Normals.toString(),u=mid.classSection(s,'class U{').source;
 const sections={fine:sha(base.section(n,'$u=(a,e,t)=>',',i_=(a,e,t)=>')),bilerp:sha(base.section(u,'static bilerpHeight(e,t,a){','static bilerpCurvature(')),complete:sha(base.section(u,'static getCompleteHeight(e,t,a,n,d,p=!0,u=!1){','loadCustom0(){')),addCell:sha(base.section(u,'addCell(e,t,a){','hasCell(e,t){')),cell:sha(mid.classSection(s,'class At{').source)};
 for(const g of value.groups)assert.deepEqual(g.sections,sections,'section identity');
}

// C1: executable geometry-context smoke in the existing oracle. Containers store
// native typed arrays only; all geometry arithmetic and vector operations are original.
function terrainContextBuild(fullTile=false,spec={seed:'a',viewDistance:2,tileX:-1,tileZ:0}){
 const nativeRoad=spec.chunkInput?.nativeNodes;
 return mid.createReference('style3',spec.seed,false,'archive-heightfield',{updates:nativeRoad?0:4100,full:true,oneDir:!!nativeRoad,fineHorizon:nativeRoad?400:undefined,halfWidth:spec.chunkInput?.roadWidth,consumeRuntime:runtime=>{
  const {ctx,input,generator}=runtime;terrainInstall(runtime,spec.chunkInput?.lane??4);
  if(nativeRoad){
   function warm(){let guard=0;while(ctx.shared.tail.i<ctx.shared.vehicleIndex+2500||ctx.shared.fineTail.i<ctx.shared.vehicleIndex+400){generator.update();assert(++guard<60000,'Native road bounded warmup');}}
   warm();const target=Math.max(0,nativeRoad[0]-20);
   while(ctx.shared.vehicleIndex<target){ctx.shared.vehicleIndex=Math.min(target,ctx.shared.vehicleIndex+32);let n=ctx.shared.head;while(n.next&&n.next.i<=ctx.shared.vehicleIndex)n=n.next;ctx.shared.vehicleNode=n;warm();}
  }
  if(spec.sourceChunk){
   terrainInstall(runtime,spec.lane);
   const all=[];for(let n=ctx.shared.head;n;n=n.next)all.push(n);
   let target=spec.bridge?all.find(n=>n.i>all[0].i+12&&n.i<ctx.shared.fineTail.i-26&&n.bridge&&n.next?.bridge):all[15];
   assert(target,'Natural source target unavailable');
   if(spec.bridge&&spec.bridgePart===1)target=target.next.next;
   if(spec.bridge&&spec.bridgePart===2){while(target.next?.bridge)target=target.next;target=target.next;}
   const ns=all.filter(n=>n.i>=target.i-12&&n.i<=target.i+24);
   const nodes=ns.map(n=>{const fine=[];for(let f=n.nextFine;f&&f.i===undefined;f=f.nextFine)fine.push({bits:[f.p.x,f.p.y,f.p.z,f.n.x,f.n.y,f.n.z,f.shadowLeft??0,f.shadowRight??0].map(bits)});
    return {i:n.i,bridge:!!n.bridge,bits:[n.p.x,n.p.y,n.p.z,n.n.x,n.n.y,n.n.z,n.da,n.g,n.gfa,n.h,n.blend,n.bank,n.shadowLeft??0,n.shadowRight??0].map(bits),fine};});
   const x=Math.floor(target.p.x/10),z=Math.floor(target.p.z/10);
   const cells=[{x,z,inner:true,candidates:[target.i,target.i+1]},{x,z:z+1,inner:true,candidates:[target.i]},{x:x+1,z,inner:false,candidates:[target.i]}];
   spec.chunkInput={schema:1,seed:spec.seed,lane:spec.lane,resolution:spec.lane<=1?24:[10,16,20,20,20][spec.detail],roadWidth:ctx.blend.config.O,
    negativeLayer:ctx.layerState.value[0],positiveLayer:ctx.layerState.value[1],nodes,cells};
  }
  ctx.contextHeight=generator.heightmap;
  if(spec.layers)generator.heightmap.setLayers(spec.layers,true);
  ctx.chunkInput=spec.chunkInput??null;
  if(ctx.chunkInput?.nativeNodes){
   const [first,last]=ctx.chunkInput.nativeNodes,all=[];for(let n=ctx.shared.head;n;n=n.next)if(n.i>=first&&n.i<=last)all.push(n);
   assert.equal(all.length,last-first+1,'Original road source range unavailable');
   ctx.chunkInput={...ctx.chunkInput,nodes:all.map(n=>{const fine=[];for(let f=n.nextFine;f&&f.i===undefined;f=f.nextFine)fine.push({bits:[f.p.x,f.p.y,f.p.z,f.n.x,f.n.y,f.n.z,f.shadowLeft??0,f.shadowRight??0].map(bits)});
    return {i:n.i,bridge:!!n.bridge,bits:[n.p.x,n.p.y,n.p.z,n.n.x,n.n.y,n.n.z,n.da,n.g,n.gfa,n.h,n.blend,n.bank,n.shadowLeft??0,n.shadowRight??0].map(bits),fine};})};
  }
  if(ctx.chunkInput){ctx.decodeDouble=v=>Buffer.from(v,'hex').readDoubleBE();generator.heightmap.setLayers([ctx.chunkInput.negativeLayer,ctx.chunkInput.positiveLayer],true);}
  const source=input.sources.Settings.toString(),pieces={};
  for(const [name,marker] of Object.entries({grid:'class f0 extends D0{',tile:'class Dt{',cell:'class At{',chunk:'class U{',hillsTile:'class i7 extends Dt{',hillsChunk:'class s0 extends v2{'}))pieces[name]=mid.classSection(source,marker).source;
  const ma=mid.classSection(source,'class Ma{').source;
  const collection=base.section(ma,'if(this.prevCenter.x=this.center.x,this.prevCenter.z=this.center.z,this.chunks.center.x=', 'if(this.tilesPendingOverlapUpdate.length)');
  const rotate=base.section(ma,'stageCurrentChunk(){','initSchedule(){}');
  const code=`(function(){
   const s=(o,k,v)=>o[k]=v,q=Vec3,G=0,I5={},x0={Dead:0};
   class B{constructor(array,itemSize){this.array=array;this.itemSize=itemSize;}setUsage(){}setXYZ(i,x,y,z){this.array[i*3]=x;this.array[i*3+1]=y;this.array[i*3+2]=z;}}
   class D0{constructor(){this.attributes={};}setAttribute(k,v){this.attributes[k]=v;}setIndex(a){this.index={array:new Uint16Array(a)};}setDrawRange(start,count){this.drawRange={start,count};}}
   class c0{constructor(){this.position=new q();}add(){}}
   class P0{constructor(geometry){this.geometry=geometry;this.position=new q();this.rotation=new q();}updateMatrix(){}}
   class B0{constructor(){this.center=new q();}}
   let A3=1,w3=0;const H=${[240,480,840,1500,2000,2500][spec.viewDistance]},Nt=Math.round(H/20),F=Nt*2,Z=F+1,C=10,i5=20,O0=F-1;
   const t0=${spec.chunkInput?.resolution??20},k0=t0+1,k1=Math.min(100,20+H/10),m0=C/t0,y1=${spec.chunkInput?.roadWidth??3.7};
   const a0=contextHeight,o={vecV:new q(),vecH:new q()},g={},D={},S1={},d0={},v={},E0={vecX:new q(),vecZ:new q()};
   const m2=terrain.project,Z4=project,Tt=blend.evaluate,r={},l1=chunkInput?blend.config.N:true;
   const h1=10000,_=0,s5=0,W4=0,H4=0,l2=0,Mt=0,B4=0,z1={Staging:1},e0={addJob(){}};
   ${pieces.cell};const It=At;
   ${pieces.grid};const w1=f0;${pieces.tile};${pieces.chunk};const v2=U;${pieces.hillsChunk};
   const Z3=D0,i2=1,x4=2,$3={},d5={};class e7{constructor(){this.container=new c0();}}class Ka extends e7{}
   ${pieces.hillsTile};
   f0.reset();
   if(${!!spec.layoutOnly}){
    const S3=U,U2=[],l={};class Collector{${rotate} step(){${collection}this.chunks.prevCenter.x=this.chunks.center.x;this.chunks.prevCenter.z=this.chunks.center.z;}registerOverlapCell(){}}
    const m=new Collector();m.container=new c0();m.tiles={};m.prioChunks=0;m.center={x:0,z:0};m.prevCenter={x:0,z:0};m.lookaheadNode={i:0};
    m.chunks={current:new U(),prev:null,stage:[],center:{x:-3,z:-3},prevCenter:{x:-3,z:-3}};
    m.chunks.current.init(0,{cellMap:{}},{});
    for(let x=-2;x<=2;x++)for(let z=-2;z<=2;z++){m.chunks.current.addCell(-3+x,-3+z,m.lookaheadNode);if(Math.abs(x)<2&&Math.abs(z)<2)m.chunks.current.registerInnerCell(-3+x,-3+z);}
    m.stageCurrentChunk();const steps=[];
    for(let i=1;i<=36;i++){const x=i<18?-3+i:-3+36-i,z=i<12?-3:i<24?i-15:33-i;steps.push({x:x*10+.25,z:z*10+.75,candidate:i+1});m.lookaheadNode={i,p:{x:x*10+.25,z:z*10+.75},next:{i:i+1}};m.step();}
    const chunks=[...m.chunks.stage,m.chunks.prev,m.chunks.current].filter(c=>c&&c.cellCount);
    return {steps,chunks:chunks.map(c=>({last:c.lastSeenIndex,cells:Object.values(c.cellMap).flatMap(row=>Object.values(row)).map(v=>({x:v.worldX/C,z:v.worldZ/C,inner:v.isInner,candidates:[v.closestNode.i,...v.closeNodes.map(n=>n.i)]})),parents:Object.entries(c.parentTileMap).flatMap(([x,row])=>Object.keys(row).map(z=>({x:+x,z:+z})))}))};
   }
   if(chunkInput){
    const nodes=chunkInput.nodes.map(a=>{const b=a.bits.map(decodeDouble);return {i:a.i,p:new q(b[0],b[1],b[2]),n:new q(b[3],b[4],b[5]),da:b[6],g:b[7],gfa:b[8],h:b[9],blend:b[10],bank:b[11],bridge:a.bridge,shadowLeft:b[12],shadowRight:b[13]};});
    let previous=null;
    for(let i=0;i<nodes.length;i++){
     const n=nodes[i];n.prev=nodes[i-1];n.next=nodes[i+1];
     if(previous){previous.nextFine=n;n.prevFine=previous;}previous=n;
     for(const a of chunkInput.nodes[i].fine){const b=a.bits.map(decodeDouble),f={p:new q(b[0],b[1],b[2]),n:new q(b[3],b[4],b[5]),prev:n,next:nodes[i+1],shadowLeft:b[6],shadowRight:b[7]};previous.nextFine=f;f.prevFine=previous;previous=f;}
    }
    const chunk=new s0();chunk.prevChunk={cellMap:{}};chunk.innerCount=0;chunk.lastSeenIndex=-Infinity;
    chunk.customCellInit=c=>{c.tTL=c.tTR=c.tBL=c.tBR=0;};chunk.customGenCellInit=()=>{};
    for(const c of chunkInput.cells){for(const i of c.candidates)chunk.addCell(c.x,c.z,nodes[i-nodes[0].i]);if(c.inner)chunk.registerInnerCell(c.x,c.z);}
    chunk.initGeneration();const inner=chunk.innerCount,fi=chunk.index1End;
    while(!chunk.loadPositions()){}
    chunk.genCellIndex=0;chunk.genCell=chunk.cellList[0];chunk.genCell.genRow=0;while(!chunk.loadNormals0()){}
    const live=chunk.cellList.length*k0*k0,coarseNormals=Array.from(chunk.normArr.slice(0,live*3));
    if(inner){chunk.genCellIndex=0;chunk.genCell=chunk.innerCellList[0];chunk.genCell.genRow=0;while(!chunk.loadNormals1()){}}
    return {position:Array.from(chunk.posArr.slice(0,live*3)),normal:Array.from(chunk.normArr.slice(0,live*3)),coarseNormals,
     proximity:Array.from(chunk.roadArr.slice(0,live)),uv:Array.from(chunk.roadUvArr.slice(0,live*2)),indices:Array.from(chunk.indexArr.slice(0,fi))};
   }
   const tile=new Dt();tile.init(${spec.tileX},${spec.tileZ},0);while(!tile.genHeights0()){}tile.genRow=0;tile.genNormals0();
   const hillsTile=new i7();
   if(${fullTile}){
    while(!tile.genNormals0()){}const coarseNormals=Array.from(tile.normArr);tile.genRow=0;
    while(!tile.genHeights1()){}tile.genRow=0;while(!tile.genNormals1()){}
    const lookups=[];tile.hasHeights0=true;
    for(const fine of [false,true]){tile.hasHeights1=fine;for(const [u,w] of [[0,0],[.37,.61],[.5,.5],[.99999,.99999]]){const x=tile.ox+H*u,z=tile.oz+H*w;lookups.push({x,z,fine,height:tile.lookupHeight(x,z)});}}
    return {...${JSON.stringify(spec)},heights:tile.heights,position:Array.from(tile.geo.attributes.position.array),normal:Array.from(tile.normArr),coarseNormals,lookups};
   }
   // The base classes do not require contents. s0 hook is executed on explicit
   // zero tree-density corners and absent wall marks, not advertised as full content parity.
   const chunk=new s0();let n=shared.head;for(let i=0;i<20;i++)n=n.next;
   const wx=Math.floor(n.p.x/C)*C,wz=Math.floor(n.p.z/C)*C;
   const cell={worldX:wx,worldZ:wz,offsetX:0,offsetZ:0,bufferIndexEntry:0,indexIndex0:0,indexIndex1:U.index0Size,genRow:0,closestNode:n,closeNodes:[],hTL:a0.getXZ(wx,wz),cTL:0,tTL:0,tTR:0,tBL:0,tBR:0};
   U.generateCellCorners(cell);chunk.worldX=wx;chunk.worldZ=wz;chunk.cellList=[cell];chunk.genCell=cell;
   while(!chunk.loadPositions()){}cell.genRow=0;chunk.genCellIndex=0;chunk.loadNormals0();
   if(n.dswl||n.dswr)throw Error('Unsupported wall-mark context');
   const arrays=c=>Object.fromEntries(Object.entries(c.geo.attributes).map(([k,v])=>[k,{type:v.array.constructor.name,length:v.array.length,first:Array.from(v.array.slice(0,Math.max(6,k0*v.itemSize)))}]));
   return {config:{H,Nt,F,Z,C,t0,k0,k1,m0,y1},tile:arrays(tile),hillsTile:arrays(hillsTile),chunk:arrays(chunk),tileIndex:Array.from(tile.geo.index.array.slice(0,24)),chunkIndex:Array.from(chunk.indexArr.slice(0,24)),height:tile.heights[0],quantizedHeight:tile.geo.attributes.position.array[1],rows:{tile:tile.genRow,chunk:cell.genRow}};
  })()`;
  const result=vm.runInContext(code,ctx,{timeout:20000});
  if(fullTile||spec.chunkInput||spec.layoutOnly)return {schema:1,hashes:runtime.sourceHashes,section:sha(spec.layoutOnly?pieces.chunk+collection+rotate:fullTile?pieces.tile:pieces.chunk+pieces.hillsChunk),input:spec.sourceChunk?spec.chunkInput:undefined,result};
  for(const object of [result.tile,result.chunk])for(const attribute of Object.values(object))assert(attribute.first.every(Number.isFinite),'Missing geometry context');
  assert(result.tile.normal.first.some(x=>x!==0));assert(result.chunk.normal.first.some(x=>x!==0));
  assert.equal(result.quantizedHeight,Math.fround(result.height));
  assert(pieces.tile.includes('o.isEven=this.genRow&!1'),'Original row expression changed');
  return {schema:1,kind:'C1 context smoke, not T2/T3 mesh acceptance',hashes:runtime.sourceHashes,sections:Object.fromEntries(Object.entries(pieces).map(([k,v])=>[k,sha(v)])),result,
   scope:{executed:'f0/Dt/i7/U/s0 constructors; Dt heights0 + first normals0 row; U complete cell positions + first normals0 rows; s0 customCellLoad/customCellElev no-wall branch',
    deferred:'i7 content initialization, shadow-density provider and staged parent scheduling; full mesh fixtures belong to T2/T3',
    dependencies:'a0 is the same natural original Pl; m2/$u and Z4/i_ original; Tt/Ll original; geometry containers only store Uint16/Float32/Int8; Vec3 original. i7 constructor-only: e7/Ka storage containers; sea plane empty storage with explicit synthetic i2=1/x4=2, no sea geometry or content stages asserted',
    rounding:'Dt keeps double heights[] for normals/lookup; position/normal/curvature/roadProximity/treeMask/roadUv Float32; overlap Int8; U normals read quantized roadProximity',
    hooks:'s0 tree corners explicitly synthetic zero, nf shadows original; dswl/dswr absent required, future nonzero input needs independent wall-elevation oracle; no content RNG invoked'}};
 }});
}
function terrainTileMain(){
 const cases=[];for(const seed of ['a','1337'])for(const viewDistance of [0,2,5])for(const tileX of [-1,0])cases.push(terrainContextBuild(true,{seed,viewDistance,tileX,tileZ:0}));
 const value={schema:1,cases},json=JSON.stringify(value)+'\n';
 const file=path.join(__dirname,'Fixtures/RoadBlend/terrain-tile.json.gz');
 if(process.argv.includes('--write'))fs.writeFileSync(file,zlib.gzipSync(json));else assert.equal(zlib.gunzipSync(fs.readFileSync(file)).toString(),json);
 console.log('terrain tile original full arrays PASS');
}
function terrainContextMain(){
 const value=terrainContextBuild(),json=JSON.stringify(value,null,2)+'\n';
 assert.equal(JSON.stringify(terrainContextBuild()),JSON.stringify(value),'Context is not repeatable');
 const file=path.join(__dirname,'../TerrainAudit/terrain-context-smoke.json');
 if(process.argv.includes('--write'))fs.writeFileSync(file,json);else assert.equal(fs.readFileSync(file,'utf8'),json,'Context reference drift');
 console.log('terrain context constructors/height/normal/index repeatability PASS');
}
function terrainTopologyMain(){
 const input=base.readArchive(['Normals']),hashes=base.verifyHashes(input.sources,['Dev','Hills','Settings','Rand','Normals']);
 const source=mid.classSection(input.sources.Settings.toString(),'class f0 extends D0{').source;
 const ctx=vm.createContext({});
 const type=vm.runInContext('(function(){class D0{};'+source+';return f0;})()',ctx);
 const cases=[10,16,20,24,48,84,150,200,250].map(resolution=>({resolution,...type.getIndexesForRes(resolution)}));
 const value={schema:1,kind:'f0.getIndexesForRes',hashes,section:sha(source),cases},json=JSON.stringify(value)+'\n';
 const file=path.join(__dirname,'Fixtures/RoadBlend/terrain-topology.json.gz');
 if(process.argv.includes('--write'))fs.writeFileSync(file,zlib.gzipSync(json));else assert.equal(zlib.gunzipSync(fs.readFileSync(file)).toString(),json);
 console.log('terrain topology original arrays PASS: '+cases.length+' resolutions');
}

// C3: original f0 storage and Dt overlap methods, without a terrain scheduler.
function terrainBoundaryMain(){
 const input=base.readArchive(['Normals']);const hashes=base.verifyHashes(input.sources,['Dev','Hills','Settings','Rand','Normals']);
 const settings=input.sources.Settings.toString(),hills=input.sources.Hills.toString();
 const grid=mid.classSection(settings,'class f0 extends D0{').source,tile=mid.classSection(settings,'class Dt{').source;
 const ctx=vm.createContext({});
 const result=vm.runInContext(`(function(){
  const s=(o,k,v)=>o[k]=v,G=0,Z=25,F=24,O0=23,C=10,H=240,r0={},x0={Dead:0,Staging:1};
  class D0{constructor(){this.attributes={};}setAttribute(k,v){this.attributes[k]=v;}setIndex(a){this.index=a;}setDrawRange(start,count){this.drawRange={start,count};}}
  class B{constructor(array,itemSize){this.array=array;this.itemSize=itemSize;}setUsage(){}}
  ${grid};${tile};f0.reset();const geometry=new f0(12,240);
  const t=Object.create(Dt.prototype);t.geo=geometry;t.status=0;t.worldCenter={set(){}};t.container={position:{set(){}}};t.customInit=()=>{};
  t.init(-1,-1,0);const cases=[];
  for(const index of [0,1,127,128,255,256,4096])for(let edge=-1;edge<8;edge++){
   t.overlaps.fill(0);t.status=1;t.pendingOverlaps=false;
   if(edge<0)t.registerOverlap(-22,-21,index);else t.registerEdgeOverlap(2,3,edge,index);
   const values=Array.from(t.overlaps);t.updateOverlap();
   cases.push({index,edge,entries:values.flatMap((value,address)=>value===0?[]:[{address,value}]),pendingAfterUpdate:t.pendingOverlaps});
  }
  return {type:t.overlaps.constructor.name,aliasesOriginalAttribute:t.overlaps===geometry.attributes.overlap.array,cases};
 })()`,ctx);
 assert.equal(result.type,'Int8Array');assert.equal(result.aliasesOriginalAttribute,true);
 const a=hills.indexOf('if(overlap > 0.0)'),b=hills.indexOf('gl_Position',a);
 assert(a>0&&b>a);const shader=hills.slice(a,b);
 const value={schema:1,hashes,sections:{grid:sha(grid),tile:sha(tile),overlapShader:sha(shader)},
  channels:{position:'f0/U Float32; local origin separate',normal:'Dt/U original coarse/fine',curvature:'Hills Pl constant zero',roadProximity:'U plus s0 L46178-L46180',roadUv:'s0 L46210-L46214, off-road only',overlap:'f0 native Int8; Dt four-corner/eight-direction writes; scheduler deferred to T4',treeMask:'i7 L44990; s0 L46158-L46209, including fine-node shadows even at zero density; absent in this geometry subset, zero adaptation omits shadows',fieldCol:'i7 L44993 and field manager; absent in this no-field subset'},
  source:{grid:'Settings L41004-L41194',tile:'Settings L42661-L42682; L42780-L42818',
   shader:{symbol:'Hills template containing overlap displacement',utf16Start:a,utf16End:b,lines:shader.split('\n').map((line,i)=>({line:'OVL'+String(i+1).padStart(4,'0'),text:line}))}},result};
 const file=path.join(__dirname,'Fixtures/RoadBlend/terrain-boundaries.json'),json=JSON.stringify(value,null,2)+'\n';
 if(process.argv.includes('--write'))fs.writeFileSync(file,json);else assert.equal(fs.readFileSync(file,'utf8'),json);
 console.log('C3 original overlap storage, eight edge addresses and shader identity PASS');
}

// T4.1: execute Ma's original constructor/init/update and U's original cell rules.
// Geometry jobs are completion-controlled storage endpoints; no Unity outputs enter this oracle.
function terrainStreamingMain(){
 const input=base.readArchive(['Normals']);const hashes=base.verifyHashes(input.sources,['Dev','Hills','Settings','Rand','Normals']);
 const s=input.sources.Settings.toString(),ma=mid.classSection(s,'class Ma{').source,u=mid.classSection(s,'class U{').source,at=mid.classSection(s,'class At{').source;
 const cases=[];
 for(const view of [0,2,5])for(const scenario of ['straight','diagonal','loop','loop-retained']){
 const result=vm.runInNewContext(`(function(){
 const trace=[],scenario="${scenario}",s=(o,k,v)=>o[k]=v,l={},g={},h1=10000,_=0,U2=[],u1=[],h0={add(){}};
 let H,Nt,F,O0,Z,i5,C,t0,k0,m0,a0,Q2,k1,l2,B4,H4,Mt,s5,W4,b4,c4,S3,It,I5,y1,Tt,b9,Ct,c9;
 class c0{add(){}remove(){}};const e0={addJob(){}},w1={reset(){}},v2={},Dt={},AtDummy={};
 const x0={Dead:0,Staging:1,Staged:2,Live:3,Retired:4,Repooling:5},J0={getLastSeenIndexForTile(){return scenario==="loop-retained"?2000:-10000;}};
 const I={vehicleIndex:0,vehicleNode:null};const z1={Pending:0};
 ${at};${u};
 const push=(kind,x,z,index)=>trace.push({kind,x,z,index});
 class Tile{constructor(){this.container={};this.status=0;}init(x,z,index){this.tileX=x;this.tileZ=z;this.oxCell=x*H/C;this.ozCell=z*H/C;this.lastSeenIndex=index;push('tile.init',x,z,index);}initGeneration(){this.status=2;push('tile.stage',this.tileX,this.tileZ,I.vehicleIndex);}initUpgrade(){push('tile.prepareFine',this.tileX,this.tileZ,I.vehicleIndex);}display(){this.status=3;push('tile.display',this.tileX,this.tileZ,I.vehicleIndex);}upgrade(){push('tile.fine',this.tileX,this.tileZ,I.vehicleIndex);}downgrade(){push('tile.coarse',this.tileX,this.tileZ,I.vehicleIndex);}retire(){this.status=4;push('tile.retire',this.tileX,this.tileZ,I.vehicleIndex);}repool(){this.status=0;push('tile.repool',this.tileX,this.tileZ,I.vehicleIndex);}registerOverlap(){}registerEdgeOverlap(){} }
 class Chunk{constructor(){this.cellList=[];this.innerCellList=[];this.cellPool=[];this.bounds={};this.mesh={};}init(...args){U.prototype.init.apply(this,args);this.creation=args[0];push('chunk.init',this.creation,0,I.vehicleIndex);}customInit(){}addCell(...a){U.prototype.addCell.apply(this,a);}hasCell(...a){return U.prototype.hasCell.apply(this,a);}registerInnerCell(...a){U.prototype.registerInnerCell.apply(this,a);}parentTilesHaveStaged(){return true;}initGeneration(){this.downgradeIndex=this.lastSeenIndex-s5;this.retireIndex=this.lastSeenIndex-W4;push('chunk.stage',this.creation,0,I.vehicleIndex);}display(){push('chunk.display',this.creation,0,I.vehicleIndex);}initUpgrade(){push('chunk.prepareFine',this.creation,0,I.vehicleIndex);}upgrade(){push('chunk.fine',this.creation,0,I.vehicleIndex);}downgrade(){push('chunk.coarse',this.creation,0,I.vehicleIndex);}retire(){push('chunk.retire',this.creation,0,I.vehicleIndex);}}
 ${ma};
 const nodes=[];for(let i=0;i<1900;i++){let x,z;if(scenario==="diagonal"){x=-3000+i*7;z=-100+i*7;}else if(scenario.startsWith("loop")){let k=i%640; x=-3000+(k<160?k:k<320?160:k<480?480-k:0)*10;z=-100+(k<160?0:k<320?k-160:k<480?160:640-k)*10;}else{x=-3000+Math.min(i,500)*10;z=-100+Math.max(0,i-500)*10;}nodes.push({i,p:{x,z}});}for(let i=0;i<nodes.length;i++){nodes[i].prev=nodes[i-1];nodes[i].next=nodes[i+1];}
 I.vehicleNode=nodes[0];const m=new Ma({tileSize:${[240,480,840,1500,2000,2500][view]},cellSize:20,cellRes:20,lod1Horizon:${[13,16,19,22,25,28][view]},heightmap:{},tileClass:Tile,chunkClass:Chunk,cellClass:At});
 let guard=0;while(!m.init()){if(++guard>1000)throw Error('init guard');}
 for(let observer=0;observer<=1000;observer++){I.vehicleIndex=observer;for(let call=0;call<8;call++)m.update();}
 return {trace,config:{Q2,k1,l2,B4,H4,Mt,s5,W4,b4,c9}};
 })()`,{},{timeout:15000});cases.push({view,scenario,...result});
 }
 const value={schema:1,hashes,sections:{Ma:sha(ma),U:sha(u),At:sha(at)},scope:'Original Ma constructor/init/update and U cell registration, immediate-ready geometry endpoints; no content RNG or geometry job timings asserted',cases};
 const f=path.join(__dirname,'Fixtures/RoadBlend/terrain-streaming.json'),json=JSON.stringify(value)+'\n';
 if(process.argv.includes('--write'))fs.writeFileSync(f,json);else assert.equal(fs.readFileSync(f,'utf8'),json);
 console.log('Original Ma/U scheduling trace PASS: '+cases.map(c=>c.trace.length).join('/'));
}

function terrainMaterialMain(){
 const input=base.readArchive(['Normals']);const hashes=base.verifyHashes(input.sources,['Dev','Hills','Settings','Rand','Normals']);
 const h=input.sources.Hills.toString(),n=input.sources.Normals.toString();
 const shaders={};for(const name of ['bo','Mo','Lo','yo','Co']){const start=h.indexOf(name+'=`')+name.length+2,end=h.indexOf('`',start);assert(start>name.length+2&&end>start);const text=h.slice(start,end);shaders[name]={text,sha256:sha(text),utf16Offset:start,lines:text.split('\n').map((text,i)=>({line:'TM-'+name+'-'+String(i+1).padStart(4,'0'),text}))};}
 const aliases={};for(const m of h.matchAll(/([A-Za-z_$][\w$]*)=""\+new URL\("\.\.\/assets\/([^"]+)"/g))aliases[m[1]]=m[2];
 const colors=Array.from(h.matchAll(/ground:(\{grassColA:[^}]+\})/g),m=>vm.runInNewContext('('+m[1]+')'));
 const maps=[];for(const symbol of ['Di','Mi','ki','uo']){
  const at=h.indexOf(symbol+'={')+symbol.length+1;let depth=0,end=at;for(;end<h.length;end++){if(h[end]==='{')depth++;if(h[end]==='}'&&--depth===0)break;}
  const body=h.slice(at,end+1),out={};for(const key of ['grass','forest','heather','gravel','sand','rock','rockBump','detailNear','detailFar','grassVariation','offroadMap']){const m=body.match(new RegExp('\\b'+key+':([A-Za-z_$][\\w$]*)'));assert(m&&aliases[m[1]],symbol+':'+key);out[key]=aliases[m[1]];}maps.push(out);
 }
 const fade=h.match(/\bts=""\+new URL\("\.\.\/assets\/([^"]+)"/);assert(fade);
 const colorStart=n.indexOf('XE=(a,e=!1)=>'),colorEnd=n.indexOf(',$w=',colorStart),colorSource=n.slice(colorStart,colorEnd);
 const convert=vm.runInNewContext('(function(){const '+colorSource+';return XE;})()');
 const roadColors=Array.from(h.matchAll(/\w+=\{road:(\d+),clear:(\d+)\}/g),m=>Number(m[1]));assert.equal(roadColors.length,4);
 const seasons=colors.map((c,i)=>({name:['summer','spring','autumn','winter'][i],maps:maps[i],roadColor:Array.from(convert(roadColors[i])),colors:Object.fromEntries(Object.entries(c).map(([k,v])=>[k,k==='fieldDiscolouration'?[(v>>16&255)/255,(v>>8&255)/255,(v&255)/255]:Array.from(convert(v))]))}));
 const binding=base.section(h,'function ls(t,a){','let r;Oe?r=He?');
 const value={schema:1,hashes,shaders,seasons,fadeFine:fade[1],sourceOrigin:[4020,8240],colorConversion:{source:colorSource,sha256:sha(colorSource)},binding:{source:binding,sha256:sha(binding)},scope:'Common ground palettes, original surface/vertex functions and exact XE/Wu gamma2.2; scene lighting/fog is a declared URP adapter; no tree/field providers'};
 // Independent scalar fixture for the original Mo/yo/Co flat winter branch:
 // constant maps, no content, no roadside/cliff. Vertex/fragment source identity pinned above.
 const depth=100,grassBlend=Math.pow(((1-(1-.5)*(1-.5))+.5)/2,2);
 const blendFar=.5*.6*2,blendMedium=(1-(1-.5)*(1-.5))*.7;
 const detail=(blendFar*(depth/800)+blendMedium*(1-depth/800))*(.4+.6*(depth/300))+.5*.7;
 value.flatWinter={cameraDepth:depth,grass:[.8,.6,.4,1],a:[.2,.4,.6],b:[.8,.6,.4],rgba:[.8,.6,.4].map((v,i)=>v*([.2,.4,.6][i]+([.8,.6,.4][i]-[.2,.4,.6][i])*grassBlend)*(1-detail)*.75).concat(1)};
 const excerpts=['# Raw archive excerpts. L labels below are THIS material excerpt sequence, not historical prettier lines.'];let line=1;
 function excerpt(name,text,offset){const first=line;excerpts.push('# symbol='+name+' rawUtf16Offset='+offset+' sha256='+sha(text));for(const row of text.split('\n'))excerpts.push('L'+String(line++).padStart(5,'0')+' '+row);return {first,last:line-1};}
 for(const [name,entry] of Object.entries(shaders))entry.excerpt=excerpt(name,entry.text,entry.utf16Offset);
 value.binding.excerpt=excerpt('ls',binding,h.indexOf(binding));value.colorConversion.excerpt=excerpt('XE/Wu',colorSource,n.indexOf(colorSource));
 const excerptPath=path.join(__dirname,'../SceneArchitecture/SourceAtlas/TerrainMaterial.source.txt'),excerptText=excerpts.join('\n')+'\n';
 if(process.argv.includes('--write'))fs.writeFileSync(excerptPath,excerptText);else assert.equal(fs.readFileSync(excerptPath,'utf8'),excerptText);
 const f=path.join(__dirname,'Fixtures/RoadBlend/terrain-material.json'),json=JSON.stringify(value,null,2)+'\n';
 if(process.argv.includes('--write'))fs.writeFileSync(f,json);else assert.equal(fs.readFileSync(f,'utf8'),json);
 console.log('Hills terrain shared material/palettes/source identity PASS');
}

// Execute the ORIGINAL GLSL, with constant linear textures and a minimal matrix
// wrapper. Only GLSL-300 syntax is adapted; no Unity shader or expected values are
// read. A separate float framebuffer supplies the surface oracle, without lighting.
function terrainMaterialGpuMain(){
 terrainMaterialMain();
 const reference=JSON.parse(fs.readFileSync(path.join(__dirname,'Fixtures/RoadBlend/terrain-material.json'),'utf8'));
 const textures={grassMap:[.8,.6,.4,1],sandMap:[.7,.6,.4,.6],rockMap:[.3,.35,.4,.6],rockMapBump:[.5,.5,.5,.5],gravelMap:[.5,.4,.3,.7],forestMap:[.1,.2,.1,.4],heatherMap:[.45,.2,.3,.5],detailNearMap:[.5,.5,.5,.5],detailFarMap:[.5,.5,.5,.5],grassVariationMap:[.5,.5,.5,.5],offroadMap:[.1,.35,.6,1],fadeFine:[.5,.5,.5,.5],displacementMap:[.5,.5,.5,.5]};
 const states=[
  {name:'flat',height:0,normal:[0,1,0],proximity:0,depth:100},
  {name:'shoulder',height:35,normal:[.6,.8,0],proximity:.2,depth:100},
  {name:'slope',height:150,normal:[.8,.6,0],proximity:2,depth:100},
  {name:'crest',height:450,normal:[0,1,0],proximity:0,depth:600},
  {name:'cliff',height:250,normal:[.95,Math.sqrt(1-.95*.95),0],proximity:1.2,depth:100},
  {name:'under-bridge',height:10,normal:[.6,.8,0],proximity:-2,depth:100},
  {name:'greenlane',height:30,normal:[0,1,0],proximity:.8,depth:100,offroad:1,greenlane:1},
  {name:'overlap-sink',height:0,normal:[0,1,0],proximity:0,depth:100,overlap:100}
 ];
 const cases=reference.seasons.flatMap((season,i)=>states.map(s=>({...s,season:i,colors:season.colors,roadColor:season.roadColor})));
 const payload={shaders:Object.fromEntries(Object.entries(reference.shaders).map(([k,v])=>[k,v.text])),textures,cases};
 const html=`<!doctype html><meta charset="utf-8"><body><script>
try {
const data=${JSON.stringify(payload)};
const canvas=document.createElement('canvas');canvas.width=canvas.height=16;
const gl=canvas.getContext('webgl2',{antialias:false,alpha:true});if(!gl||!gl.getExtension('EXT_color_buffer_float'))throw Error('WebGL2 float framebuffer unavailable');
const syntax=s=>s.replace(/\\btexture2D\\b/g,'texture');
const fv='varying vec3 vFresnelPos; varying vec3 vFresnelNorm;';
const vs='#version 300 es\\nprecision highp float; precision highp int;\\nattribute vec3 position; attribute vec3 normal; uniform mat4 modelMatrix; uniform mat4 modelViewMatrix; uniform mat4 projectionMatrix;'+fv+data.shaders.bo+'void main(){vec3 objectNormal=normal;vec3 transformed=position;'+data.shaders.Mo+'}';
const fs='#version 300 es\\nprecision highp float; precision highp int;\\n#define saturate(a) clamp(a,0.0,1.0)\\n'+fv+'uniform vec3 cameraPosition; uniform vec2 originOffset; uniform float fresnelIntensity; out vec4 resultColor;'+data.shaders.Lo+'void main(){vec4 diffuseColor=vec4(1.0); vec3 totalEmissiveRadiance=vec3(0.0);'+data.shaders.yo+data.shaders.Co+'resultColor=vec4(diffuseColor.rgb,1.0);}';
function compile(type,s){const shader=gl.createShader(type);gl.shaderSource(shader,syntax(s));gl.compileShader(shader);if(!gl.getShaderParameter(shader,gl.COMPILE_STATUS))throw Error(gl.getShaderInfoLog(shader));return shader;}
const program=gl.createProgram();gl.attachShader(program,compile(gl.VERTEX_SHADER,vs.replace(/\\battribute\\b/g,'in').replace(/\\bvarying\\b/g,'out')));gl.attachShader(program,compile(gl.FRAGMENT_SHADER,fs.replace(/\\bvarying\\b/g,'in')));gl.linkProgram(program);if(!gl.getProgramParameter(program,gl.LINK_STATUS))throw Error(gl.getProgramInfoLog(program));gl.useProgram(program);
const target=gl.createTexture();gl.bindTexture(gl.TEXTURE_2D,target);gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA32F,16,16,0,gl.RGBA,gl.FLOAT,null);const fb=gl.createFramebuffer();gl.bindFramebuffer(gl.FRAMEBUFFER,fb);gl.framebufferTexture2D(gl.FRAMEBUFFER,gl.COLOR_ATTACHMENT0,gl.TEXTURE_2D,target,0);if(gl.checkFramebufferStatus(gl.FRAMEBUFFER)!==gl.FRAMEBUFFER_COMPLETE)throw Error('incomplete framebuffer');
let unit=0;for(const [name,rgba] of Object.entries(data.textures)){const texture=gl.createTexture();gl.activeTexture(gl.TEXTURE0+unit);gl.bindTexture(gl.TEXTURE_2D,texture);gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA32F,1,1,0,gl.RGBA,gl.FLOAT,new Float32Array(rgba));gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MIN_FILTER,gl.NEAREST);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MAG_FILTER,gl.NEAREST);gl.uniform1i(gl.getUniformLocation(program,name),unit++);}
function attr(name,size,values){const loc=gl.getAttribLocation(program,name);if(loc<0)return;const b=gl.createBuffer();gl.bindBuffer(gl.ARRAY_BUFFER,b);gl.bufferData(gl.ARRAY_BUFFER,new Float32Array(values),gl.STATIC_DRAW);gl.enableVertexAttribArray(loc);gl.vertexAttribPointer(loc,size,gl.FLOAT,false,0,0);}
function one(name,value){gl.uniform1f(gl.getUniformLocation(program,name),value);}
function vec(name,v){gl.uniform3fv(gl.getUniformLocation(program,name),v);}
const identity=[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1];gl.uniformMatrix4fv(gl.getUniformLocation(program,'modelMatrix'),false,identity);
const rows=[];for(const c of data.cases){
 attr('position',3,[-1,c.height,-1,-1,c.height,1,1,c.height,1,-1,c.height,-1,1,c.height,1,1,c.height,-1]);attr('normal',3,Array(6).fill(c.normal).flat());attr('roadProximity',1,Array(6).fill(c.proximity));attr('overlap',1,Array(6).fill(c.overlap||0));attr('treeMask',1,Array(6).fill(0));attr('fieldCol',1,Array(6).fill(0));attr('roadUv',2,Array(6).fill([.5,.5]).flat());
 const cameraY=c.height+c.depth;gl.uniformMatrix4fv(gl.getUniformLocation(program,'modelViewMatrix'),false,[1,0,0,0,0,0,1,0,0,1,0,0,0,0,-cameraY,1]);gl.uniformMatrix4fv(gl.getUniformLocation(program,'projectionMatrix'),false,[1,0,0,0,0,1,0,0,0,0,-2/1999.9,0,0,0,-2000.1/1999.9,1]);vec('cameraPosition',[0,cameraY,0]);gl.uniform1i(gl.getUniformLocation(program,'seasonIndex'),c.season);one('offroad',c.offroad||0);one('greenlane',c.greenlane||0);one('sinkDist',100);one('vehicleIndex',80);one('shadowFactor',.9);one('radiance',0);one('fresnelIntensity',0);gl.uniform2f(gl.getUniformLocation(program,'originOffset'),4020,8240);vec('roadCol',c.roadColor);for(const [name,v] of Object.entries(c.colors))vec(name,v);
 gl.viewport(0,0,16,16);gl.clearColor(0,0,0,0);gl.clear(gl.COLOR_BUFFER_BIT);gl.drawArrays(gl.TRIANGLES,0,6);const rgba=new Float32Array(4);gl.readPixels(8,8,1,1,gl.RGBA,gl.FLOAT,rgba);if(gl.getError())throw Error('WebGL error');rows.push({...c,rgba:Array.from(rgba)});
}
const debug=gl.getExtension('WEBGL_debug_renderer_info');document.body.textContent=btoa(JSON.stringify({success:true,renderer:debug?gl.getParameter(debug.UNMASKED_RENDERER_WEBGL):gl.getParameter(gl.RENDERER),cases:rows}));
}catch(error){document.body.textContent=btoa(JSON.stringify({success:false,error:String(error)}));}
</script>`;
 const temp=path.resolve(__dirname,'../../../Temp/TerrainMaterialWebGL');fs.mkdirSync(temp,{recursive:true});const page=path.join(temp,'oracle.html');fs.writeFileSync(page,html);
 const edge='C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe';assert(fs.existsSync(edge),'Local Edge needed for original GLSL oracle');
 const run=require('child_process').spawnSync(edge,['--headless=new','--edge-skip-compat-layer-relaunch','--no-first-run','--no-default-browser-check','--user-data-dir='+path.join(temp,'profile-'+process.pid),'--dump-dom','file:///'+page.replace(/\\/g,'/')],{encoding:'utf8',timeout:60000,maxBuffer:8*1024*1024,windowsHide:true});
 assert.equal(run.status,0,run.error?.message||run.stderr);const body=run.stdout.match(/<body[^>]*>([A-Za-z0-9+/=]+)<\/body>/);assert(body,'WebGL result missing: '+run.stdout.slice(-1000));const result=JSON.parse(Buffer.from(body[1],'base64').toString());assert(result.success,result.error);
 const value={schema:1,sourceHashes:reference.hashes,shaderHashes:Object.fromEntries(Object.entries(reference.shaders).map(([k,v])=>[k,v.sha256])),renderer:result.renderer,scope:'Original GLSL vertex/surface/fresnel, syntax-only WebGL2 wrapper and float framebuffer; constant linear maps, no content, fresnelIntensity=0; not a native full-scene screenshot',textures,cases:result.cases};
 const file=path.join(__dirname,'Fixtures/RoadBlend/terrain-material-gpu.json');
 if(process.argv.includes('--write'))fs.writeFileSync(file,JSON.stringify(value,null,2)+'\n');else {const expected=JSON.parse(fs.readFileSync(file,'utf8'));assert.deepEqual(value.shaderHashes,expected.shaderHashes);for(let i=0;i<value.cases.length;i++)for(let k=0;k<4;k++)assert(Math.abs(value.cases[i].rgba[k]-expected.cases[i].rgba[k])<1e-5,'Original GLSL result drift '+i+'/'+k);}
 console.log('Original GLSL float framebuffer PASS: '+value.cases.length+' cases; '+value.renderer);
}

// T5 fixed cameras: regenerate Dt/U geometry from frozen seed/layers/road nodes.
// Unity mesh arrays never enter expected geometry. Content and whole-scene lighting
// are deliberately excluded; real reference maps, source fresnel and overlap remain.
async function terrainVisualMain(){
 terrainMaterialMain();
 const at=process.argv.indexOf('--terrain-visual'),root=path.resolve(__dirname,'../../..');
 const directory=path.resolve(process.argv[at+1]),input=JSON.parse(fs.readFileSync(path.join(directory,'input.json'),'utf8'));
 assert.equal(input.schema,1);assert(input.parts.length<=100&&input.cases.length<=32);
 const reference=JSON.parse(fs.readFileSync(path.join(__dirname,'Fixtures/RoadBlend/terrain-material.json'),'utf8'));
 const archive=base.readArchive(['Normals']);base.verifyHashes(archive.sources,['Dev','Hills','Settings','Rand','Normals']);
 const gridSource=mid.classSection(archive.sources.Settings.toString(),'class f0 extends D0{').source;
 const grid=vm.runInNewContext('(function(){class D0{};'+gridSource+';return f0;})()');
 const parts=[];
 for(const part of input.parts){
  let r,origin,indices,normal;
  if(part.chunk){
   r=terrainContextBuild(false,{seed:input.seed,viewDistance:input.viewDistance,chunkInput:part.input}).result;
   origin=[Math.min(...part.input.cells.map(c=>c.x))*10,0,Math.min(...part.input.cells.map(c=>c.z))*10];
   const coarse=grid.getIndexesForRes(part.input.resolution).index0.length;
   const start=part.lod===1?part.input.cells.filter(c=>c.inner).length*coarse:0;
   const end=part.lod===1?r.indices.length:part.input.cells.length*coarse;
   indices=r.indices.slice(start,end);normal=part.lod===1?r.normal:r.coarseNormals;
  }else{
   r=terrainContextBuild(true,{seed:input.seed,viewDistance:input.viewDistance,tileX:part.x,tileZ:part.z,layers:input.layers}).result;
   const size=[240,480,840,1500,2000,2500][input.viewDistance];origin=[part.x*size,0,part.z*size];
   indices=Array.from(grid.getIndexesForRes(size/10)[part.lod===1?'index1':'index0']);normal=part.lod===1?r.normal:r.coarseNormals;
  }
  parts.push({position:r.position,normal,indices,origin,proximity:r.proximity,uv:r.uv,overlap:part.overlap});
 }
 fs.writeFileSync(path.join(directory,'native-geometry.json'),JSON.stringify(parts));
 // Comparison is deliberately after original geometry generation. Baked parts
 // may use regenerated native historical nodes, not a retained Unity road graph.
 const actualGeometry=JSON.parse(fs.readFileSync(path.join(directory,'actual-geometry.json'),'utf8'));
 assert.equal(actualGeometry.length,parts.length);let comparedVertices=0,comparedIndices=0;
 for(let i=0;i<parts.length;i++)for(const field of ['position','normal','indices']){
  const actual=actualGeometry[i][field],expected=parts[i][field];assert.equal(actual.length,expected.length,'region '+i+'/'+field+' length');
  // JSON canonicalizes signed zero; numerical Float32 values remain exact.
  for(let k=0;k<expected.length;k++)assert((field==='indices'?actual[k]:Math.fround(actual[k]))===(field==='indices'?expected[k]:Math.fround(expected[k])),'region '+i+'/'+field+'/'+k);
  if(field==='position')comparedVertices+=expected.length/3;if(field==='indices')comparedIndices+=expected.length;
 }
 const sourceFiles=new Map(),textureBytes=new Map();
 const maps=input.maps.map(season=>season.map(map=>{
  assert(map.path.startsWith('Assets/SlowRoads/Art/Reference/SlowRoads/'));
  const bytes=fs.readFileSync(path.join(root,map.path));sourceFiles.set(map.path,sha(bytes));
  textureBytes.set(map.path,bytes);return map;
 }));
 const payload={...input,cases:input.cases.map((c,i)=>({...c,actualIndex:i})),parts,maps,shaders:Object.fromEntries(Object.entries(reference.shaders).map(([k,v])=>[k,v.text])),seasons:reference.seasons};
 const html=`<!doctype html><meta charset="utf-8"><body>pending<script>
(async()=>{let stage='payload';try{
const d=await (await fetch('/payload'+location.search)).json();
const gl=document.createElement('canvas').getContext('webgl2',{antialias:false,alpha:true});if(!gl||!gl.getExtension('EXT_color_buffer_float'))throw Error('WebGL2 float target unavailable');
function compile(type,s){const sh=gl.createShader(type);gl.shaderSource(sh,s.replace(/\\btexture2D\\b/g,'texture'));gl.compileShader(sh);if(!gl.getShaderParameter(sh,gl.COMPILE_STATUS))throw Error(gl.getShaderInfoLog(sh));return sh;}
const fv='varying vec3 vFresnelPos; varying vec3 vFresnelNorm;';
const vs='#version 300 es\\nprecision highp float; precision highp int;\\nattribute vec3 position; attribute vec3 normal; uniform mat4 modelMatrix; uniform mat4 modelViewMatrix; uniform mat4 projectionMatrix;'+fv+d.shaders.bo+'void main(){vec3 objectNormal=normal;vec3 transformed=position;'+d.shaders.Mo+'}';
const fs='#version 300 es\\nprecision highp float; precision highp int;\\n#define saturate(a) clamp(a,0.0,1.0)\\n'+fv+'uniform vec3 cameraPosition; uniform vec2 originOffset; uniform float fresnelIntensity; out vec4 resultColor;'+d.shaders.Lo+'void main(){vec4 diffuseColor=vec4(1.0);vec3 totalEmissiveRadiance=vec3(0.0);'+d.shaders.yo+d.shaders.Co+'resultColor=vec4(diffuseColor.rgb,1.0);}';
const p=gl.createProgram();gl.attachShader(p,compile(gl.VERTEX_SHADER,vs.replace(/\\battribute\\b/g,'in').replace(/\\bvarying\\b/g,'out')));gl.attachShader(p,compile(gl.FRAGMENT_SHADER,fs.replace(/\\bvarying\\b/g,'in')));gl.linkProgram(p);if(!gl.getProgramParameter(p,gl.LINK_STATUS))throw Error(gl.getProgramInfoLog(p));gl.useProgram(p);
function attr(name,size,values,count){const l=gl.getAttribLocation(p,name);if(l<0)return;if(!values){gl.disableVertexAttribArray(l);if(size===2)gl.vertexAttrib2f(l,0,0);else gl.vertexAttrib1f(l,0);return;}const b=gl.createBuffer();gl.bindBuffer(gl.ARRAY_BUFFER,b);gl.bufferData(gl.ARRAY_BUFFER,new Float32Array(values),gl.STATIC_DRAW);gl.enableVertexAttribArray(l);gl.vertexAttribPointer(l,size,gl.FLOAT,false,0,0);}
const geometries=d.parts.map(part=>{const vao=gl.createVertexArray();gl.bindVertexArray(vao);attr('position',3,part.position);attr('normal',3,part.normal);attr('roadProximity',1,part.proximity);attr('roadUv',2,part.uv);attr('overlap',1,part.overlap);attr('treeMask',1,null);attr('fieldCol',1,null);const b=gl.createBuffer();gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER,b);gl.bufferData(gl.ELEMENT_ARRAY_BUFFER,new Uint16Array(part.indices),gl.STATIC_DRAW);return {vao,count:part.indices.length,origin:part.origin};});
const allTextures=[],cache=new Map();for(const season of d.maps){const textures=[];for(const m of season){let t=cache.get(m.path);if(!t){stage='texture '+m.path;const bitmap=await createImageBitmap(await (await fetch('/texture?path='+encodeURIComponent(m.path))).blob(),{colorSpaceConversion:'none',premultiplyAlpha:'none',imageOrientation:'flipY'});t=gl.createTexture();gl.bindTexture(gl.TEXTURE_2D,t);gl.texImage2D(gl.TEXTURE_2D,0,m.srgb?gl.SRGB8_ALPHA8:gl.RGBA8,gl.RGBA,gl.UNSIGNED_BYTE,bitmap);gl.generateMipmap(gl.TEXTURE_2D);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MIN_FILTER,m.filter==='Bilinear'?gl.LINEAR_MIPMAP_NEAREST:gl.LINEAR_MIPMAP_LINEAR);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MAG_FILTER,gl.LINEAR);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_S,gl.REPEAT);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_T,gl.REPEAT);cache.set(m.path,t);}textures.push({name:m.name,t});}allTextures.push(textures);}
function scalar(n,v){gl.uniform1f(gl.getUniformLocation(p,n),v);}function vec(n,v){gl.uniform3fv(gl.getUniformLocation(p,n),v);}function matrix(n,v){gl.uniformMatrix4fv(gl.getUniformLocation(p,n),false,v);}
function multiply(a,b){const r=new Float32Array(16);for(let c=0;c<4;c++)for(let row=0;row<4;row++)for(let k=0;k<4;k++)r[c*4+row]+=a[k*4+row]*b[c*4+k];return r;}
function png(values,w,h){const canvas=document.createElement('canvas');canvas.width=w;canvas.height=h;const ctx=canvas.getContext('2d'),im=ctx.createImageData(w,h);for(let y=0;y<h;y++)for(let x=0;x<w;x++){const a=(y*w+x)*4,b=((h-1-y)*w+x)*4;for(let k=0;k<3;k++){const v=Math.max(0,values[b+k]);im.data[a+k]=Math.round(255*Math.min(1,v<=.0031308?v*12.92:1.055*Math.pow(v,1/2.4)-.055));}im.data[a+3]=255;}ctx.putImageData(im,0,0);return canvas.toDataURL('image/png').split(',')[1];}
const rows=[];for(let ci=0;ci<d.cases.length;ci++){
const c=d.cases[ci],w=c.width,h=c.height;stage=c.name;gl.canvas.width=w;gl.canvas.height=h;
const target=gl.createTexture();gl.bindTexture(gl.TEXTURE_2D,target);gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA32F,w,h,0,gl.RGBA,gl.FLOAT,null);const fb=gl.createFramebuffer();gl.bindFramebuffer(gl.FRAMEBUFFER,fb);gl.framebufferTexture2D(gl.FRAMEBUFFER,gl.COLOR_ATTACHMENT0,gl.TEXTURE_2D,target,0);const depth=gl.createRenderbuffer();gl.bindRenderbuffer(gl.RENDERBUFFER,depth);gl.renderbufferStorage(gl.RENDERBUFFER,gl.DEPTH_COMPONENT24,w,h);gl.framebufferRenderbuffer(gl.FRAMEBUFFER,gl.DEPTH_ATTACHMENT,gl.RENDERBUFFER,depth);if(gl.checkFramebufferStatus(gl.FRAMEBUFFER)!==gl.FRAMEBUFFER_COMPLETE)throw Error('Framebuffer incomplete');
allTextures[c.season].forEach((o,i)=>{gl.activeTexture(gl.TEXTURE0+i);gl.bindTexture(gl.TEXTURE_2D,o.t);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MIN_FILTER,c.sourcePixels?gl.LINEAR:gl.LINEAR_MIPMAP_NEAREST);gl.uniform1i(gl.getUniformLocation(p,o.name),i);});
const season=d.seasons[c.season];gl.uniform1i(gl.getUniformLocation(p,'seasonIndex'),c.season);vec('roadCol',season.roadColor);for(const [n,v]of Object.entries(season.colors))vec(n,v);vec('cameraPosition',c.camera);gl.uniform2f(gl.getUniformLocation(p,'originOffset'),4020-d.origin[0],8240-d.origin[2]);scalar('offroad',d.lane<=1?1:0);scalar('greenlane',d.lane===1?1:0);scalar('vehicleIndex',d.progress);scalar('sinkDist',d.sinkDist);scalar('shadowFactor',.9);scalar('radiance',0);scalar('fresnelIntensity',.6);matrix('projectionMatrix',c.projection);
// The frozen camera is Unity's reflected (left-handed world to GL view) matrix.
// Its front-face convention is CW. This is a coordinate adapter, no index edits.
gl.enable(gl.DEPTH_TEST);gl.depthFunc(gl.LEQUAL);gl.enable(gl.CULL_FACE);gl.frontFace(gl.CW);gl.cullFace(gl.BACK);gl.viewport(0,0,w,h);gl.clearColor(0,0,0,0);gl.clearDepth(1);gl.clear(gl.COLOR_BUFFER_BIT|gl.DEPTH_BUFFER_BIT);
for(const g of geometries){const o=d.renderOriginDouble||d.origin;const model=[1,0,0,0,0,1,0,0,0,0,1,0,Math.fround(g.origin[0]-o[0]),Math.fround(-o[1]),Math.fround(g.origin[2]-o[2]),1];matrix('modelMatrix',model);matrix('modelViewMatrix',multiply(c.view,model));gl.bindVertexArray(g.vao);gl.drawElements(gl.TRIANGLES,g.count,gl.UNSIGNED_SHORT,0);}
const rgba=new Float32Array(w*h*4);gl.readPixels(0,0,w,h,gl.RGBA,gl.FLOAT,rgba);if(gl.getError())throw Error('WebGL error');const actual=new Float32Array(await (await fetch('/actual?index='+c.actualIndex,{cache:'no-store'})).arrayBuffer());
let union=0,disagree=0,common=0,total=0,sum=0,squares=0,max=0,nonfinite=0;const errors=[];for(let i=0;i<w*h;i++){const a=rgba[i*4+3]>.5,b=actual[i*4+3]>.5;if(a||b)union++;if(a!==b)disagree++;if(a&&b){common++;for(let k=0;k<3;k++){const error=Math.abs(rgba[i*4+k]-actual[i*4+k]);if(!Number.isFinite(error)){nonfinite++;continue;}sum+=error;squares+=error*error;max=Math.max(max,error);errors.push(error);total++;}}}errors.sort((a,b)=>a-b);
rows.push({name:c.name,sourcePixels:!!c.sourcePixels,width:w,height:h,common,union,disagree,silhouetteError:disagree/Math.max(1,union),mae:sum/total,rmse:Math.sqrt(squares/total),p99:errors[Math.floor(errors.length*.99)],max,nonfinite,native:png(rgba,w,h),unity:png(actual,w,h)});gl.deleteFramebuffer(fb);gl.deleteTexture(target);gl.deleteRenderbuffer(depth);
}
const ext=gl.getExtension('WEBGL_debug_renderer_info');await fetch('/result',{method:'POST',body:JSON.stringify({success:true,renderer:ext?gl.getParameter(ext.UNMASKED_RENDERER_WEBGL):gl.getParameter(gl.RENDERER),rows})});document.body.textContent='done';
}catch(e){await fetch('/result',{method:'POST',body:JSON.stringify({success:false,error:stage+': '+String(e),stack:e.stack})});}})();</script>`;
 const page=path.join(directory,'visual.html');fs.writeFileSync(page,html);
 // Loopback transport avoids dump-dom racing asynchronous texture decoding, and
 // avoids embedding repeated 100MB+ image/float payloads in a document.
 let resolveResult,rejectResult,timer;
 // Bound per-browser decoded-image/readback residency to eight images. This also
 // avoids Edge's intermittent large-response fetch failures in long GPU batches.
 const batches=Array.from({length:Math.ceil(input.cases.length/8)},(_,batch)=>zlib.gzipSync(JSON.stringify({...payload,cases:payload.cases.slice(batch*8,batch*8+8)})));
 const server=require('node:http').createServer((req,res)=>{const url=new URL(req.url,'http://127.0.0.1');
  if(url.pathname==='/result'&&req.method==='POST'){const chunks=[];let size=0;req.on('data',b=>{size+=b.length;if(size>32*1024*1024){req.destroy();rejectResult(Error('Result too large'));}else chunks.push(b);});req.on('end',()=>{res.end('ok');try{resolveResult(JSON.parse(Buffer.concat(chunks).toString()));}catch(e){rejectResult(e);}});}
  else if(url.pathname==='/payload'){const batch=Number(url.searchParams.get('batch'));if(!Number.isInteger(batch)||!batches[batch]){res.writeHead(404);res.end();}else{res.writeHead(200,{'Content-Type':'application/json','Content-Encoding':'gzip'});res.end(batches[batch]);}}
  else if(url.pathname==='/texture'&&textureBytes.has(url.searchParams.get('path'))){res.writeHead(200,{'Content-Type':'image/png'});res.end(textureBytes.get(url.searchParams.get('path')));}
  else if(url.pathname==='/actual'){const i=Number(url.searchParams.get('index'));if(!Number.isInteger(i)||i<0||i>=input.cases.length){res.writeHead(404);res.end();}else{const bytes=fs.readFileSync(path.join(directory,input.cases[i].name+'.unity.rgba'));res.writeHead(200,{'Content-Type':'application/octet-stream','Content-Length':bytes.length,'Cache-Control':'no-store'});res.end(bytes);}}
  else if(url.pathname==='/'){res.writeHead(200,{'Content-Type':'text/html'});res.end(html);}else{res.writeHead(404);res.end();}
 });
 await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
 const result={rows:[],renderer:null};
 try{for(let batch=0;batch<batches.length;batch++){
  const complete=new Promise((resolve,reject)=>{resolveResult=resolve;rejectResult=reject;});
  const browserLog=fs.openSync(path.join(directory,'browser-'+batch+'.log'),'w');
  const child=require('child_process').spawn('C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',[
   '--headless=new','--edge-skip-compat-layer-relaunch','--no-first-run','--no-default-browser-check','--user-data-dir='+path.join(directory,'profile-'+process.pid+'-'+batch),
   'http://127.0.0.1:'+server.address().port+'/?batch='+batch],{stdio:['ignore','ignore',browserLog],windowsHide:true});
  child.on('error',rejectResult);timer=setTimeout(()=>rejectResult(Error('Original GLSL image render timed out')),90000);
  try{const value=await complete;assert(value.success,value.error+'\n'+value.stack);result.rows.push(...value.rows);result.renderer=value.renderer;}
  finally{clearTimeout(timer);child.kill();server.closeAllConnections();fs.closeSync(browserLog);}
 }}finally{server.close();}
 for(const row of result.rows)for(const kind of ['native','unity']){fs.writeFileSync(path.join(directory,row.name+'.'+kind+'.png'),Buffer.from(row[kind],'base64'));delete row[kind];}
 const report={schema:1,sourceHashes:reference.hashes,shaderHashes:Object.fromEntries(Object.entries(reference.shaders).map(([k,v])=>[k,v.sha256])),
  geometrySHA256:sha(JSON.stringify(parts)),inputSHA256:sha(fs.readFileSync(path.join(directory,'input.json'))),textureHashes:Object.fromEntries(sourceFiles),renderer:result.renderer,
  scope:'Native archive Dt/U geometry + bo/Mo/Lo/yo/Co, frozen P5 source nodes/coverage/LOD, actual reference PNGs, same fixed matrices. Geometry-only surface/fresnel comparison. Unity DXT import, sRGB/linear and sampling are adapters. No whole-scene lighting/fog/content parity claim.',
  seed:input.seed,viewDistance:input.viewDistance,detail:input.detail,distance:input.distance,progress:input.progress,origin:input.origin,regions:parts.length,
  geometry:{float32ValuesEqual:true,signedZeroCanonicalizedByJson:true,comparedVertices,comparedIndices,nativeRoadRebuilds:input.parts.filter(p=>p.input?.nativeNodes).length},
  // Image gate is separate from the exact geometry gate and .003 scalar GLSL
  // gate. Real PNG vs shipping DXT, mip generation and API raster/interpolation
  // have a measured nonzero error; maxima remain reported, not hidden/clamped.
  limits:{mae:.015,rmse:.03,p99:.12,silhouetteError:.0001,minCommonPixels:1000},cases:result.rows};
 for(const c of report.cases)c.pass=c.nonfinite===0&&c.common>=report.limits.minCommonPixels&&c.mae<=report.limits.mae&&c.rmse<=report.limits.rmse&&c.p99<=report.limits.p99&&c.silhouetteError<=report.limits.silhouetteError;
 report.pass=report.cases.every(c=>c.pass);
 fs.writeFileSync(path.join(directory,'report.json'),JSON.stringify(report,null,2)+'\n');
 assert(report.pass,'Fixed-view image gate failed: '+report.cases.filter(c=>!c.pass).map(c=>c.name).join(','));
 console.log(JSON.stringify({pass:report.pass,regions:parts.length,geometry:report.geometry,cases:report.cases.length,
  worst:{mae:Math.max(...report.cases.map(c=>c.mae)),rmse:Math.max(...report.cases.map(c=>c.rmse)),p99:Math.max(...report.cases.map(c=>c.p99)),silhouette:Math.max(...report.cases.map(c=>c.silhouetteError))}}));
}
