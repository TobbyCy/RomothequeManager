/* STAB-03B — in-memory import of a *synthetic* DTO in the existing UI.
 * node prototype/ui11/synthetic-import.test.cjs
 * This is a DOM simulation, not an Edge test or a PS-export parity test. */
"use strict";
const fs=require("node:fs"),path=require("node:path"),vm=require("node:vm"),assert=require("node:assert/strict");
const nodes=new Map(),events={},w={};
const el=id=>{if(!nodes.has(id))nodes.set(id,{innerHTML:"",textContent:"",hidden:true,focus(){},setSelectionRange(){},classList:{toggle(){}},setAttribute(){}});return nodes.get(id)};
const document={getElementById:el,querySelectorAll:()=>[],addEventListener:(k,f)=>events[k]=f};
const context=vm.createContext({window:w,globalThis:w,document,Date,Intl,Map,Set,Number,String,Object,Array,RegExp,Error,console});
for(const script of ["fixtures.js","projection-adapter.js","../../adapters/shared/catalogue-t3-consumer.js","t2-catalogue-model.js","t2-catalogue-fixture.js","app.js"]){
 vm.runInContext(fs.readFileSync(path.join(__dirname,script),"utf8"),context,{filename:script});
}
const demo={};vm.runInNewContext(fs.readFileSync(path.join(__dirname,"t4-catalogue-demo.js"),"utf8"),{window:demo});
const dto=demo.RM_PROJECTION_DATA.catalogue;
const load=(raw,size=raw.length)=>events.change({target:{id:"synthetic-catalogue-import",value:"synthetic.json",files:[{size,text:()=>Promise.resolve(raw)}]}});
const tick=()=>new Promise(resolve=>setImmediate(resolve));
(async()=>{
 events.click({target:{closest:s=>s==="[data-route]"?{dataset:{route:"collection"}}:null}});
 assert.match(el("view").innerHTML,/synthetic-catalogue-import/);
 load(JSON.stringify(dto));await tick();
 assert.match(el("toast").textContent,/JSON synthétique accepté/);
 assert.match(el("view").innerHTML,/Étoile fictive/);
 assert.equal(w.RM_PROJECTION_DATA.catalogue.schemaVersion,"0.2.0-ui-catalogue-candidate");
 const before=w.RM_PROJECTION_DATA;
 load('{"schemaVersion":"unknown"}');await tick();
 assert.match(el("toast").textContent,/refusé/);
 assert.equal(w.RM_PROJECTION_DATA,before,"failed import must preserve previous accepted view");
 load(JSON.stringify(dto).replace("Étoile fictive","\uFFFD"));await tick();
 assert.match(el("toast").textContent,/UNICODE_REPLACEMENT_CHARACTER/);
 assert.equal(w.RM_PROJECTION_DATA,before);
 let called=false;
 events.change({target:{id:"synthetic-catalogue-import",files:[{size:6*1024*1024,text:()=>{called=true;return Promise.resolve("{}")}}]}});
 assert.equal(called,false,"oversized input must not be read");
 console.log("STAB03B_SYNTHETIC_IMPORT_PASS checks=9 | Node simulated DOM | browser=NOT_EXECUTED | physical_data=NONE");
})().catch(e=>{console.error(e);process.exitCode=1});
