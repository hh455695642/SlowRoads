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
 return mid.createReference('style3',spec.seed,false,'archive-heightfield',{updates:4100,full:true,consumeRuntime:runtime=>{
  const {ctx,input,generator}=runtime;terrainInstall(runtime,spec.chunkInput?.lane??4);
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
  ctx.chunkInput=spec.chunkInput??null;
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
