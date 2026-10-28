// Execute pinned algorithms and configuration literals directly from app.asar.
// node Docs/ReverseEngineering/RoadAudit/heightfield-reference.cjs [--write | --self-test]
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const vm=require('node:vm'),assert=require('node:assert/strict');
const root=path.resolve(__dirname,'../../..');
const manifest=JSON.parse(fs.readFileSync(path.join(root,'Docs/ReverseEngineering/SceneArchitecture/source-manifest.json'),'utf8'));
const out=path.join(__dirname,'Fixtures/Heightfield/hills-heightfield.json');
const sha=bytes=>crypto.createHash('sha256').update(bytes).digest('hex');

function readArchive(){
  const fd=fs.openSync(path.join(root,'Slow Roads/resources/app.asar'),'r');
  const read=(offset,length)=>{
    const bytes=Buffer.alloc(length);let done=0;
    while(done<length){
      const count=fs.readSync(fd,bytes,done,length-done,offset+done);
      assert(count>0,'Unexpected archive EOF');done+=count;
    }
    return bytes;
  };
  try{
    const headerLength=read(0,8).readUInt32LE(4),hb=read(8,headerLength);
    const header=JSON.parse(hb.subarray(8,8+hb.readUInt32LE(4)));
    const file=name=>{
      let entry=header;
      for(const part of name.split('/')){
        assert(entry.files&&entry.files[part],'Missing archive entry '+name);
        entry=entry.files[part];
      }
      assert(!entry.unpacked,'Reference must reside inside archive');
      return read(8+headerLength+Number(entry.offset),entry.size);
    };
    const sources={};
    for(const item of manifest.filter(x=>['Dev','Hills','Settings','Rand'].includes(x.key)))
      sources[item.key]=file('build/_app/immutable/chunks/'+item.module+'.js');
    const calls=[];
    const chunks=header.files.build.files._app.files.immutable.files.chunks.files;
    for(const name of Object.keys(chunks).filter(x=>x.endsWith('.js')))
      if(/\.\s*getXZLayer\s*\(/.test(file('build/_app/immutable/chunks/'+name).toString('utf8')))calls.push(name);
    return {sources,version:JSON.parse(file('package.json')).version,calls};
  }finally{fs.closeSync(fd);}
}
function verifyHashes(sources){
  const hashes={};
  for(const key of ['Dev','Hills','Settings','Rand']){
    const listed=manifest.find(x=>x.key===key);
    assert(listed&&sources[key],'Missing reference module '+key);
    hashes[key]=sha(sources[key]);
    assert.equal(hashes[key],listed.rawSHA256,key+' archive hash mismatch');
  }
  return hashes;
}
function uniqueIndex(text,marker){
  const index=text.indexOf(marker);
  assert(index>=0&&text.indexOf(marker,index+1)<0,'Non-unique/missing marker: '+marker);
  return index;
}
function section(text,begin,end){
  const first=uniqueIndex(text,begin),last=uniqueIndex(text,end);
  assert(last>first,'Reversed extraction markers');
  return text.slice(first,last);
}
// Only pinned configuration literals use this scanner; skip strings before balancing.
function literal(text,prefix){
  const start=uniqueIndex(text,prefix);assert.equal(text[start],'{');
  let depth=0,quote=null;
  for(let i=start;i<text.length;i++){
    const c=text[i];
    if(quote){if(c==='\\')i++;else if(c===quote)quote=null;}
    else if(c==='"'||c==="'")quote=c;
    else if(c==='{')depth++;
    else if(c==='}'&&--depth===0)
      return vm.runInNewContext('('+text.slice(start,i+1)+')',{},{timeout:1000});
  }
  throw new Error('Unclosed configuration literal');
}
function oracle(input){
  const hashes=verifyHashes(input.sources);
  assert.equal(input.version,'1.0.2','Wrong reference application version');
  const dev=input.sources.Dev.toString('utf8'),hills=input.sources.Hills.toString('utf8');
  const settings=input.sources.Settings.toString('utf8');
  const pieces=[
    section(dev,'var Y=','import{'),
    section(dev,'const p=','const Z=le(ge)')+'const Z=le(ge);',
    'const '+section(hills,'e={id:0},w={},Be={};','export{'),
  ];
  const context=vm.createContext({window:{}});
  vm.runInContext(pieces[0]+pieces[1]+'\nwindow.alea=Z;globalThis.Alea=Z;',context,{timeout:1000});
  vm.runInContext('const T=n,xr=oe;\n'+pieces[2]+'\nglobalThis.Heightfield=Pl;',context,{timeout:1000});
  assert(settings.includes('aX as i8,aY as e9'),'Heightmap import aliases changed');
  assert(settings.includes('let d={seed:I0.seed,...i8,maxCached:e9}'),'Config merge changed');
  assert(hills.includes('ra=l.heightmap'),'Road-style selection changed');
  const caches=Array.from(hills.matchAll(/heightmapMaxCached:(\d+)/g),x=>Number(x[1]));
  assert.deepEqual(caches,[12,16,20,28,40,40]);
  return {context,hashes,pieces,caches};
}
function buildReference(input){
  const ref=oracle(input),context=ref.context,hills=input.sources.Hills.toString('utf8');
  const styles=[
    {id:'style0',kind:'original-style',source:'Hills L01575-L01587; detail 2 L01810',
      config:{...literal(hills,'{scale:80,offset:90,layerResolutions:'),maxCached:ref.caches[2]}},
    {id:'style3',kind:'original-style',source:'Hills L01698-L01709; detail 2 L01810',
      config:{...literal(hills,'{scale:60,offset:100,layerResolutions:'),maxCached:ref.caches[2]}},
    {id:'default',kind:'original-default',source:'Pl L06516-L06543',config:{}},
    {id:'synthetic-linear',kind:'synthetic',source:'Branch coverage; not a road style',
      config:{scale:75,offset:105,layerResolutions:[3,11,5,27],squared:false,temper:false,tileScaleRandom:.5,maxCached:4}},
    {id:'synthetic-repeated',kind:'synthetic',source:'Repeated-tile branch',
      config:{scale:12,offset:4,repeated:true,tileScaleRandom:.25}},
  ];
  const hex=x=>{
    assert(Number.isInteger(x*4294967296)&&x>=0&&x<1);
    return (x*4294967296).toString(16).padStart(8,'0');
  };
  const random=['a','1337','0','-17','','丘陵','🚗'].map(seed=>{
    const gen=new context.Alea(seed);
    const valuesU32=Array.from({length:1024},()=>hex(gen()));
    const checkpoints=[1,17,257,1024].map(at=>{
      const rand=new context.Alea(seed);for(let i=0;i<at;i++)rand();
      const state=Array.from(rand.exportState());
      const continuationU32=Array.from({length:32},()=>hex(rand()));
      rand.importState(state);
      assert.deepEqual(Array.from({length:32},()=>hex(rand())),continuationU32);
      return {at,stateU32:state.slice(0,3).map(hex),carry:state[3],continuationU32};
    });
    return {seed,valuesU32,checkpoints};
  });
  const coordinates=[[0,0],[1,1],[500,800],[-500,800],[500,-800],[-500,-800],[1500,1500],[250,250],[1000,-1000]];
  for(const k of [-102,-101,-100,-2,-1,0,1,2])for(const epsilon of [-.001,0,.001]){
    const b=k*3000;coordinates.push([b+epsilon,0],[0,b+epsilon]);
    for(const sign of [-1,1])coordinates.push([b+epsilon,sign*b-epsilon]);
  }
  const heightfields=[];
  for(const entry of styles)for(const seed of ['a','1337','0','丘陵']){
    const map=new context.Heightfield({seed,...entry.config});
    const initial=coordinates.map(([x,z])=>({x,z,height:map.getXZ(x,z)}));
    const depths=[0,1,map.config.depth-1,map.config.depth].filter((v,i,a)=>a.indexOf(v)===i)
      .map(depth=>({depth,height:map.getXZ(2999.999,-3000.001,depth)}));
    const tile=map.generateTile(-1,0).map((layer,depth)=>({depth,resolution:layer.length,
      points:layer.flatMap(row=>row.map(p=>({x:p.x,y:p.y,z:p.z,radiusSquared:p.r2})))}));
    map.setLayers([2,3],true);
    const switched=coordinates.slice(0,18).map(([x,z])=>({x,z,height:map.getXZ(x,z)}));
    map.setLayers([0,0],true);
    const restored=coordinates.slice(0,18).map(([x,z])=>({x,z,height:map.getXZ(x,z)}));
    heightfields.push({id:entry.id,kind:entry.kind,source:entry.source,seed,
      config:{...map.config},initial,depths,tile,switched,restored});
  }
  const operations=[
    ['warm',0,0],['warm',-101,-102],['warm',-102,-101],['warm',-100,-100],
    ['warm',-1,-1],['warm',1,1],['warm',-101,-102],['prune',0,0,false],
    ['prune',0,0,true],['warm',-102,-101],['warm',-101,-102],
    ['increment',0,0,false],['warm',-102,1],['increment',1,0,false],
    ['warm',1,-102],['set',2,3,false],['warm',-101,-102],
    ['set',0,0,true],['warm',-101,-102],['increment',0,0,true],
    ['increment',1,0,true],['warm',1,-101],['set',0,0,true],
  ];
  const cacheCases=[true,false].map(spiralise=>{
    const map=new context.Heightfield({seed:'a',...styles[1].config,maxCached:2,spiralise});
    const steps=operations.map(([kind,x,z,force=false])=>{
      if(kind==='warm')map.getTile(x,z);
      else if(kind==='prune')map.pruneCache(force);
      else if(kind==='increment')map.incrementLayer(x,force);
      else map.setLayers([x,z],force);
      const entries=Object.keys(map.cache).flatMap(bx=>Object.keys(map.cache[bx]).map(bz=>({
        x:Number(bx)-100,z:Number(bz)-100,access:map.cacheAccess[bx][bz],layer:map.cacheLayer[bx][bz]})));
      return {kind,x,z,force,count:map.cacheCount,negativeLayer:map.curLayer[0],
        nonNegativeLayer:map.curLayer[1],entries};
    });
    return {config:{...map.config},steps};
  });
  const kernelMap=new context.Heightfield();
  const kernel=[.25-1e-10,.25,.25+1e-10,0].map(x=>({x,z:0,point:{x:0,y:7,z:0,radiusSquared:.0625},
    height:kernelMap.lerpHeight(0,x,0,{x:0,y:7,z:0,r2:.0625})}));
  let emptyError;
  try{new context.Heightfield({layerResolutions:[]});}catch(error){emptyError=error.name;}
  assert.equal(emptyError,'TypeError');
  assert.deepEqual(input.calls,[],'getXZLayer gained a dot-call consumer');
  return {
    schema:2,version:input.version,sourceHashes:ref.hashes,
    execution:{input:'Slow Roads/resources/app.asar',symbols:['Z/Alea','oe','Pl'],
      sourceRanges:['Dev L00001-L00004,L00025-L00216','Hills L06513-L06830'],
      sectionHashes:ref.pieces.map(sha),configMerge:'seed -> roadStyle.heightmap -> graphics.maxCached',
      graphicsDetail:2,absoluteTolerance:1e-9,relativeTolerance:1e-12},
    limitations:{emptyResolutionsError:emptyError,getXZLayerCalls:input.calls.length,
      getXZLayer:'Not implemented: no dot-call consumers in archived chunks; shared temper scratch is order-dependent.'},
    random,heightfields,cacheCases,kernel,
  };
}
function main(){
  const input=readArchive();
  if(process.argv.includes('--self-test')){
    const damaged={...input.sources,Dev:Buffer.from(input.sources.Dev.toString('utf8')+' ')};
    assert.throws(()=>verifyHashes(damaged),/hash mismatch/);
    assert.throws(()=>section('class x{}','missing','class x'),/marker/);
    assert.throws(()=>section('start start end','start','end'),/marker/);
    assert.throws(()=>section('end start','start','end'),/Reversed/);
  }
  const result=buildReference(input),json=JSON.stringify(result,null,2)+'\n';
  if(process.argv.includes('--write')){
    fs.mkdirSync(path.dirname(out),{recursive:true});fs.writeFileSync(out,json);
  }else assert.equal(fs.readFileSync(out,'utf8'),json,'Reference differs; inspect before explicit --write');
  console.log(JSON.stringify({schema:result.schema,archiveHashesVerified:Object.keys(result.sourceHashes),
    randomSeeds:result.random.length,heightfieldCases:result.heightfields.length,
    heightSamples:result.heightfields.reduce((n,x)=>n+x.initial.length+x.depths.length+x.switched.length+x.restored.length,0),
    controlPoints:result.heightfields.reduce((n,x)=>n+x.tile.reduce((s,l)=>s+l.points.length,0),0),
    cacheSteps:result.cacheCases.reduce((n,x)=>n+x.steps.length,0),selfTest:process.argv.includes('--self-test')}));
}
if(require.main===module)main();
module.exports={readArchive,oracle,buildReference,verifyHashes,section};
