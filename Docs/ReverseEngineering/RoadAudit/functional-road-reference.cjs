const fs=require('node:fs'),path=require('node:path'),zlib=require('node:zlib'),assert=require('node:assert/strict');
const {build}=require('./streaming-reference.cjs');
const dir=path.join(__dirname,'Fixtures/Streaming/Functional');
function specs(){
 const cases=[];for(let lane=0;lane<5;lane++)for(let detail=0;detail<6;detail++)cases.push({id:`a-${lane}-${detail}`,seed:'a',lane,detail,nodes:detail===2?6000:500,bridges:detail===2});
 for(const [i,seed] of ['1337','0','-17','丘陵'].entries()){
  cases.push({id:`seed${i}-4-2`,seed,lane:4,detail:2,nodes:500});
  cases.push({id:`seed${i}-1-0`,seed,lane:1,detail:0,nodes:500});
  cases.push({id:`seed${i}-0-5`,seed,lane:0,detail:5,nodes:500});
 }return cases;
}
if(require.main===module){
 fs.mkdirSync(dir,{recursive:true});const selected=process.argv.find(x=>x.startsWith('--id='))?.slice(5);
 for(const spec of specs().filter(s=>!selected||s.id===selected)){
  const v=build(16,spec);delete v.spatialProbe;const json=Buffer.from(JSON.stringify(v)+'\n'),file=path.join(dir,spec.id+'.json.gz');
  if(process.argv.includes('--write'))fs.writeFileSync(file,zlib.gzipSync(json));else assert.deepEqual(zlib.gunzipSync(fs.readFileSync(file)),json);
  console.log(JSON.stringify({id:spec.id,ticks:v.totalTicks,publishes:v.events.length,bridges:v.bridges.length,bridgeQueries:v.bridges.reduce((s,c)=>s+c.queries.length,0)}));
 }
 const index=JSON.stringify({schema:1,cases:specs()})+'\n';if(process.argv.includes('--write'))fs.writeFileSync(path.join(dir,'index.json'),index);
}
module.exports={specs};
