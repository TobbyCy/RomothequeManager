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
console.log("UI17_INTEGRATED_SMOKE_PASS | checks="+count+" | Node simulated DOM | PS5.1=NOT_EXECUTED | BROWSER=NOT_EXECUTED");
