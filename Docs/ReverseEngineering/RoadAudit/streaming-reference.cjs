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
function build(ticksPerNode=16){return mid.createReference('style3','a',false,'archive-heightfield',{updates:0,full:true,oneDir:true,consumeRuntime:runtime=>{
 const {ctx,generator,input}=runtime,s=input.sources.Settings.toString(),n=input.sources.Normals.toString();
 const xi=mid.classSection(s,'class xi{'),ca=mid.classSection(s,'class Ca{');
 const install=road.install(runtime,4,false);ctx.Segment=install.Segment;
 const helpers=base.section(n,'function GC(a,e,t){','function WC(');
 vm.runInContext('globalThis.Ca=(function(){const I=shared,s=globalThis.n,z0=840,Na=20,F0=1000,z={},e0={addJob(){}},h0={add(){}},it=()=>"probe";let v3=0,q1=120,F2=240;'+helpers+'const a2=GC,e2=HC;'+ca.source+';return Ca;})()',ctx);
 vm.runInContext('globalThis.Xi=(function(){const I=shared,s=globalThis.n,c0=class{add(){} clear(){}};let Y2=30,tt,T4;'+xi.source+';return xi;})()',ctx);
 for(let k=0;k<10000 && (ctx.shared.tail.i<ctx.shared.vehicleIndex+2500||ctx.shared.fineTail.i<ctx.shared.vehicleIndex+400);k++)generator.update();
 const index=new ctx.Ca();index.initialise(180);while(!index.load()){}
 const x=new ctx.Xi(30,ctx.Segment);[x.prevSeg,x.curSeg,x.nextSeg].forEach((o,i)=>o.identity=i);x.init();while(!x.load()){}
 const events=[];const seen=new Map();let ordinal=0;
 function hash(segment){let h=2166136261;for(const key of ['position','normal','uv','bridge','shadow','paintSolid','paintSolidLeft','paintSolidRight','paintMergeLeft','paintMergeRight']){let arr=segment.mesh.geometry.attributes[key]?.array;if(!arr)continue;const len=key==='position'||key==='normal'?segment.genIndex:key==='uv'?segment.uvIndex:segment.genIndex/3;const view=new DataView(arr.buffer);for(let i=0;i<len;i++)h=Math.imul(h^view.getUint32(i*4,true),16777619)>>>0;}return h;}
 const all=[x.prevSeg,x.curSeg,x.nextSeg];
 for(const o of all){const f=o.finalise;o.finalise=function(){f.call(this);events.push({tick:ordinal,id:this.identity,head:this.headNode.i,tail:this.node.i,phase:ctx.readRoadPhase(),hash:hash(this),paint:[this.paintSolidConstraint,this.paintSolidLeftConstraint,this.paintSolidRightConstraint]});};}
 const trace=[];let reverts=0;const queries=[];
 for(ordinal=0;ordinal<6000*ticksPerNode;ordinal++){
  const observer=-17+Math.floor(ordinal/ticksPerNode);ctx.shared.vehicleIndex=observer;let at=ctx.shared.head;while(at.next&&at.next.i<=observer)at=at.next;ctx.shared.vehicleNode=at;
  const did=generator.update();if(!did&&ctx.shared.tail.i<observer+2500)reverts++;
  if(did)index.update();x.update();
  if(ordinal%100===0){const p=at.p,q=index.getClosestNode(p.x+1,p.z-1,50);queries.push({tick:ordinal,x:p.x+1,z:p.z-1,index:q?.n.i??-999999,d:q?.d??0,segments:(index.getSegmentsForTile(Math.floor((p.x+1)/840),Math.floor((p.z-1)/840))??[]).map(s=>({head:s.head.i,tail:s.tail.i,mid:s.mid?.i??-999999})),p:q?[q.n.p.x,q.n.p.y,q.n.p.z]:[]});}
  trace.push({head:ctx.shared.head.i,tail:ctx.shared.tail.i,fine:ctx.shared.fineTail.i,index:index.lookaheadNode.i,lookahead:x.lookaheadNode.i,prev:x.prevSeg.identity,cur:x.curSeg.identity,next:x.nextSeg.identity,swap:x.nextSwapIndex});
  // Unity standalone road adaptation: retire old tiles only after source lastSeen is behind the retention boundary.
  for(const tx of Object.keys(index.tileIndex))for(const tz of Object.keys(index.tileIndex[tx]))if(index.lastSeenIndex[tx][tz]<observer-100)index.retireTile(Number(tx)-1000,Number(tz)-1000);
 }
 return {schema:1,ticksPerNode,seed:'a',style:3,oneDir:true,lane:4,detail:2,intervals:30,tileSize:840,margin:180,hashes:runtime.sourceHashes,sections:{xi:xi.sha256,ca:ca.sha256},spatialProbe:spatialProbe(),ticks:trace,events,queries,reverts};
 }});}
if(require.main===module){const fast=process.argv.includes('--fast'),file=path.join(__dirname,fast?'Fixtures/Streaming/hills-streaming-fast.json':'Fixtures/Streaming/hills-streaming.json'),v=build(fast?4:16),json=JSON.stringify(v)+'\n';if(process.argv.includes('--write')){fs.mkdirSync(path.dirname(file),{recursive:true});fs.writeFileSync(file,json);}else assert.equal(fs.readFileSync(file,'utf8'),json,'Streaming reference drift');console.log(JSON.stringify({ticks:v.ticks.length,events:v.events.length,reverts:v.reverts,queries:v.queries.length}));}
module.exports={build};

