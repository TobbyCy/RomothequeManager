/* UI #17 — OFFLINE integrated smoke. Run: node prototype/ui11/integrated-smoke.cjs
 * Uses the repository's hand-authored synthetic candidate, never a PS5.1 export.
 * Simulated DOM: NOT a real browser accessibility/responsive recipe. */
"use strict";
const fs=require("node:fs"),path=require("node:path"),vm=require("node:vm"),assert=require("node:assert/strict");
const files=["fixtures.js","projection-adapter.js","catalogue-adapter.js","projection-integrated.js","app.js"];
function boot(){
const nodes={view:{innerHTML:""},breadcrumb:{textContent:""}},handlers={},window={};
const document={getElementById:id=>nodes[id]||(nodes[id]={innerHTML:"",textContent:""}),querySelectorAll:()=>[],addEventListener:(type,fn)=>handlers[type]=fn};
const context=vm.createContext({window,document,Date,Intl,Map,Set,Number,String,Object,Array,RegExp,Error,console});
for(const name of files)vm.runInContext(fs.readFileSync(path.join(__dirname,name),"utf8"),context,{filename:name});
const click=action=>handlers.click({target:{closest:selector=>selector==="[data-action]"?{dataset:{action}}:null}});
const change=(id,value)=>handlers.change({target:{id,value}});
return {window,nodes,click,change};
}
let count=0;function test(name,fn){fn();console.log("PASS",name);count++}
const app=boot(),{window:w,nodes,click,change}=app,source=w.RM_PROJECTION_DATA;
const clone=x=>JSON.parse(JSON.stringify(x));
test("integrated projection loads without network",()=>assert.match(nodes.view.innerHTML,/Votre collection/));
test("four libraries including inaccessible and unknown",()=>{const r=w.RM_PROJECTION.registry(source.registry);assert.equal(r.length,4);assert.equal(r.find(x=>x.libraryId==="lib-closed").indexAvailable,false);assert.equal(r.find(x=>x.libraryId==="lib-zero").availability,"UNKNOWN")});
test("three precomputed comparisons with six distinct classifications",()=>{const r=w.RM_PROJECTION.registry(source.registry);for(const dto of source.comparisons){const lib=r.find(x=>x.libraryId===dto.library.libraryId);const c=w.RM_PROJECTION.comparison(dto,lib.libraryId,lib);assert.equal(c.countDenominator,c.entries.length)}const m=source.comparisons.find(x=>x.library.libraryId==="lib-mixed");for(const type of w.RM_PROJECTION.types)assert.equal(m.counts[type],1)});
test("fail closed: altered counts",()=>{const dto=clone(source.comparisons[1]);dto.counts.ADDED++;assert.throws(()=>w.RM_PROJECTION.comparison(dto,"lib-mixed"),/PROJECTION_COUNT_MISMATCH_ADDED/)});
test("fail closed: missing coverage proof",()=>{const dto=clone(source.registry);dto.libraries.find(x=>x.libraryId==="lib-mixed").generations[0].coverageProof.pop();assert.throws(()=>w.RM_PROJECTION.registry(dto),/PROJECTION_UNPROVEN_SCOPE/)});
test("fail closed: duplicate partition proof",()=>{const dto=clone(source.registry);const g=dto.libraries.find(x=>x.libraryId==="lib-mixed").generations[0];g.coverageProof.push(clone(g.coverageProof[0]));assert.throws(()=>w.RM_PROJECTION.registry(dto),/PROJECTION_DUPLICATE_PROOF_PARTITION/)});
test("fail closed: unknown counter must be null",()=>{const dto=clone(source.registry);const g=dto.libraries.find(x=>x.libraryId==="lib-mixed").generations[0];g.countKnown=false;g.observedUnitCount=null;assert.throws(()=>w.RM_PROJECTION.registry(dto),/PROJECTION_COUNT_INDEX_CONFLICT/)});
test("no game invented from inaccessible index",()=>{const dto=clone(source.catalogue);dto.games[0].provenance[0].libraryId="lib-closed";const r=w.RM_PROJECTION.registry(source.registry);assert.throws(()=>w.RM_CATALOGUE.parse(dto,r),/CATALOGUE_INACCESSIBLE_INDEX_HAS_GAME/)});
test("dashboard to library and direct snapshot",()=>{click("go:libraries");assert.match(nodes.view.innerHTML,/Bibliothèque multi-états/);click("library:lib-mixed");assert.match(nodes.view.innerHTML,/mixed-gen-b/);click("snapshot-select:lib-mixed:mixed-b");assert.match(nodes.view.innerHTML,/Comparer deux générations/)});
test("precomputed six-class result, denominator and limitations",()=>{w.RM_COMPARE=()=>{throw Error("BUSINESS_RECALC_FORBIDDEN")};click("run-compare");assert.match(nodes.view.innerHTML,/Information insuffisante/);assert.match(nodes.view.innerHTML,/Comparaison impossible/);assert.match(nodes.view.innerHTML,/Dénominateur observé : 6/);assert.match(nodes.view.innerHTML,/SCOPE_INTERSECTION_NOT_FULLY_PROVEN/)});
test("collection, game distinct from ROM observations",()=>{click("go:collection");assert.match(nodes.view.innerHTML,/Mystère synthétique/);click("catalogue-game:game-unknown|PS1");assert.match(nodes.view.innerHTML,/Fiche de jeu/);assert.match(nodes.view.innerHTML,/Observations des unités ROM/);assert.match(nodes.view.innerHTML,/unit-unknown/)});
test("platform filter survives game close and reopen",()=>{change("collection-platform","PS1");click("close-game");assert.match(nodes.view.innerHTML,/option value="PS1" selected/);click("catalogue-game:game-unknown|PS1");assert.match(nodes.view.innerHTML,/option value="PS1" selected/)});
test("back navigation returns to comparison",()=>{click("back");assert.match(nodes.view.innerHTML,/Comparer deux générations/)});
test("zero without proof is never reported as empty collection",()=>{click("library:lib-zero");click("go:compare");click("run-compare");assert.match(nodes.view.innerHTML,/ZERO_OBSERVED_UNITS_NOT_PROOF_OF_EMPTY_LIBRARY/);assert.match(nodes.view.innerHTML,/Couverture incomplète/)});
test("inaccessible index refuses comparison",()=>{click("library:lib-closed");click("go:compare");assert.match(nodes.view.innerHTML,/aucun index historique accessible/)});
test("missing A/B pair refuses silent business recalculation",()=>{click("library:lib-mixed");click("go:compare");change("before-select","mixed-gen-b");click("run-compare");assert.match(nodes.view.innerHTML,/aucun recalcul dans le navigateur/)});
// S2: negative comparison DTO canaries (not merely registry parser canaries).
const s2cards=w.RM_PROJECTION.registry(source.registry);
const s2lib=s2cards.find(x=>x.libraryId==="lib-mixed");
const s2cmp=source.comparisons.find(x=>x.library.libraryId==="lib-mixed");
function rejectComparison(name,mutate,reason){
test(name,()=>{const dto=clone(s2cmp);mutate(dto);assert.throws(()=>w.RM_PROJECTION.comparison(dto,"lib-mixed",s2lib),reason)});
}
rejectComparison("S2 missing A comparison proof",d=>{d.before.coverageProof=[]},/PROJECTION_COMPARISON_BEFORE_REGISTRY_PROOF_MISMATCH/);
rejectComparison("S2 partial A proof cannot support complete scope",d=>{d.before.coverageProof[0].status="partial"},/PROJECTION_COMPARISON_BEFORE_REGISTRY_PROOF_MISMATCH/);
rejectComparison("S2 duplicate romUnitId",d=>{d.entries[1].romUnitId=d.entries[0].romUnitId},/PROJECTION_DUPLICATE_ROM_UNIT_ID/);
rejectComparison("S2 wrong sourceRefs generation",d=>{d.entries[0].sourceRefs.beforeGenerationId="wrong"},/PROJECTION_ENTRY_SOURCE_REFS/);
rejectComparison("S2 forged COMPLETE against registry partitions",d=>{
d.coverageStatus="COMPLETE";d.scopeCompared.push({console:"PS1",status:"complete",reason:"forged"});d.scopeNotComparable=[];
d.before.coverage.find(x=>x.console==="PS1").status="complete";
d.after.coverage.find(x=>x.console==="PS1").status="complete";
},/PROJECTION_COMPARISON_(BEFORE|AFTER)_REGISTRY_PROOF_MISMATCH/);

