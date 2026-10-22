/* Candidate T1-shaped synthetic DTO. Hand-authored; NOT a PowerShell export. */
(function(global){"use strict";
const prov=(libraryId,snapshotId,generationId,observedAtUtc)=>({libraryId,snapshotId,generationId,observedAtUtc});
const unit=(romUnitId,variantId,discNumber,provenance)=>({romUnitId,variantId,discNumber,provenance});
const a=prov("lib-arcade","arcade-002","arcade-002","2026-10-10T09:30:00Z"),p=prov("lib-pocket","pocket-002","pocket-002","2026-09-01T12:00:00Z");
const game=(libraryId,platform,gameId,title,quality,availability,freshness,coverageStatus,variants,units,provenance)=>({libraryId,platform,gameId,title,quality,availability,freshness,coverageStatus,variants,units,provenance});
global.RM_T2_FIXTURE=Object.freeze({
 schemaVersion:"0.1.0-ui-catalogue-t1-candidate",readOnly:true,
 libraries:global.RM_FIXTURES.libraries.map(l=>({libraryId:l.libraryId})),
 games:[
 game("lib-arcade","GBA","game-mario","Super Plombier Advance","KNOWN","ONLINE","FRESH","COMPLETE",["var-eu"],[unit("unit-mario","var-eu",null,a)],a),
 game("lib-arcade","PS1","game-orbit",null,"CONTRADICTORY","ONLINE","FRESH","COMPLETE",["var-eu","var-us"],[unit("unit-conflict","var-us",1,a)],a),
 game("lib-arcade","GBA","game-zelda",null,"KNOWN","ONLINE","FRESH","COMPLETE",["var-eu"],[unit("unit-zelda","var-eu",null,a)],a),
 game("lib-pocket","GBA","game-pocket-1","Pocket Adventures","STALE","OFFLINE","STALE","COMPLETE",["var-eu"],[unit("pocket-01","var-eu",null,p)],p),
 game("lib-pocket","GBA","game-pocket-2","Star Circuit","PARTIALLY_OBSERVED","OFFLINE","STALE","INCOMPLETE",["var-jp"],[unit("pocket-02","var-jp",null,p)],p)
 ],
 diagnostics:[{code:"CATALOG_INDEX_INACCESSIBLE",libraryId:"lib-optical",snapshotId:null},{code:"CATALOG_ZERO_NOT_EMPTY_PROOF",libraryId:"lib-unknown",snapshotId:"unknown-002"},{code:"CATALOG_TITLE_UNKNOWN",libraryId:"lib-arcade",snapshotId:"arcade-002"},{code:"CATALOG_TITLE_CONFLICT",libraryId:"lib-arcade",snapshotId:"arcade-002"}],
 countKnown:false,gameCount:null,
 limitations:["SYNTHETIC_ONLY","LATEST_SNAPSHOT_PER_LIBRARY","NOT_PHYSICAL_ROM_BYTES","NO_INFERRED_IDENTITY"]
});
})(typeof window!=="undefined"?window:globalThis);
