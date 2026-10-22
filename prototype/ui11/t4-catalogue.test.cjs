// T4-B synthetic contract/UI regression. Node.js only; NO REAL ROM/SSD/StateRoot.
// Run from repository root: node prototype/ui11/t4-catalogue.test.cjs
"use strict";
const fs=require("node:fs"),path=require("node:path"),vm=require("node:vm"),assert=require("node:assert/strict");
const scripts=["fixtures.js","projection-adapter.js","../../adapters/shared/catalogue-t3-consumer.js","t2-catalogue-model.js","t4-catalogue-demo.js"];
const w={},ctx=vm.createContext({window:w,globalThis:w});
for(const name of scripts)vm.runInContext(fs.readFileSync(path.join(__dirname,name),"utf8"),ctx,{filename:name});
const clone=x=>JSON.parse(JSON.stringify(x));
const base=w.RM_PROJECTION_DATA.catalogue;
const parse=d=>w.RM_T2_CATALOGUE.normalize(d,[]);
let checks=0;
function test(name,fn){fn();checks++;console.log("PASS",name)}
function reject(name,mutate){test(name,()=>{const d=clone(base);mutate(d);assert.throws(()=>parse(d))})}
function partial(d){
 const l=d.registry.libraries[0],g=l.generations[0];
 g.scanStatus="partial";g.scopeIsExhaustive=false;
 g.coverage[0].status="partial";g.coverageProof[0].status="partial";
 // A partial scan can count observed units; it cannot prove exhaustive gameCount.
 g.countKnown=true;g.observedUnitCount=2;l.lastVerifiedCompleteRef=null;
 d.games[0].quality="PARTIALLY_OBSERVED";d.games[0].status="INCOMPLETE";
 d.games[0].coverStatus="UNKNOWN";
 d.games[0].observations.forEach(o=>o.observationStatus="PARTIAL");
 d.countKnown=false;d.gameCount=null;
}
test("T3 shared-reference fixture normalizes without legacy conversion",()=>{
 const m=parse(clone(base));assert.equal(m.schemaVersion,"0.2.0-ui-catalogue-candidate");
 assert.equal(m.countKnown,true);assert.equal(m.gameCount,1);assert.equal(m.games[0].units.length,2);
});
test("snapshotId is not a generationId alias",()=>{
 const g=parse(clone(base)).games[0];
 assert.equal(g.provenance.snapshotId,"snap-ref");assert.equal(g.provenance.generationId,"gen-ref");
 assert.equal(g.identity,JSON.stringify(["lib-t3-ref","GBA","game-ref"]));
});
test("OFFLINE and STALE preserve observed multi-disc history",()=>{
 const g=parse(clone(base)).games[0];assert.equal(g.availability,"OFFLINE");
 assert.equal(g.freshness,"STALE");assert.equal(g.units[1].discNumber,2);
 assert.equal(g.units[0].observationStatus,"OBSERVED");
});
test("compound filter and ordinal case-sensitive library",()=>{
 const m=parse(clone(base));assert.equal(w.RM_T2_CATALOGUE.query(m,{library:"lib-t3-ref",platform:"GBA",status:"STALE"}).length,1);
 assert.equal(w.RM_T2_CATALOGUE.query(m,{library:"LIB-T3-REF"}).length,0);
});
test("nullable title without title evidence, no invented title",()=>{
 const d=clone(base);d.games[0].title=null;d.games[0].observations.forEach(o=>o.observedTitle=null);
 assert.equal(parse(d).games[0].title,null);
});
test("contradictory observed titles stay distinct",()=>{
 const d=clone(base);d.games[0].title=null;d.games[0].quality="CONTRADICTORY";d.games[0].observations[1].observedTitle="Autre jeu";
 assert.equal(parse(d).games[0].quality,"CONTRADICTORY");
});
test("partial observed source keeps index and unknown global count",()=>{
 const d=clone(base);partial(d);const m=parse(d);
 assert.equal(m.countKnown,false);assert.equal(m.gameCount,null);
 assert.equal(m.games[0].coverageStatus,"INCOMPLETE");
 assert.equal(m.games[0].units[0].observationStatus,"PARTIAL");
 assert.equal(m.registry.get("lib-t3-ref").indexAvailable,true);
});
test("index inaccessible never proves no games",()=>{
 const d=clone(base);d.games=[];d.registry.libraries[0].indexAccessible=false;
 d.registry.libraries[0].generations[0].countKnown=false;
 d.registry.libraries[0].generations[0].observedUnitCount=null;
 d.countKnown=false;d.gameCount=null;
 assert.equal(parse(d).games.length,0);assert.equal(parse(d).gameCount,null);
});
test("distinct libraries never merge same gameId",()=>{
 const d=clone(base),lib=clone(d.registry.libraries[0]),game=clone(d.games[0]);
 lib.libraryId="lib-other";game.libraryId="lib-other";game.provenance[0].libraryId="lib-other";
 game.observations.forEach(o=>o.libraryId="lib-other");
 d.registry.libraries.push(lib);d.games.push(game);d.gameCount=2;
 const m=parse(d);assert.equal(m.games.length,2);assert.notEqual(m.games[0].identity,m.games[1].identity);
});
reject("invalid schema",d=>d.schemaVersion="unknown");
reject("readOnly cannot be false",d=>d.readOnly=false);
reject("duplicate compound key",d=>d.games.push(clone(d.games[0])));
reject("orphan snapshot",d=>d.games[0].provenance[0].snapshotId="ghost");
reject("generation mismatched",d=>d.games[0].provenance[0].generationId="snap-ref");
reject("orphan observation variant",d=>d.games[0].observations[0].variantId="ghost");
reject("duplicate observed romUnitId",d=>d.games[0].observations.push(clone(d.games[0].observations[0])));
reject("forged complete proof",d=>d.registry.libraries[0].generations[0].coverageProof=[]);
reject("duplicate coverage partition",d=>d.registry.libraries[0].generations[0].coverageProof.push(clone(d.registry.libraries[0].generations[0].coverageProof[0])));
reject("game coverage forged",d=>d.games[0].coverStatus="UNKNOWN");
reject("known total forged",d=>d.gameCount=4);
reject("unknown total forged as zero",d=>{partial(d);d.gameCount=0});
reject("availability cannot diverge from registry",d=>d.games[0].availability="ONLINE");
reject("freshness cannot diverge from registry",d=>d.games[0].freshness="FRESH");
reject("STALE quality inconsistent with freshness",d=>{d.registry.libraries[0].freshness="FRESH";d.games[0].freshness="FRESH"});
reject("title must match observed title",d=>d.games[0].title="Fake title");
reject("contradictory title cannot be definitive",d=>d.games[0].observations[1].observedTitle="Other title");
reject("missing title evidence cannot claim known title",d=>d.games[0].observations.forEach(o=>o.observedTitle=null));
reject("invalid discNumber zero",d=>d.games[0].observations[1].discNumber=0);
reject("negative discNumber",d=>d.games[0].observations[0].discNumber=-1);
reject("variant label must be a string",d=>d.games[0].variants[0].label=null);
reject("orphan diagnostic library",d=>d.diagnostics.push({code:"TEST",libraryId:"ghost"}));
test("UI route Collection > Fiche displays full distinct provenance and observed discs",()=>{
 const nodes=new Map(),listeners={};
 function elem(id){if(!nodes.has(id))nodes.set(id,{innerHTML:"",textContent:"",focus(){},classList:{toggle(){}},setAttribute(){}});return nodes.get(id)}
 const doc={getElementById:elem,querySelectorAll(){return[]},addEventListener(k,fn){listeners[k]=fn}};
 ctx.document=doc;
 vm.runInContext(fs.readFileSync(path.join(__dirname,"app.js"),"utf8"),ctx,{filename:"app.js"});
 assert.match(elem("view").innerHTML,/Votre collection/);
 const click=action=>listeners.click({target:{closest(s){
   if(s==="[data-route]")return action.startsWith("route:")?{dataset:{route:action.slice(6)}}:null;
   if(s==="[data-action]")return action.startsWith("route:")?null:{dataset:{action}};
   return null;
 }}});
 click("route:collection");assert.match(elem("view").innerHTML,/Étoile fictive/);
 click("catalogue-game:"+JSON.stringify(["lib-t3-ref","GBA","game-ref"]));
 const detail=elem("view").innerHTML;
 assert.match(detail,/disc-1/);assert.match(detail,/disc-2/);
 assert.match(detail,/snap-ref/);assert.match(detail,/gen-ref/);
 assert.match(detail,/STALE/);assert.match(detail,/Hors ligne/);
 click("close-game");assert.doesNotMatch(elem("view").innerHTML,/Fermer la fiche/);
});
console.log("T4B_T3_SYNTHETIC_TESTS_PASS "+checks+" | NODE_RUNTIME | NO_PHYSICAL_DATA");