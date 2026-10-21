/* Synthetic contract and UI smoke tests. Run: node prototype/ui11/smoke.cjs */
"use strict";
const fs=require("node:fs"),path=require("node:path"),vm=require("node:vm"),assert=require("node:assert/strict");
const root=__dirname,events={};const view={innerHTML:"",textContent:""};
const nodes={view,breadcrumb:{textContent:""}};
const document={
getElementById:id=>nodes[id]||(nodes[id]={innerHTML:"",textContent:""}),
querySelectorAll:()=>[],
addEventListener:(name,fn)=>{events[name]=fn}
};
const window={};
const context=vm.createContext({window,document,Intl,Date,Map,Set,Object,Array,String,Number,RegExp,Error,console});
for(const file of ["fixtures.js","app.js"])vm.runInContext(fs.readFileSync(path.join(root,file),"utf8"),context,{filename:file});
const libs=window.RM_FIXTURES.libraries,compare=window.RM_COMPARE;
const check=(name,fn)=>{fn();console.log("PASS",name)};
const same=(x,y)=>assert.equal(x,y);
check("four fictional libraries and availability states",()=>{
same(libs.length,4);assert.deepEqual([...libs.map(l=>l.availability)].sort(),["INACCESSIBLE","OFFLINE","ONLINE","UNKNOWN"].sort());
});
check("six categories and exhaustive counts",()=>{
const r=compare(libs[0].snapshots[0],libs[0].snapshots[1]);
for(const k of ["ADDED","CHANGED","UNCHANGED","NOT_OBSERVED_IN_B_COVERED_SCOPE","UNKNOWN","NOT_COMPARABLE"])assert.ok(r.counts[k]>0,k);
same(Object.values(r.counts).reduce((a,b)=>a+b,0),r.entries.length);same(r.complete,true);
assert.ok(!r.entries.some(e=>e.classification==="DELETED"));
});
check("offline stale history stays comparable",()=>{
const r=compare(libs[1].snapshots[0],libs[1].snapshots[1]);same(r.counts.UNCHANGED,2);same(r.afterAvailability,"OFFLINE");same(r.afterFreshness,"STALE");
});
check("partial and failed scans do not imply additions or removals",()=>{
const r=compare(libs[0].snapshots[1],libs[0].snapshots[2]);same(r.complete,false);same(r.counts.ADDED,0);same(r.counts.NOT_OBSERVED_IN_B_COVERED_SCOPE,0);
const q=compare(libs[3].snapshots[0],libs[3].snapshots[1]);same(q.entries.length,0);same(q.complete,false);
});
check("empty incomplete scope never becomes proof of no differences",()=>{
const x=libs[3].snapshots;const r=compare(x[0],x[1]);same(r.entries.length,0);same(r.scopes[0].comparable,false);
});
check("mismatched library and same generation are rejected",()=>{
assert.throws(()=>compare(libs[0].snapshots[0],libs[1].snapshots[1]),/SNAPSHOT_LIBRARY_MISMATCH/);
assert.throws(()=>compare(libs[0].snapshots[0],libs[0].snapshots[0]),/SNAPSHOT_GENERATION_REUSED/);
});
check("invalid digest and transient fileId are rejected",()=>{
const a=JSON.parse(JSON.stringify(libs[0].snapshots[0])),b=libs[0].snapshots[1];a.units[0].observationDigest="bad";
assert.throws(()=>compare(a,b),/SNAPSHOT_DIGEST_INVALID/);
a.units[0].observationDigest="a".repeat(64);a.units[0].fileId="transient";
assert.throws(()=>compare(a,b),/SNAPSHOT_UNIT_INVALID/);
});
check("dashboard and navigation are rendered",()=>{
assert.match(view.innerHTML,/Votre collection/);
events.click({target:{closest:selector=>selector==="[data-route]"?{dataset:{route:"libraries"}}:null}});
assert.match(view.innerHTML,/Vos espaces de jeux/);
events.click({target:{closest:selector=>selector==="[data-action]"?{dataset:{action:"go:compare"}}:null}});
assert.match(view.innerHTML,/Comparer deux générations/);
events.click({target:{closest:selector=>selector==="[data-action]"?{dataset:{action:"run-compare"}}:null}});
assert.match(view.innerHTML,/Ajout observé/);
assert.match(view.innerHTML,/Non retrouvé dans B/);
});
check("inaccessible library refuses comparison",()=>{
events.change({target:{id:"lib-select",value:"lib-optical"}});
assert.match(view.innerHTML,/aucun index historique accessible/);
assert.doesNotMatch(view.innerHTML,/Lancer la comparaison/);
});
console.log("UI11_SYNTHETIC_SMOKE_PASS | runtime=Node.js | real_rom=0 | external_dependencies=0");
