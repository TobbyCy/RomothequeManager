/* Sprint #15: candidate synthetic catalogue read model. Never derive game metadata from ROM bytes. */
(function(){"use strict";
const fail=k=>{throw Error("CATALOGUE_"+k)};
const str=(x,k)=>{if(typeof x!=="string"||!x.trim())fail(k);return x};
const arr=(x,k)=>{if(!Array.isArray(x))fail(k);return x};
const validDate=x=>typeof x==="string"&&!Number.isNaN(Date.parse(x))&&/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z$/.test(x);
function parse(dto,libraries){
if(!dto||dto.schemaVersion!=="0.1.0-ui-catalogue-candidate"||dto.readOnly!==true)fail("SCHEMA");
const byLib=new Map(libraries.map(l=>[l.libraryId,l])),seen=new Set();
const games=arr(dto.games,"GAMES").map(g=>{
const gameId=str(g.gameId,"GAME_ID"),platform=str(g.platform,"PLATFORM"),title=str(g.title,"TITLE");
const key=gameId+"\u0000"+platform;if(seen.has(key))fail("DUPLICATE_GAME");seen.add(key);
if(!["KNOWN","INCOMPLETE","INACCESSIBLE"].includes(g.status))fail("STATUS");
const provenance=arr(g.provenance,"PROVENANCE").map(p=>{
const libraryId=str(p.libraryId,"PROVENANCE_LIBRARY"),lib=byLib.get(libraryId);
if(!lib)fail("UNKNOWN_LIBRARY");
if(!lib.indexAvailable)fail("INACCESSIBLE_INDEX_HAS_GAME");
const snapshotId=str(p.snapshotId,"PROVENANCE_SNAPSHOT");
if(!lib.snapshots.some(s=>s.snapshotId===snapshotId))fail("UNKNOWN_SNAPSHOT");
if(!validDate(p.observedAtUtc))fail("PROVENANCE_DATE");
return {libraryId,snapshotId,observedAtUtc:p.observedAtUtc,source:str(p.source,"PROVENANCE_SOURCE")};
});
if(!provenance.length)fail("GAME_WITHOUT_PROVENANCE");
const variants=arr(g.variants,"VARIANTS").map(v=>({variantId:str(v.variantId,"VARIANT_ID"),label:str(v.label,"VARIANT_LABEL")}));
const observations=arr(g.observations,"OBSERVATIONS").map(o=>{
const libraryId=str(o.libraryId,"OBS_LIBRARY"),romUnitId=str(o.romUnitId,"ROM_UNIT"),variantId=str(o.variantId,"OBS_VARIANT"),snapshotId=str(o.snapshotId,"OBS_SNAPSHOT");
if(!byLib.get(libraryId)?.indexAvailable)fail("OBS_INACCESSIBLE_LIBRARY");
if(!provenance.some(p=>p.libraryId===libraryId&&p.snapshotId===snapshotId))fail("OBS_WITHOUT_PROVENANCE");
if(!variants.some(v=>v.variantId===variantId))fail("OBS_UNKNOWN_VARIANT");
return {libraryId,romUnitId,variantId,snapshotId,observationStatus:o.observationStatus==="PARTIAL"?"PARTIAL":"OBSERVED"};
});
return {gameId,platform,title,status:g.status,provenance,variants,observations,coverStatus:g.coverStatus==="KNOWN"?"KNOWN":"UNKNOWN"};
});
return {games,schemaVersion:dto.schemaVersion,readOnly:true};
}
window.RM_CATALOGUE=Object.freeze({parse});
})();