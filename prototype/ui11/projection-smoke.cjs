/* Sprint #15 — run with: node prototype/ui11/projection-smoke.cjs
   No browser, PowerShell, filesystem ROM or network. Reads only checked-in synthetic JS. */
"use strict";
const fs=require("node:fs"),vm=require("node:vm"),path=require("node:path"),assert=require("node:assert/strict");
const root=__dirname;
function boot(sample=true){
const nodes={view:{innerHTML:""},breadcrumb:{textContent:""}},events={};
const document={getElementById:k=>nodes[k]||(nodes[k]={innerHTML:"",textContent:""}),querySelectorAll:()=>[],addEventListener:(k,f)=>events[k]=f};
const window={};const context=vm.createContext({window,document,Intl,Date,Map,Set,Object,Array,String,Number,RegExp,Error,console});
for(const file of ["fixtures.js","projection-adapter.js","catalogue-adapter.js",...(sample?["projection-sample.js"]:[]),"app.js"])
vm.runInContext(fs.readFileSync(path.join(root,file),"utf8"),context,{filename:file});
const click=(type,value)=>events.click({target:{closest:k=>k===type?{dataset:value}:null}});
const change=(id,value)=>events.change({target:{id,value}});
return {window,nodes,events,click,change};
}
let n=0;function check(name,fn){fn();console.log("PASS",name);n++}
const {window:w,nodes,click,change}=boot();
check("offline precomputed dashboard",()=>assert.match(nodes.view.innerHTML,/Votre collection/));
check("catalogue game and provenance",()=>{click("[data-route]",{route:"collection"});assert.match(nodes.view.innerHTML,/Explorateurs imaginaires/);click("[data-action]",{action:"catalogue-game:game-demo|GBA"});assert.match(nodes.view.innerHTML,/Variantes connues/);assert.match(nodes.view.innerHTML,/Catalogue synthétique manuel/)});
check("filter retained on open/close",()=>{change("collection-platform","GBA");click("[data-action]",{action:"close-game"});assert.match(nodes.view.innerHTML,/option value="GBA" selected/);click("[data-action]",{action:"catalogue-game:game-demo|GBA"});assert.match(nodes.view.innerHTML,/option value="GBA" selected/)});
check("snapshot link and precomputed comparison",()=>{click("[data-action]",{action:"snapshot:lib-demo:snap-b"});assert.match(nodes.view.innerHTML,/Comparer deux générations/);click("[data-action]",{action:"run-compare"});assert.match(nodes.view.innerHTML,/Observation identique/)});
check("six counters and no business fallback",()=>{const d=w.RM_PROJECTION_DATA.comparisons[0],v=w.RM_PROJECTION.comparison(d,"lib-demo");assert.equal(v.counts.UNCHANGED,1);assert.equal(v.entries.length,1);assert.equal(v.complete,true);assert.equal(v.projectionMode,true)});
check("tampered count is refused",()=>{const d=JSON.parse(JSON.stringify(w.RM_PROJECTION_DATA.comparisons[0]));d.counts.ADDED=1;assert.throws(()=>w.RM_PROJECTION.comparison(d,"lib-demo"),/PROJECTION_COUNT_MISMATCH_ADDED/)});
check("unsupported versions are refused",()=>{const d=JSON.parse(JSON.stringify(w.RM_PROJECTION_DATA.registry));d.schemaVersion="9.9";assert.throws(()=>w.RM_PROJECTION.registry(d),/PROJECTION_REGISTRY_VERSION_OR_READONLY/)});
check("inaccessible source cannot fabricate games",()=>{const d=JSON.parse(JSON.stringify(w.RM_PROJECTION_DATA.catalogue));d.games[0].provenance[0].libraryId="lib-closed";assert.throws(()=>w.RM_CATALOGUE.parse(d,w.RM_PROJECTION.registry(w.RM_PROJECTION_DATA.registry)),/CATALOGUE_INACCESSIBLE_INDEX_HAS_GAME/)});
check("missing precomputed pair is refused",()=>{change("lib-select","lib-demo");change("before-select","gen-b");click("[data-action]",{action:"run-compare"});assert.match(nodes.view.innerHTML,/aucun recalcul dans le navigateur/)});
check("demo remains independent",()=>{const d=boot(false);assert.match(d.nodes.view.innerHTML,/Votre collection/);assert.equal(d.window.RM_PROJECTION_DATA,undefined)});
console.log("UI15_PROJECTION_SMOKE_PASS | assertions="+n+" | runtime=Node.js simulated DOM | browser=NOT_EXECUTED | PS5.1=NOT_EXECUTED");
