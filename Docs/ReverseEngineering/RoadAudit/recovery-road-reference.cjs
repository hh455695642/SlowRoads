const fs=require('node:fs'),path=require('node:path'),zlib=require('node:zlib'),assert=require('node:assert/strict');
const {build}=require('./streaming-reference.cjs');const dir=path.join(__dirname,'Fixtures/Streaming/Recovery');
const cases=[];for(const [lane,detail] of [[4,2],[1,0]])for(const fault of ['tracker-0','overflow-repeat','layer-revert','bridge-overrun','restart-boundary'])
 cases.push({id:`${fault}-${lane}-${detail}`,seed:'a',lane,detail,nodes:3200,fault,faultOrdinal:512,compatibleRestart:true});
if(require.main===module){fs.mkdirSync(dir,{recursive:true});const selected=process.argv.find(v=>v.startsWith('--id='))?.slice(5);
 const native=build(16,{seed:'a',lane:4,detail:2,nodes:3200,fault:'restart-boundary',captureReferenceError:true});assert(native.referenceError,'Native restart error no longer reproduces');const evidence=JSON.stringify({schema:1,hashes:native.hashes,sections:native.sections,fault:native.fault,compatibleRestart:false,invalidations:native.invalidations,error:native.referenceError})+'\n';if(process.argv.includes('--write'))fs.writeFileSync(path.join(dir,'native-restart-error.json'),evidence);else assert.equal(fs.readFileSync(path.join(dir,'native-restart-error.json'),'utf8'),evidence);
 for(const c of cases.filter(c=>!selected||c.id===selected)){const v=build(16,c);delete v.spatialProbe;const bytes=Buffer.from(JSON.stringify(v)+'\n'),file=path.join(dir,c.id+'.json.gz');
  if(process.argv.includes('--write'))fs.writeFileSync(file,zlib.gzipSync(bytes));else assert.deepEqual(zlib.gunzipSync(fs.readFileSync(file)),bytes);
  console.log(JSON.stringify({id:c.id,publishes:v.events.length,invalidations:v.invalidations,reverts:v.reverts}));}
 if(process.argv.includes('--write'))fs.writeFileSync(path.join(dir,'index.json'),JSON.stringify({schema:1,cases})+'\n');
}
