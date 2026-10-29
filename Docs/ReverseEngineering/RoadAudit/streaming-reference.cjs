const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm'),assert=require('node:assert/strict');
const base=require('./heightfield-reference.cjs'),mid=require('./midline-reference.cjs'),road=require('./road-mesh-reference.cjs');
function spatialProbe(){
 const a=base.readArchive(['Normals']);base.verifyHashes(a.sources,['Settings','Normals']);const s=a.sources.Settings.toString(),n=a.sources.Normals.toString();
 const nodes=Array.from({length:80},(_,i)=>({i,p:{x:-1000+i*9,z:-480+Math.sin(i*.19)*70}}));nodes.forEach((p,i)=>{p.prev=nodes[i-1];p.next=nodes[i+1];});
 const ctx={nodes,n:(o,k,v)=>o[k]=v,console};vm.createContext(ctx);
 vm.runInContext('(function(){const I={head:nodes[0]},s=n,z0=240,Na=20,F0=1000,z={},e0={addJob(){}},h0={add(){}},it=()=>"probe";let v3=0,q1=120,F2=240;'+base.section(n,'function GC(a,e,t){','function WC(')+'const a2=GC,e2=HC;'+mid.classSection(s,'class Ca{').source+';globalThis.index=new Ca();index.initialise(140);})()',ctx);
 for(let i=0;i<78;i++)ctx.index.update();
 const queries=[];for(let i=1;i<75;i+=3){let x=nodes[i].p.x+2,z=nodes[i].p.z-3;const r=ctx.index.getClosestNode(x,z,50);queries.push({x,z,index:r?.n.i??-999999,d:r?.d??0,close:ctx.index.isCloseToRoad(Math.floor(x/240),Math.floor(z/240),x,z,12)});}
 return {nodes:nodes.map(n=>({index:n.i,x:n.p.x,z:n.p.z})),queries};
}
function build(ticksPerNode=16,options=null){
 const functional=!!options,seed=options?.seed??'a',lane=options?.lane??4,detail=options?.detail??2;
 let cfg={tileSize:840,roadNodes:30,midlineIndexMargin:180,midlineFineHorizon:400};
 if(functional){const a=base.readArchive();base.verifyHashes(a.sources);const h=a.sources.Hills.toString();const profiles=[...h.matchAll(/\{tileSize:[^{}]*?roadNodes:[^{}]*?treeInstanceSize:[^{}]*?\}/g)].map(m=>vm.runInNewContext('('+m[0]+')'));assert.equal(profiles.length,6);cfg=profiles[detail];}
 return mid.createReference('style3',seed,false,'archive-heightfield',{updates:0,full:true,oneDir:true,fineHorizon:cfg.midlineFineHorizon,halfWidth:lane===0||lane===3?3.6:lane===1||lane===2?1.8:3.7,consumeRuntime:runtime=>{
 const {ctx,generator,input}=runtime,s=input.sources.Settings.toString(),n=input.sources.Normals.toString();
 const xi=mid.classSection(s,'class xi{'),ca=mid.classSection(s,'class Ca{');
 const install=road.install(runtime,lane,false);ctx.Segment=install.Segment;
 const blend=functional?require('./road-blend-reference.cjs'):null;if(functional)blend.install(runtime,lane,install);
 ctx.functionalTileSize=cfg.tileSize;
 const helpers=base.section(n,'function GC(a,e,t){','function WC(');
 vm.runInContext('globalThis.Ca=(function(){const I=shared,s=globalThis.n,z0=functionalTileSize,Na=20,F0=1000,z={},e0={addJob(){}},h0={add(){}},it=()=>"probe";let v3=0,q1=120,F2=240;'+helpers+'const a2=GC,e2=HC;'+ca.source+';return Ca;})()',ctx);
 vm.runInContext('globalThis.Xi=(function(){const I=shared,s=globalThis.n,c0=class{add(){} clear(){}};let Y2='+cfg.roadNodes+',tt,T4;'+xi.source+';return xi;})()',ctx);
 for(let k=0;k<10000 && (ctx.shared.tail.i<ctx.shared.vehicleIndex+2500||ctx.shared.fineTail.i<ctx.shared.vehicleIndex+cfg.midlineFineHorizon);k++)generator.update();
 let index=new ctx.Ca();index.initialise(cfg.midlineIndexMargin);while(!index.load()){}
 let x=new ctx.Xi(cfg.roadNodes,ctx.Segment);[x.prevSeg,x.curSeg,x.nextSeg].forEach((o,i)=>o.identity=i);x.init();while(!x.load()){}
 const events=[];const bridges=[];const seen=new Set();let ordinal=0;
 function hash(segment){let h=2166136261;for(const key of ['position','normal','uv','bridge','shadow','paintSolid','paintSolidLeft','paintSolidRight','paintMergeLeft','paintMergeRight']){let arr=segment.mesh.geometry.attributes[key]?.array;if(!arr)continue;const len=key==='position'||key==='normal'?segment.genIndex:key==='uv'?segment.uvIndex:segment.genIndex/3;const view=new DataView(arr.buffer);for(let i=0;i<len;i++)h=Math.imul(h^view.getUint32(i*4,true),16777619)>>>0;}return h;}
 function capture(segment){
  const arrays=[];for(const key of ['position','normal','uv','bridge','shadow','paintSolid','paintSolidLeft','paintSolidRight','paintMergeLeft','paintMergeRight']){const a=segment.geo.attributes[key];arrays.push(Array.from(a.array.slice(0,segment.genIndex/3*a.itemSize)));}
  const coarse=[];for(let node=segment.headNode;node&&node.i<=segment.node.i;node=node.next)coarse.push(blend.encode(node));
  return {coarse,data:arrays.flat(),indices:Array.from(segment.indexArr),visible:segment.mesh.visible,origin:[segment.o.x,segment.o.y,segment.o.z]};
 }
 function captureBridges(){
  if(!functional||!options.bridges)return;
  for(let node=ctx.shared.head;node&&node.i<ctx.shared.fineTail.i-4;node=node.next){
   if(!node.bridge||node.prev?.bridge||seen.has(node.i)||node.i<0)continue;
   let end=node;while(end.bridge&&end.next)end=end.next;if(end.i>=ctx.shared.fineTail.i-4)continue;
   seen.add(node.i);let first=node.prev?.prev,last=end.next?.next;if(!first||!last)continue;
   const nodes=[];for(let p=first.prev;p&&p.i<=last.i+3;p=p.next)nodes.push(p);
   const samples=[];for(let p=first;p&&p!==last;p=p.nextFine){samples.push({p:p.p,n:p.n});if(p.nextFine)samples.push({p:{x:(p.p.x+p.nextFine.p.x)/2,y:(p.p.y+p.nextFine.p.y)/2,z:(p.p.z+p.nextFine.p.z)/2},n:p.n});}
   const queries=[];for(const v of samples)for(const d of [0,-1,1,-install.configuration.halfWidth,install.configuration.halfWidth,-5,5,-9,9,-20,20]){const xx=v.p.x+v.n.x*d,zz=v.p.z+v.n.z*d;queries.push(blend.query(ctx,nodes,xx,zz,generator.heightmap.getXZ(xx,zz),3));}
   bridges.push({lane,nodes:nodes.map(blend.encode),queries,start:node.i,end:end.i});
  }
 }
 let all=[x.prevSeg,x.curSeg,x.nextSeg];
 for(const o of all){const f=o.finalise;o.finalise=function(){f.call(this);events.push({tick:ordinal,id:this.identity,head:this.headNode.i,tail:this.node.i,phase:ctx.readRoadPhase(),hash:hash(this),paint:[this.paintSolidConstraint,this.paintSolidLeftConstraint,this.paintSolidRightConstraint],...(functional?capture(this):{})});};}
const recovery=[];const invalidations=[];let epoch=ctx.shared.generation,referenceError=null;
 function resetMarkings(){
  // Explicit user-authorized compatibility adapter; original B7 bodies remain untouched.
  ctx.clearReferenceMarkingsScratch();generator.markingsNode=ctx.shared.head;
  for(const key of ['markingsAbsDa','markingsDa','markingsGradLat','markingsGradLonDiff','markingsTrees','prevGradLonDiff','prevMarkingsDa','solidLineLeftExtensions','solidLineRightExtensions'])generator[key]=0;
  generator.prevSolidLeftSeg={start:null,end:null};generator.prevSolidRightSeg={start:null,end:null};generator.hasSolidLineLeft=generator.hasSolidLineRight=false;
  generator.solidLineRightPrepend=generator.solidLineExtensions=(generator.config.markingsMargin??6)/2;
 }
 function state(){const g=generator,v=ctx.shared;return {generation:v.generation,head:v.head.i,tail:v.tail.i,fine:v.fineTail.i,observer:v.vehicleIndex,
  tail:[v.tail.p.x,v.tail.p.y,v.tail.p.z],layers:[...g.tailHeightmap.layers],headLayers:[...g.headHeightmap.layers],
  anti:[g.antiTarget.x,g.antiTarget.y,g.antiTarget.z,g.antiTargetRange,g.antiTargetRevert],random:Array.from(g.alea.exportState()),startRandom:Array.from(g.rand.exportState()),trackers:g.turnTrackers.map(t=>t.da)};}
 const trace=[];let retirements=0;let reverts=0;const queries=[];
 for(ordinal=0;ordinal<(options?.nodes??6000)*ticksPerNode;ordinal++){
  const observer=-17+Math.floor(ordinal/ticksPerNode);ctx.shared.vehicleIndex=observer;let at=ctx.shared.head;while(at.next&&at.next.i<=observer)at=at.next;ctx.shared.vehicleNode=at;
  try {
  if(options?.fault&&ordinal===(options.faultOrdinal??512))mid.injectFault(generator,ctx,options.fault);
  if(options?.compatibleRestart&&ctx.shared.generation!==epoch)resetMarkings();
  let did=generator.update();if(!did&&ctx.shared.tail.i<observer+2500)reverts++;
  if(options?.fault&&ctx.shared.generation!==epoch){
   invalidations.push({tick:ordinal,from:epoch,to:ctx.shared.generation});epoch=ctx.shared.generation;
   for(let k=0;k<20000&&(ctx.shared.tail.i<ctx.shared.vehicleIndex+2500||ctx.shared.fineTail.i<ctx.shared.vehicleIndex+cfg.midlineFineHorizon);k++)generator.update();
   index=new ctx.Ca();index.initialise(cfg.midlineIndexMargin);while(!index.load()){}
   x=new ctx.Xi(cfg.roadNodes,ctx.Segment);[x.prevSeg,x.curSeg,x.nextSeg].forEach((o,i)=>o.identity=i);
   all=[x.prevSeg,x.curSeg,x.nextSeg];for(const o of all){const f=o.finalise;o.finalise=function(){f.call(this);events.push({tick:ordinal,id:this.identity,head:this.headNode.i,tail:this.node.i,phase:ctx.readRoadPhase(),...capture(this)});};}x.init();while(!x.load()){}
   did=generator.update();
  }
  if(did)index.update();x.update();
  if(ordinal%100===0){const p=at.p,q=index.getClosestNode(p.x+1,p.z-1,50);queries.push({tick:ordinal,x:p.x+1,z:p.z-1,index:q?.n.i??-999999,d:q?.d??0,segments:(index.getSegmentsForTile(Math.floor((p.x+1)/cfg.tileSize),Math.floor((p.z-1)/cfg.tileSize))??[]).map(s=>({head:s.head.i,tail:s.tail.i,mid:s.mid?.i??-999999})),p:q?[q.n.p.x,q.n.p.y,q.n.p.z]:[],...(functional?{stableExclusive:Math.max(ctx.shared.head.i,Math.min(ctx.shared.fineTail.i,generator.markingsNode.i-Math.ceil(11+(generator.config.markingsMargin??6)/2))),blend:q&&q.n.i>ctx.shared.head.i+1?blend.query(ctx,[q.n],p.x+1,p.z-1,generator.heightmap.getXZ(p.x+1,p.z-1),0):null,lods:[0,1,2].map(id=>all.find(v=>v.identity===id).curLod)}:{})});}
  if(options?.fault&&(ordinal>= (options.faultOrdinal??512)&&ordinal<(options.faultOrdinal??512)+40||ordinal===(options.nodes*ticksPerNode-1)))recovery.push({tick:ordinal,...state()});
  if(functional&&ordinal%ticksPerNode===0)captureBridges();
  if(!functional)trace.push({head:ctx.shared.head.i,tail:ctx.shared.tail.i,fine:ctx.shared.fineTail.i,index:index.lookaheadNode.i,lookahead:x.lookaheadNode.i,prev:x.prevSeg.identity,cur:x.curSeg.identity,next:x.nextSeg.identity,swap:x.nextSwapIndex});
  // Unity standalone road adaptation: retire old tiles only after source lastSeen is behind the retention boundary.
  for(const tx of Object.keys(index.tileIndex))for(const tz of Object.keys(index.tileIndex[tx]))if(index.lastSeenIndex[tx][tz]<observer-100){index.retireTile(Number(tx)-1000,Number(tz)-1000);retirements++;}
  }catch(error){if(!options?.captureReferenceError)throw error;referenceError={tick:ordinal,name:error.name,message:error.message,head:ctx.shared.head?.i,fine:ctx.shared.fineTail?.i,markings:generator.markingsNode?.i??null};break;}
 }
 return {schema:1,ticksPerNode,seed,style:3,oneDir:true,lane,detail,intervals:cfg.roadNodes,tileSize:cfg.tileSize,margin:cfg.midlineIndexMargin,...(functional?{bridges,totalTicks:ordinal,retirements,...(options.fault?{fault:options.fault,compatibleRestart:!!options.compatibleRestart,referenceError,faultOrdinal:options.faultOrdinal??512,recovery,invalidations}:{})}:{}),hashes:runtime.sourceHashes,sections:{xi:xi.sha256,ca:ca.sha256},spatialProbe:spatialProbe(),ticks:trace,events,queries,reverts};
 }});}
if(require.main===module){const fast=process.argv.includes('--fast'),file=path.join(__dirname,fast?'Fixtures/Streaming/hills-streaming-fast.json':'Fixtures/Streaming/hills-streaming.json'),v=build(fast?4:16),json=JSON.stringify(v)+'\n';if(process.argv.includes('--write')){fs.mkdirSync(path.dirname(file),{recursive:true});fs.writeFileSync(file,json);}else assert.equal(fs.readFileSync(file,'utf8'),json,'Streaming reference drift');console.log(JSON.stringify({ticks:v.ticks.length,events:v.events.length,reverts:v.reverts,queries:v.queries.length}));}
module.exports={build};