/* S4 — independent contract matrix. Candidate fixtures only; simulated DOM, not browser. */
function rejectRegistry(name,mutate,pattern){
test(name,()=>{const d=clone(source.registry);mutate(d);assert.throws(()=>w.RM_PROJECTION.registry(d),pattern)});
}
rejectRegistry("S4 registry incompatible schema version",d=>{d.schemaVersion="0.2.0"},/PROJECTION_REGISTRY_VERSION_OR_READONLY/);
rejectRegistry("S4 registry duplicate library identity",d=>{d.libraries[1].libraryId=d.libraries[0].libraryId},/PROJECTION_DUPLICATE_LIBRARY/);
rejectRegistry("S4 duplicate generation identity",d=>{const g=d.libraries.find(x=>x.libraryId==="lib-mixed").generations;g[1].generationId=g[0].generationId},/PROJECTION_DUPLICATE_SNAPSHOT_OR_GENERATION/);
rejectRegistry("S4 duplicate snapshot identity",d=>{const g=d.libraries.find(x=>x.libraryId==="lib-mixed").generations;g[1].snapshotId=g[0].snapshotId},/PROJECTION_DUPLICATE_SNAPSHOT_OR_GENERATION/);
rejectRegistry("S4 complete partition without exhaustive scope",d=>{d.libraries.find(x=>x.libraryId==="lib-mixed").generations[0].scopeIsExhaustive=false},/PROJECTION_UNPROVEN_SCOPE/);
rejectRegistry("S4 multi-root proof missing from complete partition",d=>{const l=d.libraries.find(x=>x.libraryId==="lib-mixed");l.sources.push({sourceKey:"second",rootKey:"root-2",domains:["ROMS"]})},/PROJECTION_UNPROVEN_SCOPE/);
rejectRegistry("S4 unknown count cannot be physical zero",d=>{const g=d.libraries.find(x=>x.libraryId==="lib-closed").generations[0];g.observedUnitCount=0},/PROJECTION_UNKNOWN_COUNT_NOT_NULL/);
rejectRegistry("S4 inaccessible index cannot claim known count",d=>{const g=d.libraries.find(x=>x.libraryId==="lib-closed").generations[0];g.countKnown=true;g.observedUnitCount=0},/PROJECTION_COUNT_INDEX_CONFLICT/);
rejectRegistry("S4 unsupported availability",d=>{d.libraries[0].currentAvailability="DISCONNECTED"},/PROJECTION_AVAILABILITY_ENUM/);
rejectRegistry("S4 unsupported freshness",d=>{d.libraries[0].freshness="OLD"},/PROJECTION_FRESHNESS_ENUM/);
test("S4 OFFLINE STALE and UNKNOWN remain distinct",()=>{const l=w.RM_PROJECTION.registry(source.registry);const off=l.find(x=>x.libraryId==="lib-demo"),unk=l.find(x=>x.libraryId==="lib-zero");assert.equal(off.availability,"OFFLINE");assert.equal(off.freshness,"STALE");assert.equal(unk.availability,"UNKNOWN")});
rejectComparison("S4 comparison incompatible schema version",d=>{d.schemaVersion="0.2.0"},/PROJECTION_COMPARISON_VERSION_OR_READONLY/);
rejectComparison("S4 comparison mismatched library",d=>{d.library.libraryId="wrong"},/PROJECTION_COMPARISON_LIBRARY/);
rejectComparison("S4 comparison mismatched snapshot",d=>{d.before.snapshotId="wrong"},/PROJECTION_COMPARISON_BEFORE_REGISTRY_MISMATCH/);
rejectComparison("S4 comparison mismatched generation",d=>{d.before.generationId="wrong"},/PROJECTION_COMPARISON_BEFORE_REGISTRY_MISMATCH/);
rejectComparison("S4 comparison wrong denominator",d=>{d.countDenominator++},/PROJECTION_DENOMINATOR_MISMATCH/);
rejectComparison("S4 comparison class counter altered",d=>{d.counts.UNKNOWN++},/PROJECTION_COUNT_MISMATCH_UNKNOWN/);
rejectComparison("S4 comparison unsupported classification",d=>{d.entries[0].classification="DELETED"},/PROJECTION_CLASSIFICATION_ENUM/);
rejectComparison("S4 compound forged COMPLETE counts proof and partitions",d=>{
d.coverageStatus="COMPLETE";d.scopeNotComparable=[];d.scopeCompared.push({console:"PS1",status:"complete",reason:"fabricated"});
for(const ref of [d.before,d.after]){
ref.coverage.find(x=>x.console==="PS1").status="complete";
ref.coverageProof.find(x=>x.console==="PS1").status="complete";
ref.scopeIsExhaustive=true;
}
d.counts.UNKNOWN=0;d.counts.NOT_COMPARABLE=0;d.counts.UNCHANGED=3;
d.entries=d.entries.map(e=>({...e,classification:"UNCHANGED"}));
},/PROJECTION_COMPARISON_(BEFORE|AFTER)_REGISTRY_PROOF_MISMATCH/);
test("S4 projection mode never calls JS demonstration comparator",()=>{
const original=w.RM_COMPARE;w.RM_COMPARE=()=>{throw Error("S4_FORBIDDEN_JS_RECOMPUTATION")};
try{const r=w.RM_PROJECTION.comparison(s2cmp,"lib-mixed",s2lib);assert.equal(r.projectionMode,true);assert.equal(r.countDenominator,r.entries.length)}finally{w.RM_COMPARE=original}
});
console.log("UI17_INTEGRATED_SMOKE_PASS | checks="+count+" | Node simulated DOM | PS5.1=NOT_EXECUTED | BROWSER=NOT_EXECUTED");
