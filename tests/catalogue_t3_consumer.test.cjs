// T3 candidate consumer tests: node tests/catalogue_t3_consumer.test.cjs
"use strict";
const fs=require("node:fs"),path=require("node:path"),vm=require("node:vm"),assert=require("node:assert/strict");
const ctx=vm.createContext({});
vm.runInContext(fs.readFileSync(path.join(__dirname,"../adapters/shared/catalogue-t3-consumer.js"),"utf8"),ctx);
const model=ctx.RM_T3_CATALOGUE;
const clone=v=>JSON.parse(JSON.stringify(v));
const proof={console:"GBA",sourceKey:"src",rootKey:"root",domain:"ROMS",status:"complete"};
const snapshot={snapshotId:"snap",generationId:"gen",capturedAtUtc:"2026-10-21T12:00:00Z",scanStatus:"complete",scopeIsExhaustive:true,coverageProof:[proof],coverage:[{console:"GBA",status:"complete"}],countKnown:true,observedUnitCount:1};
const dto={schemaVersion:"0.2.0-ui-catalogue-candidate",readOnly:true,
 registry:{schemaVersion:"0.1.0-synthetic",readOnly:true,libraries:[{libraryId:"lib",indexAccessible:true,currentAvailability:"OFFLINE",availabilityObservedAtUtc:"2026-10-21T10:00:00Z",lastVerifiedCompleteRef:"snap",freshness:"FRESH",platforms:["GBA"],sources:[{sourceKey:"src",rootKey:"root",domains:["ROMS"]}],generations:[snapshot]}]},
 games:[{libraryId:"lib",gameId:"game",platform:"GBA",title:null,quality:"KNOWN",status:"KNOWN",coverStatus:"KNOWN",availability:"OFFLINE",freshness:"FRESH",variants:[{variantId:"variant",label:"variant"}],provenance:[{libraryId:"lib",snapshotId:"snap",generationId:"gen",observedAtUtc:"2026-10-21T12:00:00Z",source:"SYNTHETIC_SNAPSHOT"}],observations:[{libraryId:"lib",snapshotId:"snap",generationId:"gen",romUnitId:"unit",variantId:"variant",observedTitle:null,observationStatus:"OBSERVED"}]}],
 diagnostics:[],countKnown:true,gameCount:1};
