// Hash-pinned Ll/i_ oracle. SourceAtlas is evidence, never executable input.
const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm'),assert=require('node:assert/strict');
const base=require('./heightfield-reference.cjs'),mid=require('./midline-reference.cjs'),road=require('./road-mesh-reference.cjs');
const crypto=require('node:crypto');const sha=s=>crypto.createHash('sha256').update(s).digest('hex');
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
  ';return {evaluate:Ll,curve:gr,noise:(x,z)=>[wl,jt,bl,Ml].map(n=>n.getXZ(x,z)),config:{N,F,We,Ve,me,O,re,Lt,yt}};})()',ctx);
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
if(require.main===module){
 if(process.argv.includes('--self-test')){const a=base.readArchive(['Normals']);base.verifyHashes(a.sources,['Dev','Hills','Settings','Rand','Normals']);const bad={...a.sources,Hills:Buffer.from('bad')};assert.throws(()=>base.verifyHashes(bad));for(const s of ['none','start end start','end start'])assert.throws(()=>base.section(s,'start','end'));console.log('blend extraction rejection self-test PASS');process.exit(0);}
 const file=path.join(__dirname,'Fixtures/RoadBlend/hills-road-blend.json');const value=build(),json=JSON.stringify(value)+'\n';
 if(process.argv.includes('--write')){fs.mkdirSync(path.dirname(file),{recursive:true});fs.writeFileSync(file,json);}else assert.equal(fs.readFileSync(file,'utf8'),json,'Road blend reference drift');
 console.log(JSON.stringify({cases:value.cases.length,queries:value.cases.reduce((s,c)=>s+c.queries.length,0),sections:value.sections}));}
module.exports={install,query,link,encode,fields};
