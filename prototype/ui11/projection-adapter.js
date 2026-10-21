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
const ids=new Set();return array(dto.libraries,"LIBRARIES").map(l=>{
text(l.libraryId,"LIBRARY_ID");text(l.name,"LIBRARY_NAME");if(ids.has(l.libraryId))fail("DUPLICATE_LIBRARY");ids.add(l.libraryId);
if(typeof l.indexAccessible!=="boolean")fail("INDEX_ACCESSIBLE");
const platforms=array(l.platforms,"PLATFORMS"),sources=array(l.sources,"SOURCES"),expected=new Set();
for(const platform of platforms){text(platform,"PLATFORM");for(const src of sources){text(src.sourceKey,"SOURCE_KEY");text(src.rootKey,"ROOT_KEY");for(const domain of array(src.domains,"SOURCE_DOMAINS")){enumOf(domain,["ROMS","MEDIAS"],"DOMAIN");const k=JSON.stringify([platform,src.sourceKey,src.rootKey,domain]);if(expected.has(k))fail("DUPLICATE_DECLARED_PARTITION");expected.add(k)}}}
const snapIds=new Set(),genIds=new Set();
const generations=array(l.generations,"GENERATIONS").map(g=>{
text(g.snapshotId,"SNAPSHOT_ID");text(g.generationId,"GENERATION_ID");text(g.capturedAtUtc,"CAPTURED_AT");if(snapIds.has(g.snapshotId)||genIds.has(g.generationId))fail("DUPLICATE_SNAPSHOT_OR_GENERATION");snapIds.add(g.snapshotId);genIds.add(g.generationId);
if(!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z$/.test(g.capturedAtUtc)||Number.isNaN(Date.parse(g.capturedAtUtc)))fail("CAPTURED_DATE");
const scanStatus=enumOf(g.scanStatus,["complete","partial","failed"],"SCAN");
if(typeof g.scopeIsExhaustive!=="boolean")fail("SCOPE_EXHAUSTIVE_FLAG");
const coverage=array(g.coverage,"COVERAGE"),proof=array(g.coverageProof,"COVERAGE_PROOF");
const proven=new Map();for(const p of proof){text(p.console,"PROOF_CONSOLE");text(p.sourceKey,"PROOF_SOURCE");text(p.rootKey,"PROOF_ROOT");enumOf(p.domain,["ROMS","MEDIAS"],"PROOF_DOMAIN");enumOf(p.status,["complete","partial","unobserved"],"PROOF_STATUS");const k=JSON.stringify([p.console,p.sourceKey,p.rootKey,p.domain]);if(!expected.has(k))fail("UNDECLARED_PROOF_PARTITION");if(proven.has(k))fail("DUPLICATE_PROOF_PARTITION");proven.set(k,p.status)}
for(const v of coverage){text(v.console,"COVERAGE_CONSOLE");enumOf(v.status,["complete","partial","unobserved"],"COVERAGE_STATUS");if(v.status==="complete"&&(scanStatus!=="complete"||!g.scopeIsExhaustive||![...expected].filter(k=>JSON.parse(k)[0]===v.console).every(k=>proven.get(k)==="complete")))fail("UNPROVEN_SCOPE")}
if(typeof g.countKnown!=="boolean")fail("COUNT_KNOWN_FLAG");
if(g.countKnown!==l.indexAccessible)fail("COUNT_INDEX_CONFLICT");
if(g.countKnown&& !l.indexAccessible)fail("INACCESSIBLE_COUNT");
const observedUnitCount=g.countKnown?integer(g.observedUnitCount,"OBSERVED_UNITS"):(g.observedUnitCount===null?null:fail("UNKNOWN_COUNT_NOT_NULL"));
return {snapshotId:g.snapshotId,generationId:g.generationId,capturedAtUtc:g.capturedAtUtc,scanStatus,coverage,coverageProof:proof,scopeIsExhaustive:g.scopeIsExhaustive,countKnown:g.countKnown,observedUnitCount};
});
const availability=enumOf(l.currentAvailability,["ONLINE","OFFLINE","INACCESSIBLE","UNKNOWN"],"AVAILABILITY");
const freshness=enumOf(l.freshness,["FRESH","STALE","UNKNOWN"],"FRESHNESS");
if(l.lastVerifiedCompleteRef!==null&&l.lastVerifiedCompleteRef!==undefined){const last=generations.find(g=>g.snapshotId===l.lastVerifiedCompleteRef);if(!last||last.scanStatus!=="complete"||last.coverage.some(x=>x.status!=="complete"))fail("LAST_VERIFIED_REF_UNPROVEN")}
return {libraryId:l.libraryId,name:l.name,description:"Projection synthétique du noyau · lecture seule",platforms,availability,freshness,observedAtUtc:l.availabilityObservedAtUtc??null,indexAvailable:l.indexAccessible,lastVerifiedCompleteRef:l.lastVerifiedCompleteRef??null,snapshots:generations,projectionMode:true};
});
}
function comparison(dto,expectedLibraryId,libraryCard){
if(!dto||dto.schemaVersion!=="0.1.0-synthetic"||dto.readOnly!==true)fail("COMPARISON_VERSION_OR_READONLY");
if(!dto.library||dto.library.libraryId!==expectedLibraryId)fail("COMPARISON_LIBRARY");
if(!dto.before||!dto.after)fail("COMPARISON_REFS");
const beforeId=text(dto.before.snapshotId,"BEFORE_SNAPSHOT_ID"),afterId=text(dto.after.snapshotId,"AFTER_SNAPSHOT_ID");
if(beforeId===afterId)fail("COMPARISON_SAME_SNAPSHOT");
if(dto.before.generationId===dto.after.generationId)fail("COMPARISON_SAME_GENERATION");
if(Date.parse(dto.before.capturedAtUtc)>Date.parse(dto.after.capturedAtUtc))fail("COMPARISON_DATE_ORDER");
if(libraryCard){if(libraryCard.libraryId!==expectedLibraryId||!libraryCard.indexAvailable)fail("COMPARISON_INACCESSIBLE_LIBRARY");for(const [ref,kind] of [[dto.before,"BEFORE"],[dto.after,"AFTER"]]){const s=libraryCard.snapshots.find(x=>x.snapshotId===ref.snapshotId);if(!s||s.generationId!==ref.generationId||s.capturedAtUtc!==ref.capturedAtUtc||s.scanStatus!==ref.scanStatus)fail("COMPARISON_"+kind+"_REGISTRY_MISMATCH")}}

const counts={};for(const type of TYPES)counts[type]=integer(dto.counts?.[type],"CLASS_"+type);
const entries=array(dto.entries,"ENTRIES");
if(entries.length!==integer(dto.countDenominator,"DENOMINATOR"))fail("DENOMINATOR_MISMATCH");
const actual=Object.fromEntries(TYPES.map(t=>[t,0]));
for(const e of entries){enumOf(e.classification,TYPES,"CLASSIFICATION");actual[e.classification]++;}
for(const t of TYPES)if(counts[t]!==actual[t])fail("COUNT_MISMATCH_"+t);
const compared=array(dto.scopeCompared,"SCOPE_COMPARED"),excluded=array(dto.scopeNotComparable,"SCOPE_EXCLUDED"),limitations=array(dto.limitations,"LIMITATIONS");
const coverageStatus=enumOf(dto.coverageStatus,["COMPLETE","INCOMPLETE"],"COVERAGE_STATUS");
const seenScopes=new Set();
for(const scope of [...compared,...excluded]){text(scope.console,"SCOPE_CONSOLE");if(seenScopes.has(scope.console))fail("DUPLICATE_SCOPE");seenScopes.add(scope.console)}
for(const scope of compared){if(scope.status!=="complete")fail("COMPARED_SCOPE_NOT_COMPLETE");const a=dto.before.coverage?.find(x=>x.console===scope.console),b=dto.after.coverage?.find(x=>x.console===scope.console);if(!a||!b||a.status!=="complete"||b.status!=="complete")fail("COMPARED_SCOPE_UNPROVEN")}
if(libraryCard&&seenScopes.size!==libraryCard.platforms.length)fail("SCOPE_PLATFORM_COUNT_MISMATCH");
for(const limitation of limitations)text(limitation,"LIMITATION");

if(coverageStatus==="COMPLETE"&&(excluded.length||!compared.length))fail("UNPROVEN_COMPLETE");
if(coverageStatus==="INCOMPLETE"&&!excluded.length)fail("MISSING_INCOMPLETE_EVIDENCE");
const scopeFrom=x=>array(x.coverage,"REF_COVERAGE").map(c=>({console:c.console,status:c.status,reason:c.reason??null}));
return {schemaVersion:dto.schemaVersion,libraryId:expectedLibraryId,beforeGenerationId:dto.before.generationId,afterGenerationId:dto.after.generationId,beforeSnapshotId:beforeId,afterSnapshotId:afterId,counts,entries,scopes:[...compared.map(x=>({console:x.console,comparable:true,before:"complete",after:"complete",reason:x.reason})),...excluded.map(x=>({console:x.console,comparable:false,before:x.beforeStatus,after:x.afterStatus,reason:[x.beforeReason,x.afterReason].filter(Boolean).join(" / ")}))],complete:coverageStatus==="COMPLETE",coverageStatus,limitations,beforeCoverage:scopeFrom(dto.before),afterCoverage:scopeFrom(dto.after),countDenominator:dto.countDenominator,projectionMode:true};
}
window.RM_PROJECTION=Object.freeze({registry,comparison,types:TYPES});
})();