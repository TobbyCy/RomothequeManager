/* T3 candidate read-only browser consumer. Not installed into T2 (#30). */
(function(root) {
"use strict";
const fail=code=>{throw Error("T3_CATALOGUE_"+code)};
const text=(x,code)=>typeof x==="string"&&x.trim()?x:fail(code);
const arr=(x,code)=>Array.isArray(x)?x:fail(code);
const key=(...parts)=>JSON.stringify(parts);
const ordinal=(a,b)=>a<b?-1:a>b?1:0;
function normalize(dto) {
 if(!dto||dto.schemaVersion!=="0.2.0-ui-catalogue-candidate"||dto.readOnly!==true)fail("SCHEMA");
 const registry=dto.registry;
 if(!registry||registry.schemaVersion!=="0.1.0-synthetic"||registry.readOnly!==true)fail("REGISTRY");
 const libs=new Map();
 for(const lib of arr(registry.libraries,"LIBRARIES")){
  const id=text(lib.libraryId,"LIBRARY_ID");
  if(libs.has(id))fail("DUPLICATE_LIBRARY");
  arr(lib.generations,"GENERATIONS");arr(lib.sources,"SOURCES");arr(lib.platforms,"PLATFORMS");
  libs.set(id,lib);
 }
 const games=new Map();
 for(const game of arr(dto.games,"GAMES")){
  const lid=text(game.libraryId,"LIBRARY"),id=text(game.gameId,"GAME_ID"),platform=text(game.platform,"PLATFORM");
  const lib=libs.get(lid);
  if(!lib||!lib.indexAccessible||!lib.platforms.includes(platform))fail("SOURCE");
  const k=key(lid,platform,id);
  if(games.has(k))fail("DUPLICATE_GAME");
  if(game.title!==null)text(game.title,"TITLE");
  if(!["KNOWN","STALE","PARTIALLY_OBSERVED","CONTRADICTORY","UNKNOWN"].includes(game.quality))fail("QUALITY");
  const prov=arr(game.provenance,"PROVENANCE");
  if(prov.length!==1)fail("PROVENANCE");
  const p=prov[0],snap=lib.generations.find(s=>s.snapshotId===p.snapshotId);
  if(!snap||p.libraryId!==lid||p.generationId!==snap.generationId||p.observedAtUtc!==snap.capturedAtUtc||p.source!=="SYNTHETIC_SNAPSHOT")fail("REFERENCE");
  const expected=new Set();
  for(const platformId of lib.platforms)for(const source of lib.sources)for(const domain of source.domains)expected.add(key(platformId,source.sourceKey,source.rootKey,domain));
  const proven=new Set();
  for(const part of arr(snap.coverageProof,"PROOF")){
   const pk=key(part.console,part.sourceKey,part.rootKey,part.domain);
   if(!expected.has(pk)||proven.has(pk))fail("PROOF_PARTITION");
   if(part.status==="complete")proven.add(pk);
  }
  const covered=snap.scanStatus==="complete"&&snap.scopeIsExhaustive===true&&proven.size===expected.size&&
   arr(snap.coverage,"COVERAGE").length===lib.platforms.length&&snap.coverage.every(c=>c.status==="complete");
  if(game.coverStatus!==(covered?"KNOWN":"UNKNOWN")||game.status!==(covered?"KNOWN":"INCOMPLETE"))fail("COVERAGE_FORGED");
  if(game.quality==="KNOWN"&&(!covered||lib.freshness!=="FRESH"))fail("QUALITY_FORGED");
  const variants=new Set();
  for(const v of arr(game.variants,"VARIANTS")){text(v.variantId,"VARIANT_ID");if(variants.has(v.variantId))fail("DUPLICATE_VARIANT");variants.add(v.variantId)}
  const units=new Set();
  for(const o of arr(game.observations,"OBSERVATIONS")){
   text(o.romUnitId,"ROM_UNIT");
   if(o.libraryId!==lid||o.snapshotId!==p.snapshotId||o.generationId!==p.generationId||!variants.has(o.variantId)||units.has(o.romUnitId))fail("OBSERVATION");
   if(o.observationStatus!==(covered?"OBSERVED":"PARTIAL"))fail("OBS_STATUS");
   units.add(o.romUnitId);
  }
  if(!units.size)fail("EMPTY_GAME");
  games.set(k,game);
 }
 const countKnown=dto.countKnown;
 if(typeof countKnown!=="boolean")fail("COUNT_FLAG");
 if(countKnown) {
  if(!Number.isInteger(dto.gameCount)||dto.gameCount!==games.size||[...libs.values()].some(l=>!l.indexAccessible)||[...games.values()].some(g=>g.coverStatus!=="KNOWN"))fail("COUNT_FORGED");
 }else if(dto.gameCount!==null)fail("COUNT_FORGED");
 return Object.freeze({schemaVersion:dto.schemaVersion,readOnly:true,games:[...games.values()].sort((a,b)=>
  ordinal(key(a.libraryId,a.platform,a.gameId),key(b.libraryId,b.platform,b.gameId))),registry:libs,countKnown,gameCount:dto.gameCount});
}
function query(model,filter={}) {
 const search=String(filter.search||"").toLowerCase();
 const items=model.games.filter(g=>(!filter.libraryId||g.libraryId===filter.libraryId)&&
 (!filter.platform||g.platform===filter.platform)&&(!filter.coverStatus||g.coverStatus===filter.coverStatus)&&
 (!filter.status||g.status===filter.status)&&(!filter.availability||g.availability===filter.availability)&&
 (!filter.variantId||g.variants.some(v=>v.variantId===filter.variantId))&&
 (!search||String(g.title||"").toLowerCase().includes(search)||g.gameId.toLowerCase().includes(search)));
 const offset=filter.offset===undefined?0:filter.offset,limit=filter.limit===undefined?100:filter.limit;
 if(!Number.isInteger(offset)||offset<0||!Number.isInteger(limit)||limit<1||limit>1000)fail("PAGE");
 return {total:items.length,offset,limit,items:items.slice(offset,offset+limit)};
}
root.RM_T3_CATALOGUE=Object.freeze({normalize,query});
})(typeof window!=="undefined"?window:globalThis);
