/* UI #13 — read-only projection DTO adapter. No fetch, filesystem or business recomputation. */
(function(){
"use strict";
const TYPES=["ADDED","CHANGED","UNCHANGED","NOT_OBSERVED_IN_B_COVERED_SCOPE","UNKNOWN","NOT_COMPARABLE"];
const fail=code=>{throw Error("PROJECTION_"+code)};
const array=(v,name)=>{if(!Array.isArray(v))fail(name+"_ARRAY");return v};
const text=(v,name)=>{if(typeof v!=="string"||!v.trim())fail(name+"_TEXT");return v};
const enumOf=(v,choices,name)=>{if(!choices.includes(v))fail(name+"_ENUM");return v};
const integer=(v,name)=>{if(!Number.isSafeInteger(v)||v<0)fail(name+"_COUNT");return v};
function registry(dto){
if(!dto||dto.schemaVersion!=="0.1.0-synthetic"||dto.readOnly!==true)fail("REGISTRY_VERSION_OR_READONLY");
return array(dto.libraries,"LIBRARIES").map(l=>{
text(l.libraryId,"LIBRARY_ID");text(l.name,"LIBRARY_NAME");
const generations=array(l.generations,"GENERATIONS").map(g=>{
text(g.snapshotId,"SNAPSHOT_ID");text(g.generationId,"GENERATION_ID");text(g.capturedAtUtc,"CAPTURED_AT");
return {snapshotId:g.snapshotId,generationId:g.generationId,capturedAtUtc:g.capturedAtUtc,scanStatus:enumOf(g.scanStatus,["complete","partial","failed"],"SCAN"),coverage:array(g.coverage,"COVERAGE"),coverageProof:array(g.coverageProof,"COVERAGE_PROOF"),scopeIsExhaustive:g.scopeIsExhaustive===true,observedUnitCount:integer(g.observedUnitCount,"OBSERVED_UNITS")};
});
const availability=enumOf(l.currentAvailability,["ONLINE","OFFLINE","INACCESSIBLE","UNKNOWN"],"AVAILABILITY");
const freshness=enumOf(l.freshness,["FRESH","STALE","UNKNOWN"],"FRESHNESS");
if(typeof l.indexAccessible!=="boolean")fail("INDEX_ACCESSIBLE");
return {libraryId:l.libraryId,name:l.name,description:"Projection synthétique du noyau · lecture seule",platforms:array(l.platforms,"PLATFORMS"),availability,freshness,observedAtUtc:l.availabilityObservedAtUtc??null,indexAvailable:l.indexAccessible,lastVerifiedCompleteRef:l.lastVerifiedCompleteRef??null,snapshots:generations,projectionMode:true};
});
}
function comparison(dto,expectedLibraryId){
if(!dto||dto.schemaVersion!=="0.1.0-synthetic"||dto.readOnly!==true)fail("COMPARISON_VERSION_OR_READONLY");
if(!dto.library||dto.library.libraryId!==expectedLibraryId)fail("COMPARISON_LIBRARY");
if(!dto.before||!dto.after)fail("COMPARISON_REFS");
const beforeId=text(dto.before.snapshotId,"BEFORE_SNAPSHOT_ID"),afterId=text(dto.after.snapshotId,"AFTER_SNAPSHOT_ID");
if(beforeId===afterId)fail("COMPARISON_SAME_SNAPSHOT");
const counts={};for(const type of TYPES)counts[type]=integer(dto.counts?.[type],"CLASS_"+type);
const entries=array(dto.entries,"ENTRIES");
if(entries.length!==integer(dto.countDenominator,"DENOMINATOR"))fail("DENOMINATOR_MISMATCH");
const actual=Object.fromEntries(TYPES.map(t=>[t,0]));
for(const e of entries){enumOf(e.classification,TYPES,"CLASSIFICATION");actual[e.classification]++;}
for(const t of TYPES)if(counts[t]!==actual[t])fail("COUNT_MISMATCH_"+t);
const compared=array(dto.scopeCompared,"SCOPE_COMPARED"),excluded=array(dto.scopeNotComparable,"SCOPE_EXCLUDED"),limitations=array(dto.limitations,"LIMITATIONS");
const coverageStatus=enumOf(dto.coverageStatus,["COMPLETE","INCOMPLETE"],"COVERAGE_STATUS");
if(coverageStatus==="COMPLETE"&&(excluded.length||!compared.length))fail("UNPROVEN_COMPLETE");
if(coverageStatus==="INCOMPLETE"&&!excluded.length)fail("MISSING_INCOMPLETE_EVIDENCE");
const scopeFrom=x=>array(x.coverage,"REF_COVERAGE").map(c=>({console:c.console,status:c.status,reason:c.reason??null}));
return {schemaVersion:dto.schemaVersion,libraryId:expectedLibraryId,beforeGenerationId:dto.before.generationId,afterGenerationId:dto.after.generationId,beforeSnapshotId:beforeId,afterSnapshotId:afterId,counts,entries,scopes:[...compared.map(x=>({console:x.console,comparable:true,before:"complete",after:"complete",reason:x.reason})),...excluded.map(x=>({console:x.console,comparable:false,before:x.beforeStatus,after:x.afterStatus,reason:[x.beforeReason,x.afterReason].filter(Boolean).join(" / ")}))],complete:coverageStatus==="COMPLETE",coverageStatus,limitations,beforeCoverage:scopeFrom(dto.before),afterCoverage:scopeFrom(dto.after),countDenominator:dto.countDenominator,projectionMode:true};
}
window.RM_PROJECTION=Object.freeze({registry,comparison,types:TYPES});
})();