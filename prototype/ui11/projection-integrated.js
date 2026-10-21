/* UI #15 - hand-authored synthetic DTO sample, NOT a PowerShell runtime export.
   For offline UI exercise only: change index.html script projection-data.js to projection-sample.js.
   Replace this with a real synthetic PS5.1 export before parity sign-off. */
(function(){"use strict";
const at="2026-10-20T12:00:00Z";
const gen=(id,generationId,day)=>({snapshotId:id,scanId:"scan-"+id,generationId,capturedAtUtc:day,scanStatus:"complete",scopeIsExhaustive:true,coverage:[{console:"GBA",status:"complete",reason:"ALL_DECLARED_PARTITIONS_PROVEN"}],coverageProof:[{console:"GBA",sourceKey:"source-demo",rootKey:"root-demo",domain:"ROMS",status:"complete"}],countKnown:true,observedUnitCount:1});
const a=gen("snap-a","gen-a","2026-10-19T12:00:00Z"),b=gen("snap-b","gen-b",at);
const library={libraryId:"lib-demo",name:"Bibliothèque synthétique",platforms:["GBA"],sources:[{sourceKey:"source-demo",rootKey:"root-demo",domains:["ROMS"]}],currentAvailability:"OFFLINE",availabilityObservedAtUtc:at,freshness:"STALE",indexAccessible:true,lastVerifiedCompleteRef:"snap-b",lastObservedAtUtc:at,generations:[a,b]};
const inaccessible={libraryId:"lib-closed",name:"Source non consultable",platforms:["PS1"],sources:[{sourceKey:"source-closed",rootKey:"root-closed",domains:["ROMS"]}],currentAvailability:"INACCESSIBLE",availabilityObservedAtUtc:at,freshness:"UNKNOWN",indexAccessible:false,lastVerifiedCompleteRef:null,lastObservedAtUtc:null,generations:[]};
const registry={schemaVersion:"0.1.0-synthetic",readOnly:true,libraries:[library,inaccessible]};
const comparison={schemaVersion:"0.1.0-synthetic",readOnly:true,library:{libraryId:"lib-demo",name:library.name,platforms:["GBA"],currentAvailability:"OFFLINE",availabilityObservedAtUtc:at,freshness:"STALE",indexAccessible:true,lastVerifiedCompleteRef:"snap-b"},before:a,after:b,scopeCompared:[{console:"GBA",status:"complete",reason:"PROVEN_IN_A_AND_B"}],scopeNotComparable:[],coverageStatus:"COMPLETE",counts:{ADDED:0,CHANGED:0,UNCHANGED:1,NOT_OBSERVED_IN_B_COVERED_SCOPE:0,UNKNOWN:0,NOT_COMPARABLE:0},entries:[{romUnitId:"unit-demo",gameId:"game-demo",variantId:"variant-eu",console:"GBA",classification:"UNCHANGED",reason:"OBSERVATION_DIGEST_EQUAL",beforeDigest:"a".repeat(64),afterDigest:"a".repeat(64),sourceRefs:{beforeGenerationId:"gen-a",afterGenerationId:"gen-b"},changedFields:[],comparedFields:[]}],countDenominator:1,limitations:["HISTORICAL_OBSERVATIONS_NOT_PHYSICAL_ROM_BYTES","COUNTS_ONLY_OBSERVED_UNION_A_B","CURRENT_SOURCE_NOT_CONFIRMED_ONLINE","HISTORICAL_FRESHNESS_NOT_CURRENT"]};
const catalogue={schemaVersion:"0.1.0-ui-catalogue-candidate",readOnly:true,games:[{gameId:"game-demo",platform:"GBA",title:"Explorateurs imaginaires",status:"KNOWN",coverStatus:"UNKNOWN",provenance:[{libraryId:"lib-demo",snapshotId:"snap-b",observedAtUtc:at,source:"Catalogue synthétique manuel"}],variants:[{variantId:"variant-eu",label:"Édition européenne"}],observations:[{libraryId:"lib-demo",snapshotId:"snap-b",romUnitId:"unit-demo",variantId:"variant-eu",observationStatus:"OBSERVED"}]}]};
window.RM_PROJECTION_DATA={registry,comparisons:[comparison],catalogue};
})();

