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
 registry:{schemaVersion:"0.1.0-synthetic",readOnly:true,libraries:[{libraryId:"lib",indexAccessible:true,freshness:"FRESH",platforms:["GBA"],sources:[{sourceKey:"src",rootKey:"root",domains:["ROMS"]}],generations:[snapshot]}]},
 games:[{libraryId:"lib",gameId:"game",platform:"GBA",title:null,quality:"KNOWN",status:"KNOWN",coverStatus:"KNOWN",availability:"OFFLINE",freshness:"FRESH",variants:[{variantId:"variant",label:"variant"}],provenance:[{libraryId:"lib",snapshotId:"snap",generationId:"gen",observedAtUtc:"2026-10-21T12:00:00Z",source:"SYNTHETIC_SNAPSHOT"}],observations:[{libraryId:"lib",snapshotId:"snap",generationId:"gen",romUnitId:"unit",variantId:"variant",observedTitle:null,observationStatus:"OBSERVED"}]}],
 diagnostics:[],countKnown:true,gameCount:1};
let checks=0;
function ok(name,fn){fn();checks++;process.stdout.write("PASS "+name+"\n")}
function rejects(name,change,code){ok(name,()=>{const d=clone(dto);change(d);assert.throws(()=>model.normalize(d),new RegExp("T3_CATALOGUE_"+code))})}
const sharedSample=JSON.parse(fs.readFileSync(path.join(__dirname,"fixtures/CatalogueT3CanonicalSample.json"),"utf8"));
ok("shared JSON reference fixture accepted",()=>{const v=model.normalize(sharedSample);assert.equal(v.games[0].quality,"STALE");assert.equal(v.games[0].observations.length,2);assert.equal(v.games[0].availability,"OFFLINE")});
ok("valid nullable title and offline historical source",()=>assert.equal(model.normalize(dto).games.length,1));
ok("ordinal identity and search",()=>{const x=model.normalize(dto);assert.equal(model.query(x,{libraryId:"LIB"}).total,0);assert.equal(model.query(x,{search:"GAME"}).total,1)});
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
ok("pagination",()=>assert.equal(model.query(model.normalize(dto),{limit:1}).items.length,1));
console.log("T3_CANDIDATE_CONSUMER_TESTS_PASS",checks);
