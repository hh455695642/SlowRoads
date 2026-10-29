// Hills P2/P3 reference entry. Executes hash-pinned archive methods in an isolated VM.
// node Docs/ReverseEngineering/RoadAudit/midline-reference.cjs [--style0] [--scenario explicit|retry-low] [--write | --self-test]
const fs=require('node:fs');
const path=require('node:path');
const vm=require('node:vm');
const crypto=require('node:crypto');
const assert=require('node:assert/strict');
const base=require('./heightfield-reference.cjs');

const fixtureDir=path.join(__dirname,'Fixtures/Midline');
const manifest=JSON.parse(fs.readFileSync(path.join(__dirname,'../SceneArchitecture/source-manifest.json'),'utf8'));
const sha=bytes=>crypto.createHash('sha256').update(bytes).digest('hex');

// Extract the exact archived Three.js vector classes. A hand-written adapter can
// silently shift rounding order at bridge and curve thresholds.
function classSection(source,marker){
  const start=source.indexOf(marker);
  assert(start>=0&&source.indexOf(marker,start+1)<0,'Non-unique vector class: '+marker);
  let depth=0,quote=null;
  for(let i=source.indexOf('{',start);i<source.length;i++){
    const char=source[i];
    if(quote){if(char==='\\')i++;else if(char===quote)quote=null;continue;}
    if(char==='"'||char==="'"||char==='`'){quote=char;continue;}
    if(char==='{')depth++;
    if(char==='}'&&--depth===0){const part=source.slice(start,i+1);return {source:part,sha256:sha(part)};}
  }
  throw Error('Unclosed vector class: '+marker);
}
function checkedSection(source,start,end){
  const part=base.section(source,start,end);
  assert(part.length>0&&part.length<200000,'Unexpected section size: '+start);
  return {source:part,sha256:sha(part)};
}
const snapshotNode=n=>n?{i:n.i,p:[n.p.x,n.p.y,n.p.z],a:n.a,da:n.da,t:n.t,
    g:n.g,gf:n.gf,gfa:n.gfa,h:n.h,n:[n.n.x,n.n.y,n.n.z],
    bridge:!!n.bridge,blend:n.blend??null,o:n.o??null,fs:n.fs??null}:null;
function snapshot(g,shared,layerEvents,queryEvents){
  const node=snapshotNode;
  return {
    generation:shared.generation,head:node(shared.head),tail:node(shared.tail),
    fineTail:node(shared.fineTail),smoothWindowHead:node(g.smoothWindowHead),
    startNode:g.startNode,restartCount:g.restartCount,generationCount:g.generationCount,
    prevT:g.prevT,byWater:g.byWater,waterFactor:g.waterFactor,
    targetDir:[g.targetDir.x,g.targetDir.y,g.targetDir.z],prevTargetSide:g.prevTargetSide,
    targetOverflow:g.targetOverflow,accumulatedOverflow:g.accumulatedOverflow,
    antiTarget:[g.antiTarget.x,g.antiTarget.y,g.antiTarget.z],
    antiTargetRange:g.antiTargetRange,antiTargetTriggerCount:g.antiTargetTriggerCount,
    bridge:{active:g.bridging,approach:!!g.onBridgeApproach,dismount:!!g.onBridgeDismount,
      spawnIndex:g.bridge.spawnNode?.i??null,quality:g.bridge.quality,
      landing:[g.bridge.landing.x,g.bridge.landing.y,g.bridge.landing.z],
      distance:g.bridge.distance,height:g.bridge.height??null},
    tailLayers:[...g.tailHeightmap.layers],headLayers:[...g.headHeightmap.layers],
    trackers:g.turnTrackers.map(t=>({i:t.i,n:t.n?.i??null,da:t.da,threshold:t.daThreshold})),
    aleaState:Array.from(g.alea.exportState()),randState:Array.from(g.rand.exportState()),
    layerEventCount:layerEvents.length,queryCount:queryEvents.length,
  };
}
function scratch(g){
  const pick=(source,keys)=>Object.fromEntries(keys.filter(key=>Object.hasOwn(source,key))
    .map(key=>[key,source[key]]));
  return {
    T:pick(g.T,['feelDist','feelL','feelR','feelA','feelAvg','feelDif','gradLat','gradLon',
      'bT','gradLeft','gradRight','gradAvg','maxT','minT','maxDif','gradLon2','bDir']),
    E:pick(g.E,['targetDot','targetSide','targetAng','targetAdjust','targetRedirect',
      'violatedOverflow','violatedTurnback','shouldBridge','bridgeViolation',
      'roadSinkFactor','roadSinkFacotr','x','z','nx','nz','nxz','rad']),
  };
}