let checks=0;
function ok(name,fn){fn();checks++;process.stdout.write("PASS "+name+"\n")}
function rejects(name,change,code){ok(name,()=>{const d=clone(dto);change(d);assert.throws(()=>model.normalize(d),new RegExp("T3_CATALOGUE_"+code))})}
const sharedSample=JSON.parse(fs.readFileSync(path.join(__dirname,"fixtures/CatalogueT3CanonicalSample.json"),"utf8"));
ok("shared JSON reference fixture accepted",()=>{const v=model.normalize(sharedSample);assert.equal(v.games[0].quality,"STALE");assert.equal(v.games[0].observations.length,2);assert.equal(v.games[0].availability,"OFFLINE")});
ok("valid nullable title and offline historical source",()=>assert.equal(model.normalize(dto).games.length,1));
ok("ordinal identity and search",()=>{const x=model.normalize(dto);assert.equal(model.query(x,{libraryId:"LIB"}).total,0);assert.equal(model.query(x,{search:"GAME"}).total,1)});
rejects("foreign proof console despite valid partition names",d=>d.registry.libraries[0].generations[0].coverageProof[0].console="PS1","PROOF_PARTITION");
rejects("no generations cannot certify a global zero count",d=>{
 d.registry.libraries[0].generations=[];
 d.registry.libraries[0].lastVerifiedCompleteRef=null;
 d.games=[];d.gameCount=0;d.countKnown=true;
},"COUNT_FORGED");
rejects("wrong schema",d=>d.schemaVersion="wrong","SCHEMA");
rejects("orphan snapshot",d=>d.games[0].provenance[0].snapshotId="ghost","REFERENCE");
rejects("generation is not snapshot alias",d=>d.games[0].provenance[0].generationId="snap","REFERENCE");
rejects("false coverage",d=>d.games[0].coverStatus="UNKNOWN","COVERAGE_FORGED");
rejects("removed proof",d=>d.registry.libraries[0].generations[0].coverageProof=[],"COVERAGE_FORGED");
rejects("duplicate partial proof",d=>{d.registry.libraries[0].generations[0].coverageProof.push({...proof,status:"partial"})},"PROOF_PARTITION");
rejects("duplicate ROM",d=>d.games[0].observations.push({...d.games[0].observations[0]}),"OBSERVATION");
rejects("orphan variant",d=>d.games[0].observations[0].variantId="ghost","OBSERVATION");
rejects("forged count",d=>d.gameCount=999,"COUNT_FORGED");
rejects("missing coverage with definitive count",d=>d.diagnostics.push({code:"CATALOG_COVERAGE_UNPROVEN"}),"COUNT_FORGED");
rejects("forged current availability",d=>d.games[0].availability="ONLINE","LIBRARY_STATE");
rejects("older snapshot masked as latest",d=>{
 const old={...d.registry.libraries[0].generations[0],snapshotId:"old",generationId:"old-gen",capturedAtUtc:"2026-10-20T12:00:00Z"};
 d.registry.libraries[0].generations.push(old);
 d.games[0].provenance[0]={...d.games[0].provenance[0],snapshotId:"old",generationId:"old-gen",observedAtUtc:"2026-10-20T12:00:00Z"};
 d.games[0].observations[0].snapshotId="old";d.games[0].observations[0].generationId="old-gen";
},"HISTORICAL_SNAPSHOT");
rejects("observed-count forged upward",d=>d.registry.libraries[0].generations[0].observedUnitCount=2,"SNAPSHOT_COUNT");
rejects("bad disc",d=>d.games[0].observations[0].discNumber=0,"DISC");
rejects("title falsely asserted",d=>d.games[0].title="Invented","TITLE_UNKNOWN");
rejects("coordinated proof and claim forgery",d=>{
 const g=d.registry.libraries[0].generations[0];
 g.coverageProof=[];g.scanStatus="failed";g.scopeIsExhaustive=false;
 g.coverage[0].status="complete";d.games[0].coverStatus="KNOWN";d.games[0].status="KNOWN";
},"COVERAGE_FORGED");
ok("strict source identity casing",()=>assert.equal(model.query(model.normalize(dto),{libraryId:"LIB"}).total,0));
ok("mixed GBA proven + PS1 unobserved keeps partial overall",()=>{
 const d=clone(dto),lib=d.registry.libraries[0],gen=lib.generations[0];
 lib.platforms.push("PS1");lib.lastVerifiedCompleteRef=null;
 gen.coverage.push({console:"PS1",status:"unobserved"});
 d.games[0].coverStatus="UNKNOWN";d.games[0].status="INCOMPLETE";
 d.games[0].quality="PARTIALLY_OBSERVED";d.games[0].observations[0].observationStatus="PARTIAL";
 d.countKnown=false;d.gameCount=null;
 assert.equal(model.normalize(d).games[0].coverStatus,"UNKNOWN");
});
rejects("verified snapshot missing",d=>d.registry.libraries[0].lastVerifiedCompleteRef="ghost","LAST_VERIFIED_ORPHAN");
rejects("snapshot and generation conflated",d=>d.registry.libraries[0].generations[0].snapshotId="gen","IDENTITY_CONFLATED");
rejects("unknown availability with observed date",d=>d.registry.libraries[0].currentAvailability="UNKNOWN","LIBRARY_DATE");
rejects("wrong last complete proof",d=>{
 const g=d.registry.libraries[0].generations[0];g.scanStatus="failed";
 g.coverage[0].status="partial";
},"LAST_VERIFIED_UNPROVEN");
ok("length-prefixed Ordinal game sort equals PowerShell",()=>{
 const d=clone(dto),g=clone(d.games[0]);
 g.gameId="z";g.observations[0].romUnitId="unit-two";
 d.games.push(g);d.gameCount=2;
 d.registry.libraries[0].generations[0].observedUnitCount=2;
 assert.deepEqual(Array.from(model.normalize(d).games, g=>g.gameId),["z","game"]);
});
ok("pagination",()=>assert.equal(model.query(model.normalize(dto),{limit:1}).items.length,1));
// When CI provides the real PS5.1 synthetic export, validate those exact bytes
// in the same runtime and exercise Unicode search. Never infer that the hand-written
// shared fixture proves PowerShell's source decoding.
if(process.env.RM_CANONICAL_EXPORT_JSON){
 ok("runtime PS5.1 JSON -> JS normalization + Unicode codepoints/search",()=>{
  const p=path.resolve(process.env.RM_CANONICAL_EXPORT_JSON);
  if(path.basename(p)!=="rm-t3-catalogue-synthetic.json")throw Error("UNEXPECTED_SYNTHETIC_EXPORT_PATH");
  const raw=fs.readFileSync(p,"utf8");
  const exported=JSON.parse(raw);
  const expected=String.fromCodePoint(0x00C9)+"toile des disques";
  assert.equal(exported.schemaVersion,"0.2.0-ui-catalogue-candidate");
  assert.equal(exported.games.length,1);
  assert.equal(exported.games[0].title,expected);
  assert.equal(exported.games[0].title.codePointAt(0),0x00C9);
  const normalized=model.normalize(exported);
  assert.equal(normalized.gameCount,1);
  assert.equal(normalized.games[0].observations.length,3);
  assert.equal(normalized.games[0].variants.length,2);
  assert.equal(model.query(normalized,{search:String.fromCodePoint(0x00C9)+"toile"}).total,1);
  assert.equal(model.query(normalized,{search:"éTOILE"}).total,1);
 });
}
console.log("T3_CANDIDATE_CONSUMER_TESTS_PASS",checks);
