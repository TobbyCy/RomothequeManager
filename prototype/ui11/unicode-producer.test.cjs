/* STAB-03B U5 — optional contract/UI test against the REAL UTF-8 JSON
   emitted by Dev1's synthetic PowerShell exporter (not a hand-edited fixture).
   node prototype/ui11/unicode-producer.test.cjs "<output-json>"
   Produce input via: powershell.exe -NoProfile -File .\\tests\\Export-SyntheticCanonicalCatalogue.ps1 -OutputJson "<output-json>"
   No ROM, StateRoot, network, or physical drive is accessed here. */
"use strict";
const fs=require("node:fs"),path=require("node:path"),vm=require("node:vm");
const assert=require("node:assert/strict"),crypto=require("node:crypto");
const file=process.argv[2];
if(!file)throw Error("PRODUCER_JSON_REQUIRED: pass unchanged JSON file produced by Dev1 PowerShell synthetic exporter");
const bytes=fs.readFileSync(file);
const raw=new TextDecoder("utf-8",{fatal:true}).decode(bytes);
assert.doesNotMatch(raw,/\uFFFD/,"replacement Unicode character is not admissible");
const dto=JSON.parse(raw);
const w={RM_PROJECTION_DATA:{registry:dto.registry,catalogue:dto,comparisons:[]}};
const elements=new Map(),events={};
const element=id=>{if(!elements.has(id))elements.set(id,{innerHTML:"",textContent:"",focus(){},setSelectionRange(){},classList:{toggle(){}},setAttribute(){}});return elements.get(id)};
const document={getElementById:element,querySelectorAll:()=>[],addEventListener:(type,fn)=>events[type]=fn};
const context=vm.createContext({window:w,globalThis:w,document,Date,Intl,Map,Set,Number,String,Object,Array,RegExp,Error,console});
for(const name of ["fixtures.js","projection-adapter.js","../../adapters/shared/catalogue-t3-consumer.js","t2-catalogue-model.js","app.js"]){
 vm.runInContext(fs.readFileSync(path.join(__dirname,name),"utf8"),context,{filename:name});
}
const original=JSON.stringify(dto.games);
const game=dto.games.find(g=>g.title==="Étoile des disques");
assert.ok(game,"UNICODE_PRODUCER_TITLE_MISSING: do not replace mojibake in the UI");
assert.equal(game.title.codePointAt(0),0x00C9);
const model=w.RM_T2_CATALOGUE.normalize(dto,[]);
const identity=JSON.stringify([game.libraryId,game.platform,game.gameId]);
const projected=model.games.find(g=>g.identity===identity);
assert.ok(projected,"COMPOUND_IDENTITY_LOST");
assert.equal(projected.title,"Étoile des disques");
assert.deepEqual(JSON.parse(JSON.stringify(projected.provenance)),game.provenance[0]);
const results=w.RM_T2_CATALOGUE.query(model,{search:"Étoile"});
assert.ok(results.some(g=>g.identity===identity),"ACCENTED_SEARCH_MISSES_GAME");
assert.equal(JSON.stringify(dto.games),original,"INPUT_DTO_MUTATED");
const click=action=>events.click({target:{closest:selector=>{
 if(selector==="[data-route]"&&action.startsWith("route:"))return {dataset:{route:action.slice(6)}};
 if(selector==="[data-action]"&&!action.startsWith("route:"))return {dataset:{action}};
 return null;
}}});
click("route:collection");
assert.match(element("view").innerHTML,/Étoile des disques/);
click("catalogue-game:"+identity);
const detail=element("view").innerHTML;
assert.match(detail,/Étoile des disques/);
assert.match(detail,new RegExp(game.provenance[0].snapshotId));
assert.match(detail,new RegExp(game.provenance[0].generationId));
assert.equal(JSON.stringify(dto.games),original,"UI_MUTATED_SOURCE");
events.input({target:{id:"collection-search",value:"Étoile",selectionStart:6}});
assert.match(element("view").innerHTML,/Étoile des disques/);
console.log("STAB03B_UNICODE_PRODUCER_UI_PASS | SHA256="+crypto.createHash("sha256").update(bytes).digest("hex")+
 " | library="+game.libraryId+" | game="+game.gameId+" | input=unaltered synthetic PS-export JSON"+
 " | browser=NOT_EXECUTED | physical_data=NONE");