// Synthetic inputs only; the pinned original methods still perform recovery.
function injectFault(generator,ctx,name){
      if(name==='restart-boundary'){generator.restartGeneration();return;}
      generator.bridge.spawnNode=null;generator.bridge.quality=0;
      generator.accumulatedOverflow=0;
      for(const tr of generator.turnTrackers)tr.da=0;
      if(name.startsWith('tracker-')) generator.turnTrackers[Number(name.slice(-1))].da=10;
      if(name==='priority'||name==='restart')for(const tr of generator.turnTrackers)tr.da=10;
      if(name.startsWith('overflow')){
        generator.accumulatedOverflow=1000;
        if(name==='overflow-repeat'){
          generator.antiTarget.copy(ctx.shared.tail.p);generator.antiTarget.x+=10;
          generator.antiTargetRange=1e8;generator.antiTargetRevert=475;
        }
      }
      if(name==='layer-revert'){
        generator.turnTrackers[0].da=10;
        generator.tailHeightmap.prevSwitchNode=ctx.shared.tail.i-2;
        generator.tailHeightmap.layers=[1,1];generator.tailHeightmap.curSide=0;
        generator.heightmap.setLayers([1,1],true);
      }
      if(name==='bridge-overrun'){
        generator.bridging=true;generator.onBridgeApproach=false;
        generator.bridge.spawnNode=ctx.shared.head.next;
        generator.bridge.distance=10;generator.bridge.height=3;
        generator.bridge.landing.copy(ctx.shared.tail.p);
      }
      if(name==='post-fine-restart'){
        for(const tr of generator.turnTrackers)tr.da=10;
        ctx.shared.vehicleIndex=ctx.shared.tail.i;ctx.shared.vehicleNode=ctx.shared.tail;
      }
}
function createReference(style='style3',seed='a',observe=true,scenario='archive-heightfield',options={}){
  assert(['style0','style3'].includes(style),'Only Hills style0/style3 are supported');
  const steeringScenario=scenario.startsWith('steering-');
  const bridgeScenario=scenario.startsWith('bridge-');
  assert(['archive-heightfield','explicit','retry-low','restart','smoothing','fine','inputs','attributes'].includes(scenario)||
    ['steering-flat','steering-ramp-x','steering-ramp-z','steering-water','steering-transition'].includes(scenario)||
    ['bridge-good','bridge-mid-high','bridge-side-high','bridge-short',
      'bridge-anti-near','bridge-emergency','bridge-improve','bridge-sweep'].includes(scenario),
    'Unknown scenario: '+scenario);
  const input=base.readArchive(['Normals']);
  const sourceHashes=base.verifyHashes(input.sources,['Dev','Hills','Settings','Rand','Normals']);
  assert.equal(input.version,'1.0.2');
  const reference=base.oracle(input),ctx=reference.context;
  const dev=input.sources.Dev.toString('utf8'),hills=input.sources.Hills.toString('utf8');
  const settings=input.sources.Settings.toString('utf8'),normals=input.sources.Normals.toString('utf8');
  const pieces={
    vector2:classSection(normals,'class je{'),
    vector3:classSection(normals,'class w{'),
    curve:checkedSection(normals,'function yd(){','class $o extends wt{'),
    gradient:checkedSection(dev,'class ze extends oe{','const we='),
    midline:checkedSection(dev,'const pe=0','export{'),
    hillsMidline:checkedSection(settings,'const M={},','let E3='),
  };
  const profile=base.literal(hills,style==='style3'
    ?'{midline:{isStraight:!1,smoothWindow:7,maxGrad:.12,roadSink:.4'
    :'{midline:{isStraight:!0,smoothWindow:9,maxGrad:.2');
  assert(profile.midline&&profile.heightmap&&profile.treemap,'Incomplete Hills road profile');
  if(scenario==='explicit'){
    profile.midline.startPos={x:0,y:20,z:0};
    profile.midline.startHeading={x:1,y:0,z:0};
  }
  if(steeringScenario){
    profile.midline.startPos={x:0,y:20,z:100};
    profile.midline.startHeading={x:1,y:0,z:0};
  }
  if(scenario==='smoothing')profile.midline.smoothingStrength=options.strength;
  if(scenario==='fine'){
    profile.midline.minFineSegs=options.minimumSegments;
    profile.midline.curvatureHeightFactor=options.heightFactor;
  }

  // All numeric vector/curve methods execute from the hash-pinned archive.
  ctx.Vec2=vm.runInContext('(function(){'+pieces.vector2.source+';return je})()',ctx,{timeout:1000});
  ctx.Vec3=vm.runInContext('(function(){'+pieces.vector3.source+';return w})()',ctx,{timeout:1000});
  vm.runInContext('globalThis.Curve=(function(){const w=Vec3,fM=class {};'+pieces.curve.source+';return OE})()',ctx,{timeout:1000});
  vm.runInContext('globalThis.Gradient=(function(){const ce=Math.PI*2,s={};'+pieces.gradient.source+';return ze})()',ctx,{timeout:1000});
  const setup='const m=Vec3,q=Vec2,ie=Curve,o=Pl,ae=class {},ne=false;'+
    'const i={generation:0},y={add:()=>{}};Math.TAU=Math.PI*2;'+
    'globalThis.shared=i;globalThis.logs=y;';
  vm.runInContext('globalThis.Se=(function(){'+setup+pieces.midline.source+';return Se})()',ctx,{timeout:1000});
  ctx.referenceRoadHalfWidth=options.halfWidth??3.7;
  const hillsSetup='const E8=Se,k5=Gradient,I=shared,s=n,$=referenceRoadHalfWidth;'+
    'const p0={getShadowDensityAt:(x,z)=>{if(!globalThis.shadowProvider)throw Error("B7 shadow dependency requires real source");return globalThis.shadowProvider.getShadowDensityAt(x,z)}};';
  vm.runInContext('globalThis.B7=(function(){'+hillsSetup+pieces.hillsMidline.source+';globalThis.referenceBlend=H7;globalThis.clearReferenceMarkingsScratch=()=>{for(const key of Object.keys(M))delete M[key];};return B7;})()',ctx,{timeout:1000});
  const layerEvents=[],queryEvents=[];
  const layerState={value:[0,0],set(v){this.value=[...v];layerEvents.push({layers:[...v]});
    if(ctx.shadowProvider)ctx.shadowProvider.onHeightmapLayersChanged(v);}};
  const heightConfig={seed,...profile.heightmap,maxCached:20};
  // Se changes maxCached to 8 on its private heightmap config, exactly as source.
  ctx.midlineConfig={...profile.midline,...(options.fineHorizon===undefined?{}:{fineHorizon:options.fineHorizon}),...(options.oneDir===undefined?{}:{oneDir:options.oneDir})};ctx.heightConfig=heightConfig;ctx.layerState=layerState;
  const generator=vm.runInContext('new B7(midlineConfig,heightConfig,Pl,layerState)',ctx,{timeout:1000});
  if(scenario==='explicit'||scenario==='retry-low'||steeringScenario){
    let syntheticCalls=0;
    generator.heightmap.getXZ=(x,z)=>{
      if(scenario==='retry-low')return syntheticCalls++<(options.lowRetries??1)?-100:20;
      if(scenario==='steering-ramp-x')return 20+0.01*x;
      if(scenario==='steering-ramp-z')return 20+0.3*(z-100);
      if(scenario==='steering-water')return 2.5;
      if(scenario==='steering-transition')return 15;
      return 20;
    };
  }
  if(observe){
    const original=generator.heightmap.getXZ;
    generator.heightmap.getXZ=function(x,z,depth,...rest){
      const value=original.call(this,x,z,depth,...rest);
      queryEvents.push({x,z,depth:depth??null,value});
      return value;
    };
  }
  generator.initialise(seed,0);
  const initial=snapshot(generator,ctx.shared,layerEvents,queryEvents);
  const initialQueryEvents=queryEvents.slice(),initialLayerEvents=layerEvents.slice();
  let inputs=null;
  if(scenario==='inputs'){
    const shadow=classSection(settings,'class Wa{');
    const points=[[0,0],[399,399],[400,400],[-1,-1],[-500,-800],
      [initial.tail.p[0],initial.tail.p[2]]];
    ctx.shadowProfile=profile.treemap;ctx.inputSeed=seed;
    vm.runInContext('globalThis.ReferenceShadow=(function(){const s=n,q3=Pl,k5=Gradient,P={};'+
      'const R={detail:2,treeDensity:"Auto",addListener:()=>{}},X3={Auto:"Auto"};'+
      'const c2={value:[0,0],addListener:()=>{}};'+shadow.source+
      ';return function(density){R.treeDensity=density;const v=new Wa();v.init(inputSeed,shadowProfile);return v;}})()',ctx,{timeout:1000});
    const shadowCases=[];
    for(const density of ['Auto',1,4]) for(const layers of [[0,0],[1,2]]){
      const source=ctx.ReferenceShadow(density);
      source.onHeightmapLayersChanged(layers);
      shadowCases.push({density:density==='Auto'?0:density,auto:density==='Auto',detail:2,layers,
        densityFactor:source.densityFactor,queries:points.map(([x,z])=>({x,z,
          map:source.map.getXZ(x,z),shadow:source.getShadowDensityAt(x,z)}))});
    }
    inputs={style,seed,shadowClassSha256:shadow.sha256,
      blendQueries:points.map(([x,z])=>({x,z,value:ctx.referenceBlend.getXZ(x,z)})),shadowCases};
  }
  let attributes=null,headLayerChecks=null,shadowClassHash=null;
  if(scenario==='attributes'||options.full){
    const shadow=classSection(settings,'class Wa{');
    shadowClassHash=shadow.sha256;
    ctx.shadowProfile=profile.treemap;ctx.inputSeed=seed;
    vm.runInContext('globalThis.shadowProvider=(function(){const s=n,q3=Pl,k5=Gradient,P={};'+
      'const R={detail:2,treeDensity:"Auto",addListener:()=>{}},X3={Auto:"Auto"};'+
      'const c2={value:[0,0],addListener:()=>{}};'+shadow.source+
      ';const v=new Wa();v.init(inputSeed,shadowProfile);return v;})()',ctx,{timeout:1000});
  }
  if(options.headLayerChecks){
    headLayerChecks=[];
    for(const [x,z] of [[1,1],[1,-1],[-1,-1],[-1,1],[1,1],[1,-1],[-1,-1],[-1,1]]){
      ctx.shared.head.p.x=x;ctx.shared.head.p.z=z;generator.checkUpdateHeadHeightmap();
      const h=generator.headHeightmap;
      headLayerChecks.push({x,z,previousSide:h.prevSide,currentSide:h.curSide,nextSwitch:h.nextLayerSwitch,
        layers:[...h.layers],events:layerEvents.length,
        shadowMap:ctx.shadowProvider.map.getXZ(-400,350)});
    }
  }
  if(scenario==='attributes'){
    const nodes=Array.from({length:40},(_,j)=>({i:j,p:new ctx.Vec3(10*j,2+j%8,3*(j%4)**2),
      n:new ctx.Vec3(0,0,1),a:j*.01,da:(j%9-4)*.06,g:(j%5-2)*.7,gf:(j%7-3)*.4,h:j%3*.25,
      bridge:j>=12&&j<=15,prev:null,next:null}));
    for(let j=0;j<nodes.length;j++){nodes[j].prev=nodes[j-1]??null;nodes[j].next=nodes[j+1]??null;}
    ctx.shared.head=ctx.shared.fineTail=nodes[0];ctx.shared.tail=nodes.at(-1);
    generator.markingsNode=nodes[0];
    const inputNodes=nodes.map(n=>({i:n.i,p:[n.p.x,n.p.y,n.p.z],a:n.a,da:n.da,g:n.g,gf:n.gf,h:n.h,bridge:n.bridge}));
    const stages=[];
    for(let i=0;i<36;i++){
      generator.extendFine();
      const chain=[];
      for(let n=nodes[0];n;n=n.nextFine)chain.push({coarseIndex:n.i??-1,
        hasLeft:Object.hasOwn(n,'shadowLeft'),hasRight:Object.hasOwn(n,'shadowRight'),
        left:n.shadowLeft??0,right:n.shadowRight??0,
        leftFinite:Number.isFinite(n.shadowLeft),rightFinite:Number.isFinite(n.shadowRight)});
      const markings={};
      for(const key of ['markingsDa','markingsAbsDa','markingsGradLat','markingsGradLonDiff',
        'markingsTrees','prevGradLonDiff','prevMarkingsDa','solidLineLeftExtensions','solidLineRightExtensions',
        'hasSolidLineLeft','hasSolidLineRight'])markings[key]=generator[key];
      markings.node=generator.markingsNode?.i??-1;
      markings.leftStart=generator.prevSolidLeftSeg.start?.i??-1;
      markings.leftEnd=generator.prevSolidLeftSeg.end?.i??-1;
      markings.rightStart=generator.prevSolidRightSeg.start?.i??-1;
      markings.rightEnd=generator.prevSolidRightSeg.end?.i??-1;
      const paint=nodes.map(n=>({i:n.i,left:n.paintSolidLeftWeight??0,right:n.paintSolidRightWeight??0,
        hasLeft:Object.hasOwn(n,'paintSolidLeftWeight'),hasRight:Object.hasOwn(n,'paintSolidRightWeight'),
        mergeLeft:!!n.paintMergeLeft,mergeRight:!!n.paintMergeRight,
        hasMergeLeft:Object.hasOwn(n,'paintMergeLeft'),hasMergeRight:Object.hasOwn(n,'paintMergeRight')}));
      stages.push({fineTail:ctx.shared.fineTail.i,chain,markings,paint});
    }
    attributes={style,seed,shadowClassSha256:shadowClassHash,inputNodes,stages};
  }
  let fine=null;
  if(scenario==='fine'){
    const nodes=options.points.map((p,j)=>({i:j,p:new ctx.Vec3(...p),n:{x:0,y:0,z:1},prev:null,next:null}));
    for(let j=0;j<nodes.length;j++){nodes[j].prev=nodes[j-1]??null;nodes[j].next=nodes[j+1]??null;}
    const start=options.startIndex??0;
    ctx.shared.fineTail=nodes[start];
    const stages=[];
    for(let step=0;step<(options.steps??2);step++){
      ctx.Se.prototype.extendFine.call(generator);
      const chain=[];
      for(let n=nodes[start];n;n=n.nextFine){
        chain.push({isCoarse:Object.hasOwn(n,'i'),coarseIndex:n.i??-1,previousCoarse:n.prev?.i??-1,nextCoarse:n.next?.i??-1,
          p:[n.p.x,n.p.y,n.p.z],n:[n.n.x,n.n.y,n.n.z],normalFinite:[n.n.x,n.n.y,n.n.z].map(Number.isFinite),fs:n.fs??0,
          previousIndex:chain.length?chain.length-1:null,nextIndex:n.nextFine?chain.length+1:null});
      }
      stages.push({fineTailIndex:ctx.shared.fineTail.i,chain});
    }
    const curve=new ctx.Curve(options.points.slice(0,4).map(p=>new ctx.Vec3(...p)),false);
    const samples=[0,1/6,1/3,0.4,0.5,0.6,2/3,5/6,1].map(t=>{
      const p=curve.getPoint(t);return {t,p:[p.x,p.y,p.z]};
    });
    fine={points:options.points,inputPoints:options.points.map(([x,y,z])=>({x,y,z})),startIndex:start,minimumSegments:options.minimumSegments,
      heightFactor:options.heightFactor,stages,samples};
  }
  let restart=null;
  if(scenario==='restart'){
    const beforeQueries=queryEvents.length,beforeLayers=layerEvents.length;
    generator.restartGeneration();
    restart={queryEvents:queryEvents.slice(beforeQueries),layerEvents:layerEvents.slice(beforeLayers),
      state:snapshot(generator,ctx.shared,layerEvents,queryEvents)};
  }
  let smoothing=null;
  if(scenario==='smoothing'){
    const nodes=Array.from({length:20},(_,j)=>({
      i:j,p:new ctx.Vec3(10*j,20+j%3,j%2),a:j*0.01,da:(j%5-2)*0.005,
      h:0,bridge:j===options.bridgeIndex,prev:null,next:null,
    }));
    for(let j=0;j<nodes.length;j++){
      nodes[j].prev=nodes[j-1]??null;
      nodes[j].next=nodes[j+1]??null;
    }
    ctx.shared.head=nodes[0];ctx.shared.tail=nodes.at(-1);
    generator.smoothWindowHead=nodes[0];generator.smoothMidlineHeadH=0;
    const record=()=>({headIndex:generator.smoothWindowHead.i,
      smoothMidlineHeadH:generator.smoothMidlineHeadH,
      observation:Object.hasOwn(generator.E,'avg')?{
        average:generator.E.avg,windowHasBridge:generator.E.windowHasBridge,
        bridgeLerp:generator.E.bridgeLerp,correctionIndex:generator.E.s.i}:null,
      nodes:nodes.map(n=>({i:n.i,p:[n.p.x,n.p.y,n.p.z],a:n.a,da:n.da,h:n.h,
        bridge:n.bridge,bank:n.bank??null,hasBank:Object.hasOwn(n,'bank')}))});
    const stages=[record()];
    for(let j=0;j<8;j++){
      generator.smoothHeight();
      generator.smoothCurves();
      stages.push(record());
    }
    smoothing={strength:options.strength,bridgeIndex:options.bridgeIndex,
      smoothWindow:generator.config.smoothWindow,stages};
  }
  let bridgeTest=null;
  if(bridgeScenario&&scenario!=='bridge-improve'&&scenario!=='bridge-sweep'){
    const beforeQueries=queryEvents.length;
    generator.heightmap.getXZ=(x,z)=>{
      const shore=scenario==='bridge-short'?20:scenario==='bridge-emergency'?700:200;
      let value=x>shore?3:-5;
      if(scenario==='bridge-mid-high'&&x>70&&x<150&&Math.abs(z)<1)value=3;
      if(scenario==='bridge-side-high'&&Math.abs(z)>30)value=3;
      if(observe)queryEvents.push({x,z,depth:null,value});
      return value;
    };
    const spawn={i:10,p:new ctx.Vec3(0,3,0)};
    ctx.shared.tail={i:20};
    generator.targetDir.set(1,0,0);
    generator.E.targetDot=scenario==='bridge-emergency'?-0.5:0.5;
    generator.allowEmergencyBridge=scenario==='bridge-emergency';
    if(scenario==='bridge-anti-near')generator.antiBridgePoint.set(210,3,0);
    generator.testForBridge(spawn,1,new ctx.Vec3(1,0,0));
    bridgeTest={spawnInputIndex:spawn.i,spawnPosition:[0,3,0],tailIndex:20,
      direction:[1,0,0],targetDirection:[1,0,0],targetDot:generator.E.targetDot,
      allowEmergency:generator.allowEmergencyBridge,
      antiBridgePoint:[generator.antiBridgePoint.x,generator.antiBridgePoint.y,generator.antiBridgePoint.z],
      quality:generator.bridge.quality,spawnIndex:generator.bridge.spawnNode?.i??null,
      distance:generator.bridge.distance,
      landing:[generator.bridge.landing.x,generator.bridge.landing.y,generator.bridge.landing.z],
      scratchLanding:[generator.landing.x,generator.landing.y,generator.landing.z],
      queryEvents:queryEvents.slice(beforeQueries)};
  }
  let bridgeImprove=null;
  if(scenario==='bridge-improve'){
    const nodes=Array.from({length:15},(_,j)=>({i:j,
      p:new ctx.Vec3(j*10,options.highStop&&j===7?10:3+(j%4===0?1:0),j%3*2),
      prev:null,next:null}));
    for(let j=0;j<nodes.length;j++){
      nodes[j].prev=nodes[j-1]??null;nodes[j].next=nodes[j+1]??null;
    }
    const start=options.startIndex??10;
    const landing=new ctx.Vec3(140,3,50);
    generator.bridge.spawnNode=nodes[start];
    generator.bridge.landing.copy(landing);
    generator.improveBridgeStartNode();
    bridgeImprove={nodes:nodes.map(n=>({i:n.i,p:[n.p.x,n.p.y,n.p.z]})),
      startIndex:start,landing:[landing.x,landing.y,landing.z],
      highStop:!!options.highStop,resultIndex:generator.bridge.spawnNode.i};
  }
  let bridgeSweep=null;
  if(scenario==='bridge-sweep'){
    const nodes=[-20,-10,0].map((x,j)=>({i:j,p:new ctx.Vec3(x,3,0),
      n:{x:0,y:0,z:1},a:0,da:0,t:0,g:0,gfa:0,h:0,bridge:false,prev:null,next:null}));
    for(let j=0;j<nodes.length;j++){
      nodes[j].prev=nodes[j-1]??null;nodes[j].next=nodes[j+1]??null;
    }
    ctx.shared.head=nodes[0];ctx.shared.tail=nodes[2];ctx.shared.fineTail=nodes[0];
    generator.smoothWindowHead=nodes[0];
    for(const tracker of generator.turnTrackers){tracker.n=nodes[0];tracker.da=0;}
    generator.targetDir.set(1,0,0);
    generator.prevTargetSide=1;
    generator.heightmap.getXZ=(x,z,depth)=>{
      let value=x>200?3:-5;
      if(Math.abs(x)<1&&Math.abs(z)<1)value=3;
      else if(x<20&&x>-20&&Math.abs(z)<10)value=z<0?2.4:2.6;
      if(observe)queryEvents.push({x,z,depth:depth??null,value});
      return value;
    };
    generator.heightmap.getMidlineXZ=(x,z)=>generator.heightmap.getXZ(x,z);
    const calls=[];
    const originalTest=generator.testForBridge;
    generator.testForBridge=function(spawn,sign,direction){
      const before=queryEvents.length;
      const incoming=[direction.x,direction.y,direction.z];
      const result=originalTest.call(this,spawn,sign,direction);
      calls.push({spawnIndex:spawn.i,sign,direction:incoming,
        queryEvents:queryEvents.slice(before),
        scratchLanding:[this.landing.x,this.landing.y,this.landing.z],
        quality:this.bridge.quality,selectedSpawnIndex:this.bridge.spawnNode?.i??null});
      return result;
    };
    const before=queryEvents.length;
    const didExtend=generator.update();
    bridgeSweep={inputNodes:nodes.map(n=>({i:n.i,p:[n.p.x,n.p.y,n.p.z],
      n:[n.n.x,n.n.y,n.n.z],a:n.a,da:n.da})),
      calls,queryEvents:queryEvents.slice(before),didExtend,
      options:['optionA','optionB','optionC'].map(key=>{const v=generator.bridge[key];
        return {key,landing:[v.x,v.y,v.z]};}),
      candidate:{quality:generator.bridge.quality,spawnIndex:generator.bridge.spawnNode?.i??null,
        distance:generator.bridge.distance,
        landing:[generator.bridge.landing.x,generator.bridge.landing.y,generator.bridge.landing.z]},
      scratch:scratch(generator)};
  }
  const trace=[];
  const observerInputs=[];
  let lastRevert=-100;
  for(let ordinal=0;ordinal<(scenario==='archive-heightfield'?(options.updates??32):steeringScenario?1:0);ordinal++){
    if(options.progress && ordinal>=2600){
      ctx.shared.vehicleIndex=-17+ordinal-2600;
      let n=ctx.shared.head;while(n.next && n.next.i<=ctx.shared.vehicleIndex)n=n.next;
      ctx.shared.vehicleNode=n;observerInputs.push({ordinal,index:ctx.shared.vehicleIndex});
    }
    if(options.fault && ordinal===(options.faultOrdinal??(options.fault==='restart'?4:64))){
      // Labeled synthetic checkpoints exercise original methods, never a JS rewrite.
      injectFault(generator,ctx,options.fault);
    }
    const beforeQueries=queryEvents.length,beforeLayers=layerEvents.length;
    const didExtend=generator.update();
    if(!didExtend)lastRevert=ordinal;
    const transition=options.transitions?{
      lastResetIndex:generator.lastResetIndex,maxRetractIndex:generator.maxRetractIndex,
      antiTargetRevert:generator.antiTargetRevert,antiWeight:generator.antiWeight,
      revertTrackerIndex:generator.revertTrackerIndex,trackRevertCount:generator.trackRevertCount??null,
      allowEmergencyBridge:generator.allowEmergencyBridge??false,
      totalDA:generator.totalDA,
      violatedTurnback:!!generator.E.violatedTurnback,violatedOverflow:!!generator.E.violatedOverflow,
      bridgeViolation:!!generator.E.bridgeViolation,revertDistance:generator.E.revertDistance??null,
      maxRetract:generator.E.maxRetract??null,
    }:undefined;
    if(transition&&(!didExtend||ordinal===lastRevert+32||ordinal===options.updates-1)){
      transition.retained=[];
      for(let n=ctx.shared.head;n;n=n.next)transition.retained.push({...snapshotNode(n),
        previousIndex:n.prev?.i??-10000,nextIndex:n.next?.i??-10000,
        hasO:Object.hasOwn(n,'o'),hasBank:Object.hasOwn(n,'bank'),bank:n.bank??0});
    }
    let fineOutput;
    if(options.full){
      const chain=[];
      const start=ctx.shared.fineTail.prev?.prev??ctx.shared.head;
      for(let n=start;n;n=n.nextFine){
        chain.push({coarseIndex:n.i??-1,previousCoarse:n.prev?.i??-1,nextCoarse:n.next?.i??-1,
          p:[n.p.x,n.p.y,n.p.z],n:[n.n.x,n.n.y,n.n.z],
          hasLeft:Object.hasOwn(n,'shadowLeft'),hasRight:Object.hasOwn(n,'shadowRight'),
          left:n.shadowLeft??0,right:n.shadowRight??0,leftFinite:Number.isFinite(n.shadowLeft),rightFinite:Number.isFinite(n.shadowRight)});
      }
      fineOutput={fineTail:ctx.shared.fineTail.i,markingsNode:generator.markingsNode.i,
        markingsNext:generator.markingsNode.next?.i??-1,chain};
    }
    trace.push({ordinal,didExtend,queryEvents:queryEvents.slice(beforeQueries),...(fineOutput?{fineOutput}:{}),
      layerEvents:layerEvents.slice(beforeLayers),scratch:scratch(generator),
      state:snapshot(generator,ctx.shared,layerEvents,queryEvents),...(transition?{transition}:{})});
  }
  if(options.consumeRuntime)return options.consumeRuntime({ctx,generator,input,profile,sourceHashes,pieces});
  return {schema:1,version:input.version,style,seed,scenario,sourceHashes,
    sections:Object.fromEntries(Object.entries(pieces).map(([key,value])=>[key,value.sha256])),
    profile:{midline:profile.midline,heightmap:profile.heightmap,treemap:profile.treemap,
      graphicsDetail:2,treeDensity:'Auto',laneStyle:'Standard',startNode:0},
    effectiveMidlineConfig:JSON.parse(JSON.stringify(generator.config)),
    effectiveHeightConfig:JSON.parse(JSON.stringify(ctx.heightConfig)),
    inputContract:{coordinates:'world XZ / Y, JS Number',
      midlineHeightmapCache:8,worldHeightmapCache:20,
      note:scenario==='archive-heightfield'
        ?'32 original update() calls with frozen observerIndex=-17; no scene/vehicle objects'
        :steeringScenario?'Original B7/Se one update() with labeled synthetic height function'
        :scenario==='restart'?'Original B7/Se restartGeneration after initialise; no update() calls'
        :scenario==='smoothing'?'Original B7.smoothHeight and Se.smoothCurves on a 20-node synthetic chain'
        :bridgeScenario?'Original Se.testForBridge with labeled synthetic shoreline; no update() calls'
        :'Original B7/Se with labeled synthetic height returns; no update() calls'},
    initial,initialQueryEvents,initialLayerEvents,restart,smoothing,bridgeTest,
    bridgeImprove,...(bridgeSweep?{bridgeSweep}:{}),...(fine?{fine}:{}),...(inputs?{inputs}:{}),...(attributes?{attributes}:{}),
    ...(headLayerChecks?{headLayerChecks}:{}),
    ...(options.progress?{observerInputs}:{}),trace};
}

