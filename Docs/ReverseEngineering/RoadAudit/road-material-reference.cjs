// Read-only by default. Surface arithmetic oracle, not a replacement WebGL renderer.
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),assert=require('node:assert/strict');
const base=require('./heightfield-reference.cjs');
const sha=s=>crypto.createHash('sha256').update(s).digest('hex');
function build(){
 const a=base.readArchive(['Normals']);const hashes=base.verifyHashes(a.sources,['Dev','Hills','Settings','Rand','Normals']);
 const h=a.sources.Hills.toString(),n=a.sources.Normals.toString();
 const sections={surface:base.section(h,'ue={pars_vert:', ';class Nl'),binding:base.section(h,'let r;Oe?r=He?', ',v.radiance.value='),
 material:base.section(h,'const c=new M({map:s(Z)', ';const cs='),fresnel:base.section(h,'es=`','`,ue='),
 shadow:base.section(n,'lx=`','`,Nm=')};
 assert(sections.material.includes('alphaTest:.75'));assert(sections.surface.includes('ceil(max(wSolidLeft, wSolidRight))'));
 const maps=Array.from(h.matchAll(/road:\{base:[^}]+\},roadSingle:\{[^}]+\},roadSingleWide:\{[^}]+\}/g),m=>m[0]);assert.equal(maps.length,4);
 const values={base:[.12,.18,.24,.5],dash:[.8,.1,.2,.7],solid:[.1,.8,.2,.6],left:[.1,.2,.8,.9],right:[.7,.6,.1,.8],mergeLeft:[.5,.2,.7,.65],mergeRight:[.2,.7,.6,.55],overlay:[.3,.4,.1,.8]};
 function oracle(c){
  const sm=x=>{x=Math.max(0,Math.min(1,(x-.49)/.02));return x*x*(3-2*x);};
  const mix=(a,b,w)=>a.map((v,i)=>v+(b[i]-v)*w);
  let b=values.base.slice(),p=values.dash.slice(),s=sm(c.channels[2]),l=sm(c.channels[3]),r=sm(c.channels[4]);
  let ml=sm(c.channels[5])*(1-Math.ceil(Math.max(l,r))),mr=sm(c.channels[6])*(1-Math.ceil(Math.max(l,r)));
  if(c.paint){if(ml+mr>0){p=mix(p,values.mergeLeft,ml);p=mix(p,values.mergeRight,mr);}
   if(r+l>0){p=mix(p,values.right,r);p=mix(p,values.left,l);p=mix(p,values.solid,s);}b=mix(b,p,p[3]).slice(0,3).concat(b[3]);}
  if(c.overlay){const w=Math.max(0,Math.min(1,c.channels[1]*c.shadowFactor*3));b=mix(b,values.overlay,w*values.overlay[3]).slice(0,3).concat(b[3]);}
  b[3]=Math.max(c.channels[0],values.base[3]);return {rgba:b,discard:b[3]<.75};
 }
 const cases=[['dash',[1,0,0,0,0,0,0]],['solid-only-no-side',[1,0,1,0,0,0,0]],['left',[1,0,0,1,0,0,0]],['right',[1,0,0,0,1,0,0]],['solid',[1,0,1,1,1,0,0]],['merge-left',[1,0,0,0,0,1,0]],['merge-both',[1,0,0,0,0,1,1]],['priority',[1,0,0,1,0,1,1]],['transition',[1,0,.5,.5,.5,.5,.5]],['cutout',[0,0,0,0,0,0,0]],['overlay',[1,.2,0,0,0,0,0]],['single',[1,1,0,0,0,0,0]]].map(([id,channels])=>{const c={id,channels,paint:id!=='single',overlay:id==='overlay',shadowFactor:.9};return {...c,...oracle(c)};});
 let line=1;const evidence=['# Raw material excerpts. L labels are THIS excerpt sequence, not historical prettier line numbers.'];
 const identities=Object.fromEntries(Object.entries(sections).map(([k,v])=>{
  const module=k==='shadow'?'Normals':'Hills',source=k==='shadow'?n:h,lines=v.split('\n'),firstLine=line;
  evidence.push('# '+k+' module='+module+' rawUtf16Offset='+source.indexOf(v));
  for(const text of lines)evidence.push('L'+String(line++).padStart(5,'0')+' '+text);
  return [k,{sha256:sha(v),module,rawUtf16Offset:source.indexOf(v),firstLine,lastLine:line-1}];
 }));
 return {schema:1,version:a.version,hashes,sections:identities,maps,values,cases,evidence:evidence.join('\n')+'\n'};
}
if(require.main===module){const out=path.join(__dirname,'Fixtures/RoadMesh/hills-road-material.json'),data=build(),excerpt=path.join(__dirname,'../SceneArchitecture/SourceAtlas/RoadMaterial.source.txt');
 const evidence=data.evidence;delete data.evidence;const json=JSON.stringify(data,null,2)+'\n';
 if(process.argv.includes('--write')){fs.writeFileSync(out,json);fs.writeFileSync(excerpt,evidence);}else {assert.equal(fs.readFileSync(out,'utf8'),json,'Material source/reference changed');assert.equal(fs.readFileSync(excerpt,'utf8'),evidence,'Material excerpt changed');}
 console.log(JSON.stringify({version:data.version,cases:data.cases.length,hashes:data.hashes,result:'PASS'}));}
module.exports={build};