/* SPRINT #17 CANDIDATE EXTENSION — hand authored, NEVER PS5.1-exported. */
(function(){"use strict";
const p=window.RM_PROJECTION_DATA,at="2026-10-20T12:00:00Z",prior="2026-10-19T12:00:00Z";
const proof=(console,status)=>({console,sourceKey:"synthetic-source",rootKey:"synthetic-root",domain:"ROMS",status});
const snap=(id,gen,date,scan,coverage,proofs,count)=>({snapshotId:id,generationId:gen,scanId:"scan-"+id,capturedAtUtc:date,scanStatus:scan,scopeIsExhaustive:true,coverage,coverageProof:proofs,countKnown:true,observedUnitCount:count});
const ca=[{console:"GBA",status:"complete",reason:"ALL_DECLARED_PARTITIONS_PROVEN"},{console:"PS1",status:"complete",reason:"ALL_DECLARED_PARTITIONS_PROVEN"}];
const cb=[ca[0],{console:"PS1",status:"partial",reason:"SCAN_NOT_COMPLETE"}];
const pa=[proof("GBA","complete"),proof("PS1","complete")],pb=[proof("GBA","complete"),proof("PS1","partial")];
const a=snap("mixed-a","mixed-gen-a",prior,"complete",ca,pa,5),b=snap("mixed-b","mixed-gen-b",at,"complete",cb,pb,5);
const mixed={libraryId:"lib-mixed",name:"Bibliothèque multi-états",platforms:["GBA","PS1"],sources:[{sourceKey:"synthetic-source",rootKey:"synthetic-root",domains:["ROMS"]}],currentAvailability:"ONLINE",availabilityObservedAtUtc:at,freshness:"FRESH",indexAccessible:true,lastVerifiedCompleteRef:"mixed-a",lastObservedAtUtc:at,generations:[a,b]};
p.registry.libraries.push(mixed);
const ref={beforeGenerationId:a.generationId,afterGenerationId:b.generationId};
const entry=(id,classification,reason,console="GBA")=>({romUnitId:id,gameId:"game-"+id,variantId:"variant-"+id,console,classification,reason,beforeDigest:null,afterDigest:null,changedFields:[],comparedFields:[],sourceRefs:ref});
const entries=[
entry("unit-added","ADDED","ABSENT_FROM_VERIFIED_PRIOR_SCOPE"),
entry("unit-changed","CHANGED","OBSERVATION_DIGEST_DIFFERENT"),
entry("unit-unchanged","UNCHANGED","OBSERVATION_DIGEST_EQUAL"),
entry("unit-not-observed","NOT_OBSERVED_IN_B_COVERED_SCOPE","ABSENT_FROM_VERIFIED_CURRENT_SCOPE"),
entry("unit-unknown","UNKNOWN","SCOPE_OR_SCAN_INCOMPLETE","PS1"),
entry("unit-conflict","NOT_COMPARABLE","IDENTITY_OWNER_CONFLICT")
];
const counts={ADDED:1,CHANGED:1,UNCHANGED:1,NOT_OBSERVED_IN_B_COVERED_SCOPE:1,UNKNOWN:1,NOT_COMPARABLE:1};
p.comparisons.push({schemaVersion:"0.1.0-synthetic",readOnly:true,library:{libraryId:mixed.libraryId,name:mixed.name,platforms:mixed.platforms,currentAvailability:mixed.currentAvailability,availabilityObservedAtUtc:at,freshness:mixed.freshness,indexAccessible:true,lastVerifiedCompleteRef:"mixed-a"},before:a,after:b,scopeCompared:[{console:"GBA",status:"complete",reason:"PROVEN_IN_A_AND_B"}],scopeNotComparable:[{console:"PS1",status:"INCOMPLETE",beforeStatus:"complete",afterStatus:"partial",beforeReason:"ALL_DECLARED_PARTITIONS_PROVEN",afterReason:"SCAN_NOT_COMPLETE"}],coverageStatus:"INCOMPLETE",counts,entries,countDenominator:6,limitations:["HISTORICAL_OBSERVATIONS_NOT_PHYSICAL_ROM_BYTES","COUNTS_ONLY_OBSERVED_UNION_A_B","SCOPE_INTERSECTION_NOT_FULLY_PROVEN"]});
const za=snap("zero-a","zero-gen-a",prior,"partial",[{console:"GBA",status:"partial",reason:"SCAN_NOT_COMPLETE"}],[proof("GBA","partial")],0);
const zb=snap("zero-b","zero-gen-b",at,"partial",[{console:"GBA",status:"partial",reason:"SCAN_NOT_COMPLETE"}],[proof("GBA","partial")],0);
const zero={libraryId:"lib-zero",name:"Zéro non probant",platforms:["GBA"],sources:[{sourceKey:"synthetic-source",rootKey:"synthetic-root",domains:["ROMS"]}],currentAvailability:"UNKNOWN",availabilityObservedAtUtc:null,freshness:"UNKNOWN",indexAccessible:true,lastVerifiedCompleteRef:null,lastObservedAtUtc:at,generations:[za,zb]};
p.registry.libraries.push(zero);
p.comparisons.push({schemaVersion:"0.1.0-synthetic",readOnly:true,library:{libraryId:zero.libraryId,name:zero.name,platforms:zero.platforms,currentAvailability:"UNKNOWN",availabilityObservedAtUtc:null,freshness:"UNKNOWN",indexAccessible:true,lastVerifiedCompleteRef:null},before:za,after:zb,scopeCompared:[],scopeNotComparable:[{console:"GBA",status:"INCOMPLETE",beforeStatus:"partial",afterStatus:"partial",beforeReason:"SCAN_NOT_COMPLETE",afterReason:"SCAN_NOT_COMPLETE"}],coverageStatus:"INCOMPLETE",counts:{ADDED:0,CHANGED:0,UNCHANGED:0,NOT_OBSERVED_IN_B_COVERED_SCOPE:0,UNKNOWN:0,NOT_COMPARABLE:0},entries:[],countDenominator:0,limitations:["ZERO_OBSERVED_UNITS_NOT_PROOF_OF_EMPTY_LIBRARY","SCOPE_INTERSECTION_NOT_FULLY_PROVEN","HISTORICAL_OBSERVATIONS_NOT_PHYSICAL_ROM_BYTES"]});
p.catalogue.games.push({gameId:"game-unknown",platform:"PS1",title:"Mystère synthétique",status:"INCOMPLETE",coverStatus:"UNKNOWN",provenance:[{libraryId:"lib-mixed",snapshotId:"mixed-b",observedAtUtc:at,source:"Fixture candidate UI #17"}],variants:[{variantId:"variant-unknown",label:"Variante non complète"}],observations:[{libraryId:"lib-mixed",snapshotId:"mixed-b",romUnitId:"unit-unknown",variantId:"variant-unknown",observationStatus:"PARTIAL"}]});
})();