function main(){
  if(process.argv.includes('--road-mesh')){
    require('./road-mesh-reference.cjs').run(createReference);return;
  }
  if(process.argv.includes('--post-fine-restart')){
    const r=createReference('style3','a',true,'archive-heightfield',{
      updates:3833,full:true,fault:'post-fine-restart',faultOrdinal:3800});
    const output={schema:1,version:r.version,style:r.style,seed:r.seed,profile:r.profile,
      effectiveMidlineConfig:r.effectiveMidlineConfig,sourceHashes:r.sourceHashes,sections:r.sections,
      syntheticCheckpoint:{ordinal:3800,note:'Observer at tail plus injected tracker violation forces original restart after natural fine sampling'},
      cases:r.trace.slice(3799).map(s=>({ordinal:s.ordinal,head:s.state.head.i,tail:s.state.tail.i,
        fineTail:s.fineOutput.fineTail,markingsNode:s.fineOutput.markingsNode,markingsNext:s.fineOutput.markingsNext}))};
    const file=path.join(fixtureDir,'hills-midline-post-fine-restart.json'),json=JSON.stringify(output,null,2)+'\n';
    if(process.argv.includes('--write'))fs.writeFileSync(file,json);
    else assert.equal(fs.readFileSync(file,'utf8'),json,'Post-fine restart reference drift');
    console.log(JSON.stringify({cases:output.cases.length,restart:output.cases[1]}));return;
  }
  if(process.argv.includes('--head-layers')){
    const r=createReference('style3','a',true,'archive-heightfield',{updates:0,full:true,headLayerChecks:true});
    const output={schema:1,version:r.version,style:r.style,seed:r.seed,profile:r.profile,
      effectiveMidlineConfig:r.effectiveMidlineConfig,sourceHashes:r.sourceHashes,sections:r.sections,cases:r.headLayerChecks};
    const file=path.join(fixtureDir,'hills-midline-head-layers.json'),json=JSON.stringify(output,null,2)+'\n';
    if(process.argv.includes('--write'))fs.writeFileSync(file,json);
    else assert.equal(fs.readFileSync(file,'utf8'),json,'Head layer reference drift');
    console.log(JSON.stringify({cases:output.cases.length,final:output.cases.at(-1).layers}));return;
  }
  if(process.argv.includes('--joint')){
    const style=process.argv.includes('--style0')?'style0':'style3';
    const progress=process.argv.includes('--progress');
    const output=createReference(style,'a',true,'archive-heightfield',{
      updates:progress?3300:style==='style0'?2700:4100,full:true,progress});
    const file=path.join(fixtureDir,`hills-midline-joint-${progress?'progress-':''}${style}.json`),json=JSON.stringify(output)+'\n';
    if(process.argv.includes('--write'))fs.writeFileSync(file,json);
    else assert.equal(fs.readFileSync(file,'utf8'),json,'Joint reference drift');
    console.log(JSON.stringify({style,updates:output.trace.length,tail:output.trace.at(-1).state.tail.i,
      fineTail:output.trace.at(-1).fineOutput.fineTail}));return;
  }
  if(process.argv.includes('--attributes')){
    const output={schema:1,version:'1.0.2',cases:['style0','style3'].map(style=>{
      const r=createReference(style,'a',true,'attributes');return {sourceHashes:r.sourceHashes,sections:r.sections,
        profile:r.profile,effectiveMidlineConfig:r.effectiveMidlineConfig,...r.attributes};})};
    const file=path.join(fixtureDir,'hills-midline-attributes.json'),json=JSON.stringify(output,null,2)+'\n';
    if(process.argv.includes('--write'))fs.writeFileSync(file,json);
    else assert.equal(fs.readFileSync(file,'utf8'),json,'Attributes reference drift');
    console.log(JSON.stringify({styles:output.cases.length,stages:output.cases.map(c=>c.stages.length)}));return;
  }
  if(process.argv.includes('--retry-layer')){
    const output=createReference('style3','a',true,'retry-low',{lowRetries:23});
    output.syntheticCheckpoint={lowElevationQueries:23,note:'Original initialise crosses >20-retry forced layer change'};
    const file=path.join(fixtureDir,'hills-midline-init-retry-layer.json'),json=JSON.stringify(output,null,2)+'\n';
    if(process.argv.includes('--write'))fs.writeFileSync(file,json);
    else assert.equal(fs.readFileSync(file,'utf8'),json,'Retry layer reference drift');
    console.log(JSON.stringify({queries:output.initialQueryEvents.length,layers:output.initialLayerEvents}));
    return;
  }
  if(process.argv.includes('--fault-transitions')){
    const cases=['tracker-0','tracker-1','tracker-2','tracker-3','priority','overflow','overflow-repeat',
      'restart','layer-revert','bridge-overrun'];
    for(const fault of cases){
      const options={updates:fault==='restart'?37:97,transitions:true,fault};
      const output=createReference('style3','a',true,'archive-heightfield',options);
      const plain=createReference('style3','a',false,'archive-heightfield',options);
      const comparable=r=>JSON.parse(JSON.stringify(r.trace.map(s=>({
        state:{...s.state,queryCount:0},transition:s.transition}))));
      assert.deepEqual(comparable(output),comparable(plain),'Fault observation changed '+fault);
      output.syntheticCheckpoint={fault,ordinal:fault==='restart'?4:64,
        note:'Reach normal archive trajectory, inject labeled branch state, then execute original update and 32 recovery updates'};
      const file=path.join(fixtureDir,`hills-midline-fault-${fault}.json`),json=JSON.stringify(output,null,2)+'\n';
      if(process.argv.includes('--write'))fs.writeFileSync(file,json);
      else assert.equal(fs.readFileSync(file,'utf8'),json,'Fault transition reference drift '+fault);
      console.log(JSON.stringify({fault,updates:output.trace.length,
        event:output.trace[output.syntheticCheckpoint.ordinal].transition}));
    }
    return;
  }
  if(process.argv.includes('--numeric-feedback')){
    const input=base.readArchive(['Normals']);
    const sourceHashes=base.verifyHashes(input.sources,['Dev','Hills','Settings','Rand','Normals']);
    const fixture=JSON.parse(fs.readFileSync(path.join(fixtureDir,'hills-midline-transitions.json'),'utf8'));
    const angles=[0,-0,Math.PI/2,-Math.PI/2,Math.PI,-Math.PI,2*Math.PI,
      -1.2449722442010414,-1.1471050694064182];
    for(let i=0;i<=1024;i++)angles.push(-Math.PI*2+i*(Math.PI*4/1024));
    for(const step of fixture.trace)angles.push(step.state.tail.a,
      step.state.tail.a-0.463,step.state.tail.a+0.463);
    const bits=x=>{const b=Buffer.alloc(8);b.writeDoubleBE(x);return b.toString('hex');};
    const cases=angles.map(x=>({x:bits(x),sin:bits(Math.sin(x)),cos:bits(Math.cos(x)),
      atan2:bits(Math.atan2(Math.sin(x),Math.cos(x))),acos:bits(Math.acos(Math.cos(x)))}));
    const output={schema:1,version:input.version,sourceHashes,
      provenance:'Math intrinsics of the same Node/V8 runtime executing the pinned archive oracle; not an Electron runtime parity claim',
      node:process.version,v8:process.versions.v8,cases};
    const file=path.join(fixtureDir,'hills-midline-numeric-feedback.json'),json=JSON.stringify(output,null,2)+'\n';
    if(process.argv.includes('--write'))fs.writeFileSync(file,json);
    else assert.equal(fs.readFileSync(file,'utf8'),json,'Numeric feedback reference drift');
    console.log(JSON.stringify({cases:cases.length,node:process.version,v8:process.versions.v8}));
    return;
  }
  if(process.argv.includes('--inputs')){
    const cases=['style0','style3'].map(style=>createReference(style,'a',true,'inputs').inputs);
    const reference=createReference();
    const output={schema:1,version:'1.0.2',sourceHashes:reference.sourceHashes,
      sections:reference.sections,cases};
    const file=path.join(fixtureDir,'hills-midline-inputs.json'),json=JSON.stringify(output,null,2)+'\n';
    if(process.argv.includes('--write'))fs.writeFileSync(file,json);
    else assert.equal(fs.readFileSync(file,'utf8'),json,'Inputs reference drift');
    console.log(JSON.stringify({styles:cases.length,shadowCases:cases.map(c=>c.shadowCases.length)}));
    return;
  }
  if(process.argv.includes('--fine-matrix')){
    const shapes=[
      {name:'straight',points:[[0,0,0],[10,0,0],[20,0,0],[30,0,0],[40,0,0]]},
      {name:'hill-curve',points:[[0,0,0],[10,2,0],[18,5,6],[25,3,15],[33,2,25]]},
      {name:'coincident',points:[[0,0,0],[0,0,0],[10,0,0],[10,0,0],[20,0,2]]},
      {name:'near-coincident',points:[[0,0,0],[1e-10,0,0],[10,2,0],[20,0,0],[30,0,0]]},
      {name:'threshold-below',points:[[0,0,0],[10,0,0],[20,0,0.2828],[30,0,0.8484],[40,0,1.6968]]},
      {name:'threshold-above',points:[[0,0,0],[10,0,0],[20,0,0.283],[30,0,0.849],[40,0,1.698]]},
    ];
    const cases=[];
    for(const shape of shapes) for(const minimumSegments of [1,2,4]) for(const heightFactor of [3,50]){
      const options={...shape,minimumSegments,heightFactor};
      const result=createReference('style3','a',true,'fine',options);
      const plain=createReference('style3','a',false,'fine',options);
      assert.equal(JSON.stringify(result.fine),JSON.stringify(plain.fine),'Fine observation changed');
      cases.push({name:shape.name,...result.fine});
    }
    const reference=createReference();
    const output={schema:1,version:'1.0.2',sourceHashes:reference.sourceHashes,
      sections:reference.sections,cases};
    const file=path.join(fixtureDir,'hills-midline-fine.json'),json=JSON.stringify(output,null,2)+'\n';
    if(process.argv.includes('--write'))fs.writeFileSync(file,json);
    else assert.equal(fs.readFileSync(file,'utf8'),json,'Fine reference drift');
    console.log(JSON.stringify({cases:cases.length,segments:cases.map(c=>c.stages[0].chain[0].fs)}));
    return;
  }
  if(process.argv.includes('--transitions')){
    const arg=name=>{const i=process.argv.indexOf(name);return i<0?null:process.argv[i+1];};
    const seed=arg('--transition-seed')??'a';
    assert(/^[a-z]+$/.test(seed),'Transition seed must be lowercase letters');
    const updates=Number(arg('--transition-updates')??600);
    assert(Number.isInteger(updates)&&updates>0&&updates<=1200,'Transition update limit is 1200');
    const options={updates,transitions:true};
    const result=createReference('style3',seed,true,'archive-heightfield',options);
    const plain=createReference('style3',seed,false,'archive-heightfield',options);
    const strip=r=>JSON.parse(JSON.stringify(r.trace.map(s=>({
      state:{...s.state,queryCount:0},transition:s.transition}))));
    assert.deepEqual(strip(result),strip(plain),'Transition observation changed');
    const file=path.join(fixtureDir,seed==='a'?'hills-midline-transitions.json':`hills-midline-transitions-${seed}.json`);
    const json=JSON.stringify(result,null,2)+'\n';
    if(process.argv.includes('--write'))fs.writeFileSync(file,json);
    else assert.equal(fs.readFileSync(file,'utf8'),json,'Transition reference drift');
    console.log(JSON.stringify({seed,updates:result.trace.length,
      reverts:result.trace.filter(s=>!s.didExtend).map(s=>s.ordinal),
      bridgingUpdates:result.trace.filter(s=>s.state.bridge.active).length}));
    return;
  }
  if(process.argv.includes('--bridge-sweep')){
    const result=createReference('style3','a',true,'bridge-sweep');
    const plain=createReference('style3','a',false,'bridge-sweep');
    const {queryEvents:ignored,...observed}=result.bridgeSweep;
    const {queryEvents:ignoredPlain,...unobserved}=plain.bridgeSweep;
    assert.deepEqual(JSON.parse(JSON.stringify({...observed,calls:observed.calls.map(({queryEvents,...c})=>c)})),
      JSON.parse(JSON.stringify({...unobserved,calls:unobserved.calls.map(({queryEvents,...c})=>c)})),
      'Bridge sweep observation changed');
    const output={schema:1,version:'1.0.2',sourceHashes:result.sourceHashes,...result.bridgeSweep};
    const file=path.join(fixtureDir,'hills-midline-bridge-sweep.json');
    const json=JSON.stringify(output,null,2)+'\n';
    if(process.argv.includes('--write'))fs.writeFileSync(file,json);
    else assert.equal(fs.readFileSync(file,'utf8'),json,'Bridge sweep reference drift');
    console.log(JSON.stringify({calls:output.calls.length,queries:output.queryEvents.length,
      quality:output.candidate.quality,didExtend:output.didExtend}));
    return;
  }
  if(process.argv.includes('--bridge-improve-matrix')){
    const cases=[[10,false],[10,true],[1,false]].map(([startIndex,highStop])=>{
      const result=createReference('style3','a',true,'bridge-improve',{startIndex,highStop});
      const plain=createReference('style3','a',false,'bridge-improve',{startIndex,highStop});
      assert.deepEqual(JSON.parse(JSON.stringify(result.bridgeImprove)),
        JSON.parse(JSON.stringify(plain.bridgeImprove)),'Bridge improvement observation changed');
      return result.bridgeImprove;
    });
    const output={schema:1,version:'1.0.2',cases};
    const file=path.join(fixtureDir,'hills-midline-bridge-improve.json');
    const json=JSON.stringify(output,null,2)+'\n';
    if(process.argv.includes('--write'))fs.writeFileSync(file,json);
    else assert.equal(fs.readFileSync(file,'utf8'),json,'Bridge improvement reference drift');
    console.log(JSON.stringify({schema:1,cases:cases.map(c=>c.resultIndex)}));
    return;
  }
  if(process.argv.includes('--bridge-matrix')){
    const scenarios=['bridge-good','bridge-mid-high','bridge-side-high','bridge-short',
      'bridge-anti-near','bridge-emergency'];
    const cases=scenarios.map(scenario=>{
      const result=createReference('style3','a',true,scenario);
      const plain=createReference('style3','a',false,scenario);
      const {queryEvents:ignored,...observed}=result.bridgeTest;
      const {queryEvents:ignoredPlain,...unobserved}=plain.bridgeTest;
      assert.deepEqual(JSON.parse(JSON.stringify(observed)),
        JSON.parse(JSON.stringify(unobserved)),scenario+' observation');
      return {scenario,sourceHashes:result.sourceHashes,sections:result.sections,
        ...result.bridgeTest};
    });
    const output={schema:1,version:'1.0.2',cases};
    const file=path.join(fixtureDir,'hills-midline-bridge.json');
    const json=JSON.stringify(output,null,2)+'\n';
    if(process.argv.includes('--write'))fs.writeFileSync(file,json);
    else assert.equal(fs.readFileSync(file,'utf8'),json,'Bridge reference drift');
    console.log(JSON.stringify({schema:1,cases:cases.length,sourceHashes:cases[0].sourceHashes}));
    return;
  }
  if(process.argv.includes('--steering-matrix')){
    const scenarios=['steering-flat','steering-ramp-x','steering-ramp-z',
      'steering-water','steering-transition'];
    const cases=scenarios.map(scenario=>{
      const result=createReference('style3','a',true,scenario);
      const plain=createReference('style3','a',false,scenario);
      const comparable=({queryCount,...state})=>JSON.parse(JSON.stringify(state));
      assert.deepEqual(comparable(plain.initial),comparable(result.initial),scenario+' initial');
      assert.deepEqual(comparable(plain.trace[0].state),comparable(result.trace[0].state),
        scenario+' update');
      return {scenario,sourceHashes:result.sourceHashes,initial:result.initial,
        initialQueryEvents:result.initialQueryEvents,step:result.trace[0]};
    });
    const output={schema:1,version:'1.0.2',cases};
    const file=path.join(fixtureDir,'hills-midline-steering.json');
    const json=JSON.stringify(output,null,2)+'\n';
    if(process.argv.includes('--write'))fs.writeFileSync(file,json);
    else assert.equal(fs.readFileSync(file,'utf8'),json,'Steering reference drift');
    console.log(JSON.stringify({schema:1,cases:cases.length,sourceHashes:cases[0].sourceHashes}));
    return;
  }
  if(process.argv.includes('--smoothing-matrix')){
    const cases=[
      ['style3',1,-1],['style0',1,-1],['style3',0,-1],
      ['style3',1,0],['style3',1,3],['style3',1,6],
    ].map(([style,strength,bridgeIndex])=>{
      const result=createReference(style,'a',true,'smoothing',{strength,bridgeIndex});
      const plain=createReference(style,'a',false,'smoothing',{strength,bridgeIndex});
      assert.deepEqual(JSON.parse(JSON.stringify(plain.smoothing)),
        JSON.parse(JSON.stringify(result.smoothing)),'Observation changed smoothing');
      return {style,sourceHashes:result.sourceHashes,sections:result.sections,...result.smoothing};
    });
    const dev=base.readArchive().sources.Dev.toString('utf8');
    const rollbackSource=checkedSection(dev,'if(i.tail.i<this.tailHeightmap.prevSwitchNode',
      ';if(this.bridging||');
    const before=cases[0].stages.at(-1).nodes;
    const rollbackCases=[10,5,19].map(tailIndex=>{
      const nodes=before.map(n=>({i:n.i,p:{x:n.p[0],y:n.p[1],z:n.p[2]},h:n.h,prev:null,next:null}));
      for(let j=0;j<nodes.length;j++){
        nodes[j].prev=nodes[j-1]??null;nodes[j].next=nodes[j+1]??null;
      }
      const state={smoothWindowHead:nodes[8],tailHeightmap:{prevSwitchNode:-100},
        undoUpdateTailHeightmap:()=>{throw Error('Unexpected heightmap boundary')}};
      const fn=vm.runInNewContext('(function(){'+rollbackSource.source+';})',
        {i:{tail:nodes[tailIndex]},e:{},z:7},{timeout:1000});
      fn.call(state);
      return {tailIndex,windowHeadIndex:8,window:7,before:before.map(n=>({i:n.i,p:n.p,h:n.h})),
        afterHeadIndex:state.smoothWindowHead.i,
        afterY:nodes.map(n=>n.p.y),
        writtenIndices:nodes.filter((n,j)=>n.p.y!==before[j].p[1]).map(n=>n.i)};
    });
    const output={schema:1,version:'1.0.2',rollbackSectionSha256:rollbackSource.sha256,
      cases,rollbackCases};
    const file=path.join(fixtureDir,'hills-midline-smoothing.json');
    const json=JSON.stringify(output,null,2)+'\n';
    if(process.argv.includes('--write'))fs.writeFileSync(file,json);
    else assert.equal(fs.readFileSync(file,'utf8'),json,'Smoothing reference drift');
    console.log(JSON.stringify({schema:1,cases:cases.length,sourceHashes:cases[0].sourceHashes}));
    return;
  }
  const style=process.argv.includes('--style0')?'style0':'style3',seed='a';
  const scenarioIndex=process.argv.indexOf('--scenario');
  const scenario=scenarioIndex<0?'archive-heightfield':process.argv[scenarioIndex+1];
  const fixturePath=path.join(fixtureDir,scenario==='archive-heightfield'
    ?`hills-midline-entry-${style}.json`:`hills-midline-init-${scenario}.json`);
  const result=createReference(style,seed,true,scenario);
  const plain=createReference(style,seed,false,scenario);
  const comparable=({queryCount,...state})=>JSON.parse(JSON.stringify(state));
  assert.deepEqual(comparable(plain.initial),comparable(result.initial),'Observation changed initialization');
  if(result.restart)assert.deepEqual(comparable(plain.restart.state),comparable(result.restart.state),
    'Observation changed restart');
  for(let i=0;i<result.trace.length;i++){
    const a=plain.trace[i].state,b=result.trace[i].state;
    assert.deepEqual(comparable(a),comparable(b),'Observation changed update '+i);
  }
  if(process.argv.includes('--self-test')){
    assert.throws(()=>base.verifyHashes({...base.readArchive(['Normals']).sources,Normals:Buffer.from('corrupt')},
      ['Normals']),/hash mismatch/);
    assert.throws(()=>base.section('a a b','a','b'),/Non-unique/);
  }
  const json=JSON.stringify(result,null,2)+'\n';
  if(process.argv.includes('--write')){
    fs.mkdirSync(path.dirname(fixturePath),{recursive:true});
    fs.writeFileSync(fixturePath,json);
  }else assert.equal(fs.readFileSync(fixturePath,'utf8'),json,'Reference drift; inspect before --write');
  console.log(JSON.stringify({schema:1,style,seed,scenario,updates:result.trace.length,
    sourceHashes:result.sourceHashes,observedQueries:result.trace.at(-1)?.state.queryCount??
      result.restart?.state.queryCount??result.initial.queryCount,
    selfTest:process.argv.includes('--self-test')}));
}
module.exports={createReference,classSection,checkedSection,injectFault};
if(require.main===module)main();
