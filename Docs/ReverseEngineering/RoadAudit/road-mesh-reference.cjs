// P4 support for the existing midline-reference entry. All geometry executes D/Nl
// from the hash-verified archive; these adapters only store rendering data.
const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm'),assert=require('node:assert/strict');
const base=require('./heightfield-reference.cjs');
const bits=x=>{const b=Buffer.alloc(8);b.writeDoubleLE(x??NaN);return b.readBigUInt64LE().toString(16).padStart(16,'0');};
const fbits=a=>Array.from(a,x=>{const b=Buffer.alloc(4);b.writeFloatLE(x);return b.readInt32LE();});
function install(runtime,laneStyle,baseOnly){
  const {ctx,input,profile}=runtime,{classSection,checkedSection}=require('./midline-reference.cjs');
  const dev=input.sources.Dev.toString(),hills=input.sources.Hills.toString(),normals=input.sources.Normals.toString();
  assert(normals.includes('l_ as ax')&&normals.includes('Wm as ay')&&normals.includes('r_ as dT')&&normals.includes('r_=.02,'));
  const enumeration=base.literal(normals,'{OffRoadWide:0,OffRoad:1,'),lanes=JSON.parse(normals.match(/\bl_=(\[[0-9,]+\])/)[1]);
  const laneWidth=base.literal(hills,'{single:{standard:3.6,');
  const widthSource=checkedSection(hills,'xl=t=>{',',cr=()=>{');
  ctx.roadProfile=JSON.parse(JSON.stringify(profile));ctx.laneWidth=laneWidth;ctx.laneEnum=enumeration;ctx.laneCounts=lanes;
  ctx.roadLaneStyle=laneStyle;
  const configuration=vm.runInContext('(function(){let N,F,We,me,Ve,ra,Ea,pe,O,na,la,Lt,Oe,He,xl;'+
    'const te={roads:{style:{3:roadProfile},laneWidth}},Cr=laneCounts,z=laneEnum,Mt=400,cr=()=>{};'+
    widthSource.source+';xl({roadStyle:3,laneStyle:roadLaneStyle});return {halfWidth:O,offRoad:N,singleOffRoad:F,laneWidth:na,laneStyle:roadLaneStyle};})()',ctx);
  const d=checkedSection(dev,'let fe=0,','let U=D;'),nl=classSection(hills,'class Nl extends Ut{');
  vm.runInContext(`globalThis.RoadAttribute=class{constructor(array,itemSize){this.array=array;this.itemSize=itemSize;}setUsage(){}};
    globalThis.RoadGeometry=class{constructor(){this.attributes={};}setAttribute(k,v){this.attributes[k]=v;}
    setIndex(a){this.index={array:Uint16Array.from(a,x=>x||0)};}setDrawRange(start,count){this.drawRange={start,count};}dispose(){}};
    globalThis.RoadMesh=class{constructor(g,m){this.geometry=g;this.material=m;this.position=new Vec3();this.visible=true;}updateMatrix(){}};
    globalThis.RoadSphere=class{constructor(){this.center=new Vec3();}};`,ctx);
  vm.runInContext('globalThis.OriginalD=(function(){const m=Vec3,K=RoadGeometry,J=RoadMesh,O=RoadAttribute,B=0,ee=RoadSphere,$=class{},G=.02,i=shared;'+
    d.source+';globalThis.readRoadPhase=()=>M;globalThis.writeRoadPhase=v=>M=v;return D;})()',ctx);
  ctx.meshHalfWidth=configuration.halfWidth;ctx.meshOffRoad=configuration.offRoad;
  vm.runInContext('globalThis.OriginalNl=(function(){const Ut=OriginalD,T=n,K=RoadAttribute,q=0,c={},O=meshHalfWidth,N=meshOffRoad;'+
    nl.source+';return Nl;})()',ctx);
  if(baseOnly){configuration.halfWidth=3.2;ctx.OriginalD.prototype.customGenNext=()=>{};ctx.OriginalD.prototype.customGenFine=()=>{};}
  return {configuration,Segment:baseOnly?ctx.OriginalD:ctx.OriginalNl,
    sections:{D:d.sha256,Nl:nl.sha256,width:widthSource.sha256},enumeration,laneCounts:lanes};
}
function synthetic(ctx,shape,origin,bridge){
  const nodes=Array.from({length:12},(_,i)=>({i,p:new ctx.Vec3(origin+(shape==='degenerate'&&i===2?1:i)*10,
    shape==='crest'?20+(i%4)*2:20+i*(shape==='slope'?2:0),shape==='curve'?Math.sin(i*.8)*15:0),
    n:new ctx.Vec3(0,0,1),bridge:bridge==='all'||!!bridge&&i>=2&&i<=4,
    shadowLeft:i/20,shadowRight:1-i/20,paintSolidLeftWeight:i%3===0?1:0,paintSolidRightWeight:i%4<2?1:0,
    paintMergeLeft:shape==='constraints'?(i===5||i===9):i===3,paintMergeRight:shape==='constraints'?(i===5||i===9):i===6}));
  nodes.forEach((n,i)=>{n.prev=nodes[i-1]??null;n.next=nodes[i+1]??null;});
  for(let i=0;i<nodes.length-1;i++){
    const fs=shape==='dense'?10:[1,2,5,10][i%4];nodes[i].fs=fs;let p=nodes[i];
    for(let j=1;j<=fs;j++){
      const next=j===fs?nodes[i+1]:{p:new ctx.Vec3().copy(nodes[i].p).lerp(nodes[i+1].p,j/fs),
        n:new ctx.Vec3(0,0,1),prev:nodes[i],next:nodes[i+1],shadowLeft:i/20,shadowRight:1-i/20};
      p.nextFine=next;next.prevFine=p;p=next;
    }
  }
  for(let p=nodes[0];p;p=p.nextFine){
    const a=p.prevFine??p,b=p.nextFine??p;
    const x=b.p.x-a.p.x,z=b.p.z-a.p.z,l=Math.sqrt(x*x+z*z);
    if(l)p.n.set(-z/l,0,x/l);else p.n.set(0,0,1);
  }
  return nodes;
}
function captureInput(head){
  const coarse=[];for(let n=head;n;n=n.next)coarse.push(n);
  const points=[];for(let n=head;n;n=n.nextFine)points.push(n);
  const encode=n=>({index:n.i,p:[n.p.x,n.p.y,n.p.z].map(bits),side:[n.n.x,n.n.y,n.n.z].map(bits),bridge:!!n.bridge,
    hasShadowLeft:Object.hasOwn(n,'shadowLeft'),hasShadowRight:Object.hasOwn(n,'shadowRight'),
    shadowLeft:bits(n.shadowLeft),shadowRight:bits(n.shadowRight),
    hasLeft:Object.hasOwn(n,'paintSolidLeftWeight'),hasRight:Object.hasOwn(n,'paintSolidRightWeight'),
    left:bits(n.paintSolidLeftWeight),right:bits(n.paintSolidRightWeight),
    hasMergeLeft:Object.hasOwn(n,'paintMergeLeft'),hasMergeRight:Object.hasOwn(n,'paintMergeRight'),
    mergeLeft:!!n.paintMergeLeft,mergeRight:!!n.paintMergeRight});
  return {coarse:coarse.map(encode),points:points.map(n=>({...encode(n),index:n.i??0,isCoarse:Object.hasOwn(n,'i'),
    owner:n.i??n.prev.i,previous:n.prev?.i??-2147483648,next:n.next?.i??-2147483648}))};
}
function state(s,ctx){return {phase:bits(ctx.readRoadPhase()),constraints:Object.values(s.lerpConstraints??{}).map(x=>x===null?null:bits(x))};}
function capture(s,ctx,coarse){
  const count=s.genIndex/3,attributes={};
  for(const name of ['position','normal','uv','bridge','shadow','paintSolid','paintSolidLeft','paintSolidRight','paintMergeLeft','paintMergeRight']){
    const a=s.geo.attributes[name];if(a)attributes[name]=fbits(a.array.slice(0,count*a.itemSize));
  }
  const origin=[s.o.x,s.o.y,s.o.z].map(bits);
  return {vertexCount:count,coarseCount:s.lod0LiveIndex,fineStart:s.lod1IndexStart,
    fineCount:s.lod1LiveIndex-s.lod1IndexStart,attributes,indices:Array.from(s.indexArr),origin,
    visible:s.mesh.visible,draw:s.geo.drawRange,lod:s.curLod,end:state(s,ctx),
    writes:{position:s.genIndex,uv:s.uvIndex,bridge:s.bridgeIndex??0,shadow:s.shadowIndex??0,paint:s.solidIndex??0},
    sphere:[s.geo.boundingSphere.center.x,s.geo.boundingSphere.center.y,s.geo.boundingSphere.center.z,s.geo.boundingSphere.radius].map(bits),
    paint:coarse.map(n=>({index:n.i,hasLeft:Object.hasOwn(n,'paintSolidLeftWeight'),hasRight:Object.hasOwn(n,'paintSolidRightWeight'),
      left:bits(n.paintSolidLeftWeight),right:bits(n.paintSolidRightWeight),
      hasMergeLeft:Object.hasOwn(n,'paintMergeLeft'),hasMergeRight:Object.hasOwn(n,'paintMergeRight'),
      mergeLeft:!!n.paintMergeLeft,mergeRight:!!n.paintMergeRight,
      hasSolid:Object.hasOwn(n,'paintSolid'),solid:!!n.paintSolid,
      hasSolidLeft:Object.hasOwn(n,'paintSolidLeft'),solidLeft:!!n.paintSolidLeft,
      hasSolidRight:Object.hasOwn(n,'paintSolidRight'),solidRight:!!n.paintSolidRight}))};
}
function buildCase(createReference,spec){
  return createReference(spec.style??'style3','a',spec.observe??true,'archive-heightfield',{
    updates:spec.natural?(spec.style==='style0'?2000:4100):0,full:true,
    consumeRuntime:runtime=>{
      const {ctx,generator}=runtime,installed=install(runtime,spec.lane,spec.baseOnly);
      const nodes=spec.natural?null:synthetic(ctx,spec.shape,spec.origin??0,spec.bridge);
      const head=nodes?nodes[0]:ctx.shared.head;
      const coarse=[];for(let n=head;n;n=n.next)coarse.push(n);
      const input=captureInput(head),A=4,segments=[new installed.Segment(A),new installed.Segment(A)];
      ctx.writeRoadPhase(spec.phase??0);ctx.shared.vehicleIndex=0;
      const generations=[];
      for(const [object,start] of [[0,0],[1,4],[0,0]]){
        const segment=segments[object],before=state(segment,ctx);
        segment.initGeneration(coarse[start]);
        let calls=0;while(!segment.genNext())assert(++calls<A+1,'Segment did not finish');
        segment.finalise();const output=capture(segment,ctx,coarse);
        segment.upgrade();const fineDraw={...segment.geo.drawRange};segment.downgrade();const coarseDraw={...segment.geo.drawRange};
        let shadowRefresh=null;
        if(!spec.baseOnly){segment.regenerateShadows();shadowRefresh={cursor:segment.shadowIndex,shadow:fbits(segment.shadowArr)};}
        generations.push({object,startIndex:coarse[start].i,before,output,fineDraw,coarseDraw,shadowRefresh});
      }
      return {id:spec.id,kind:spec.natural?'archive-B7-chain':'synthetic-frozen-chain',seed:'a',
        style:spec.style??'style3',profile:runtime.profile,effectiveMidlineConfig:generator.config,
        graphics:{detail:2,heightmapMaxCached:20,treeDensity:'Auto'},effectiveHeightfieldConfig:ctx.heightConfig,observerIndex:0,
        unityBinding:{generation:1,revision:1,note:'Frozen test binding, not a declaration of P3 publication readiness'},
        configuration:{...installed.configuration,baseOnly:!!spec.baseOnly,intervals:A},input,generations,
        sourceHashes:runtime.sourceHashes,sections:installed.sections,
        laneEnum:installed.enumeration,laneCounts:installed.laneCounts};
    }});
}
function run(createReference){
  const specs=[];
  for(let lane=0;lane<8;lane++)specs.push({id:'lane-'+lane,lane,shape:lane%2?'curve':'slope',bridge:lane<2,phase:9.8});
  specs.push({id:'straight',lane:4,shape:'straight'}, {id:'crest',lane:4,shape:'crest'},
    {id:'large-negative-origin',lane:4,shape:'curve',origin:-1e9,phase:21.3},
    {id:'large-positive-origin',lane:4,shape:'curve',origin:1e9,phase:-21.3},
    {id:'persistent-constraints',lane:2,shape:'constraints',phase:9.8},
    {id:'offroad-all-bridge',lane:1,shape:'curve',bridge:'all'},
    {id:'offroad-hidden',lane:1,shape:'curve'}, {id:'base-D',lane:4,shape:'slope',baseOnly:true},
    {id:'dense-capacity',lane:4,shape:'dense'}, {id:'zero-tangent',lane:4,shape:'degenerate'},
    {id:'natural-style0',lane:4,natural:true,style:'style0'}, {id:'natural-style3',lane:4,natural:true,style:'style3'});
  const cases=specs.map(spec=>buildCase(createReference,spec));
  // Existing query observers are diagnostic wrappers, not an alternative geometry oracle.
  for(const spec of specs.filter(s=>s.natural))
    assert.equal(JSON.stringify(buildCase(createReference,{...spec,observe:false})),
      JSON.stringify(cases.find(c=>c.id===spec.id)),'Query observers changed road mesh output: '+spec.id);
  const result={schema:1,version:'1.0.2',archive:'Slow Roads/resources/app.asar',adapterVersion:1,
    numericEncoding:'double:16 hex IEEE754 bits, float:signed int32 bits; missing values have presence flags',cases};
  const file=path.join(__dirname,'Fixtures/RoadMesh/hills-road-mesh.json'),json=JSON.stringify(result)+'\n';
  if(process.argv.includes('--write')){fs.mkdirSync(path.dirname(file),{recursive:true});fs.writeFileSync(file,json);}
  else assert.equal(fs.readFileSync(file,'utf8'),json,'Road mesh reference drift');
  console.log(JSON.stringify({schema:1,cases:cases.length,builds:cases.reduce((s,c)=>s+c.generations.length,0)}));
}
module.exports={run};
