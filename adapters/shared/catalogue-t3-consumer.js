/* Candidate DTO reader for Dev2. Defensive parsing only, never an inventory/comparison engine.
 * No file/network access; may be loaded before T2's t2-catalogue-model.js.
 * Contract 0.2 remains CANDIDATE pending owner approval. */
(function(root) {
"use strict";
const fail=code=>{throw Error("T3_CATALOGUE_"+code)};
const text=(x,code)=>typeof x==="string"&&x.trim()?x:fail(code);
const arr=(x,code)=>Array.isArray(x)?x:fail(code);
const key=(...parts)=>JSON.stringify(parts);
const ordinalKey=(...parts)=>parts.map(v=>String(v.length)+":"+v).join("");
const ordinal=(a,b)=>a<b?-1:a>b?1:0;
const dateUTC=(v)=>{
 text(v,"DATE");
 if(!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z$/.test(v)||Number.isNaN(Date.parse(v)))fail("DATE");
 return v;
};
const integer=(v)=>Number.isSafeInteger(v)&&v>=0;
function inspectLibrary(lib) {
 const id=text(lib.libraryId,"LIBRARY_ID");
 if(typeof lib.indexAccessible!=="boolean")fail("INDEX");
 if(!["FRESH","STALE","UNKNOWN"].includes(lib.freshness)||!["ONLINE","OFFLINE","INACCESSIBLE","UNKNOWN"].includes(lib.currentAvailability))fail("LIBRARY_STATE");
 if(lib.currentAvailability==="UNKNOWN"){
  if(lib.availabilityObservedAtUtc!==null)fail("LIBRARY_DATE");
 }else dateUTC(lib.availabilityObservedAtUtc);
 const platforms=arr(lib.platforms,"PLATFORMS"),sources=arr(lib.sources,"SOURCES"),generations=arr(lib.generations,"GENERATIONS");
 if(!platforms.length||!sources.length||new Set(platforms).size!==platforms.length)fail("PLATFORM_SCOPE");
 for(const p of platforms)text(p,"PLATFORM");
 const expected=new Set(),byPlatform=new Map(platforms.map(p=>[p,new Set()])),sourceIds=new Set();
 for(const src of sources){
  text(src.sourceKey,"SOURCE_ID");text(src.rootKey,"ROOT_ID");
  if(sourceIds.has(src.sourceKey))fail("DUPLICATE_SOURCE");
  sourceIds.add(src.sourceKey);
  const domains=arr(src.domains,"DOMAINS");
  if(!domains.length||new Set(domains).size!==domains.length)fail("DOMAINS");
  for(const domain of domains){
   if(!["ROMS","MEDIAS"].includes(domain))fail("DOMAIN");
   for(const platform of platforms){
    const k=key(platform,src.sourceKey,src.rootKey,domain);
    expected.add(k);byPlatform.get(platform).add(k);
   }
  }
 }
 const bySnapshot=new Map(),genIds=new Set();
 let latest=null;
 for(const gen of generations){
  const sid=text(gen.snapshotId,"SNAPSHOT"),gid=text(gen.generationId,"GENERATION"),at=dateUTC(gen.capturedAtUtc);
  if(bySnapshot.has(sid)||genIds.has(gid))fail("DUPLICATE_GENERATION");
  if(sid===gid)fail("IDENTITY_CONFLATED");
  bySnapshot.set(sid,gen);genIds.add(gid);
  if(!latest||Date.parse(at)>Date.parse(latest.capturedAtUtc)||
     (Date.parse(at)===Date.parse(latest.capturedAtUtc)&&ordinal(sid,latest.snapshotId)>0))latest=gen;
  if(!["complete","partial","failed"].includes(gen.scanStatus)||typeof gen.scopeIsExhaustive!=="boolean")fail("SCAN");
  const proof=arr(gen.coverageProof,"PROOF"),seen=new Set(),completeProof=new Set(),partialProof=new Set();
  for(const part of proof){
   const partStatus=part.status;
   if(!["complete","partial","unobserved"].includes(partStatus))fail("PROOF_STATUS");
   const k=key(part.console,part.sourceKey,part.rootKey,part.domain);
   if(!expected.has(k)||seen.has(k))fail("PROOF_PARTITION");
   seen.add(k);
   if(partStatus==="complete")completeProof.add(k);
   if(partStatus==="partial")partialProof.add(k);
  }
  const coverage=arr(gen.coverage,"COVERAGE"),covKeys=new Set();
  for(const c of coverage){
   if(!byPlatform.has(c.console)||covKeys.has(c.console))fail("COVERAGE_CONSOLE");
   covKeys.add(c.console);
   let proven=true,anySeen=false,anyPartial=false;
   for(const pk of byPlatform.get(c.console)){
    if(!completeProof.has(pk))proven=false;
    if(seen.has(pk))anySeen=true;
    if(partialProof.has(pk))anyPartial=true;
   }
   const calculated=gen.scanStatus==="complete"&&gen.scopeIsExhaustive===true&&proven?
    "complete":anyPartial||anySeen?"partial":"unobserved";
   if(c.status!==calculated)fail(c.status==="complete"?"COVERAGE_FORGED":"COVERAGE_MISMATCH");
  }
  if(covKeys.size!==platforms.length)fail("COVERAGE_MISSING");
  if(typeof gen.countKnown!=="boolean")fail("COUNT_FLAG");
  if(lib.indexAccessible){
   if(!gen.countKnown||!integer(gen.observedUnitCount))fail("SNAPSHOT_COUNT");
  }else if(gen.countKnown||gen.observedUnitCount!==null)fail("INACCESSIBLE_COUNT");
 }
 if(lib.lastVerifiedCompleteRef!==null){
  const verified=bySnapshot.get(text(lib.lastVerifiedCompleteRef,"LAST_VERIFIED"));
  if(!verified)fail("LAST_VERIFIED_ORPHAN");
  if(verified.scanStatus!=="complete"||verified.scopeIsExhaustive!==true||
     verified.coverage.length!==platforms.length||!verified.coverage.every(c=>c.status==="complete"))fail("LAST_VERIFIED_UNPROVEN");
 }
 return {id,latest,bySnapshot,complete:!!latest&&latest.scanStatus==="complete"&&latest.scopeIsExhaustive===true&&
   latest.coverage.length===platforms.length&&latest.coverage.every(c=>c.status==="complete")};
}
function normalize(dto) {
 if(!dto||dto.schemaVersion!=="0.2.0-ui-catalogue-candidate"||dto.readOnly!==true)fail("SCHEMA");
 const registry=dto.registry;
 if(!registry||registry.schemaVersion!=="0.1.0-synthetic"||registry.readOnly!==true)fail("REGISTRY");
 const libs=new Map(),info=new Map();
 for(const lib of arr(registry.libraries,"LIBRARIES")){
  const state=inspectLibrary(lib);
  if(libs.has(state.id))fail("DUPLICATE_LIBRARY");
  libs.set(state.id,lib);info.set(state.id,state);
 }
 const games=new Map(),romScope=new Set(),observedByLib=new Map();
 for(const id of libs.keys())observedByLib.set(id,0);
 for(const g of arr(dto.games,"GAMES")){
  const lid=text(g.libraryId,"LIBRARY"),id=text(g.gameId,"GAME_ID"),platform=text(g.platform,"PLATFORM");
  const lib=libs.get(lid),li=info.get(lid);
  if(!lib||!lib.indexAccessible||!lib.platforms.includes(platform))fail("SOURCE");
  const gameKey=key(lid,platform,id);
  if(games.has(gameKey))fail("DUPLICATE_GAME");
  if(g.title!==null)text(g.title,"TITLE");
  if(!["KNOWN","STALE","PARTIALLY_OBSERVED","CONTRADICTORY","UNKNOWN"].includes(g.quality))fail("QUALITY");
  if(g.availability!==lib.currentAvailability||g.freshness!==lib.freshness)fail("LIBRARY_STATE");
  const prov=arr(g.provenance,"PROVENANCE");
  if(prov.length!==1)fail("PROVENANCE");
  const p=prov[0],snap=li.bySnapshot.get(p.snapshotId);
  if(!snap||p.libraryId!==lid||p.generationId!==snap.generationId||p.observedAtUtc!==snap.capturedAtUtc||p.source!=="SYNTHETIC_SNAPSHOT")fail("REFERENCE");
  if(!li.latest||p.snapshotId!==li.latest.snapshotId)fail("HISTORICAL_SNAPSHOT");
  const covered=li.complete;
  if(g.coverStatus!==(covered?"KNOWN":"UNKNOWN")||g.status!==(covered?"KNOWN":"INCOMPLETE"))fail("COVERAGE_FORGED");
  if(g.quality==="KNOWN"&&(!covered||lib.freshness!=="FRESH"))fail("QUALITY_FORGED");
  if(g.quality==="STALE"&&(lib.freshness!=="STALE"||!covered))fail("QUALITY_FORGED");
  if(g.quality==="PARTIALLY_OBSERVED"&&covered)fail("QUALITY_FORGED");
  if(g.quality==="UNKNOWN"&&lib.freshness!=="UNKNOWN")fail("QUALITY_FORGED");
  const variants=new Set();
  for(const v of arr(g.variants,"VARIANTS")){
   text(v.variantId,"VARIANT_ID");text(v.label,"VARIANT_LABEL");
   if(variants.has(v.variantId))fail("DUPLICATE_VARIANT");
   variants.add(v.variantId);
  }
  const obs=arr(g.observations,"OBSERVATIONS"),localUnits=new Set(),titles=new Set();
  for(const o of obs){
   text(o.romUnitId,"ROM_UNIT");
   if(o.libraryId!==lid||o.snapshotId!==p.snapshotId||o.generationId!==p.generationId||
      !variants.has(o.variantId)||localUnits.has(o.romUnitId))fail("OBSERVATION");
   localUnits.add(o.romUnitId);
   const scope=key(lid,p.snapshotId,o.romUnitId);
   if(romScope.has(scope))fail("CROSS_GAME_DUPLICATE_ROM");
   romScope.add(scope);
   if(o.observationStatus!==(covered?"OBSERVED":"PARTIAL"))fail("OBS_STATUS");
   if(o.discNumber!==undefined&&o.discNumber!==null&&(!integer(o.discNumber)||o.discNumber===0))fail("DISC");
   if(o.observedTitle!==null)titles.add(text(o.observedTitle,"OBSERVED_TITLE"));
   observedByLib.set(lid,observedByLib.get(lid)+1);
  }
  if(!obs.length)fail("EMPTY_GAME");
  if(titles.size>1&&(g.quality!=="CONTRADICTORY"||g.title!==null))fail("TITLE_CONFLICT");
  if(!titles.size&&g.title!==null)fail("TITLE_UNKNOWN");
  if(titles.size===1&&(!titles.has(g.title)||g.quality==="CONTRADICTORY"))fail("TITLE_EVIDENCE");
  games.set(gameKey,g);
 }
 const diagnostics=arr(dto.diagnostics,"DIAGNOSTICS");
 const allProven=[...libs.entries()].every(([id,lib])=>lib.indexAccessible&&info.get(id).complete);
 for(const [id,lib] of libs){
  const l=info.get(id);
  if(lib.indexAccessible&&l.latest){
   const n=observedByLib.get(id);
   if(n>l.latest.observedUnitCount)fail("SNAPSHOT_COUNT");
   if(allProven&&n!==l.latest.observedUnitCount&&!diagnostics.some(d=>d.code==="CATALOG_IDENTITY_AMBIGUOUS"&&d.libraryId===id))fail("SNAPSHOT_COUNT");
  }
 }
 if(typeof dto.countKnown!=="boolean")fail("COUNT_FLAG");
 if(dto.countKnown){
  if(!allProven||diagnostics.some(d=>["CATALOG_COVERAGE_UNPROVEN","CATALOG_IDENTITY_AMBIGUOUS","CATALOG_NO_SNAPSHOT"].includes(d.code))||
     !integer(dto.gameCount)||dto.gameCount!==games.size)fail("COUNT_FORGED");
 }else if(dto.gameCount!==null)fail("COUNT_FORGED");
 return Object.freeze({schemaVersion:dto.schemaVersion,readOnly:true,
  games:[...games.values()].sort((a,b)=>ordinal(ordinalKey(a.libraryId,a.platform,a.gameId),ordinalKey(b.libraryId,b.platform,b.gameId))),
  registry:libs,countKnown:dto.countKnown,gameCount:dto.gameCount,diagnostics});
}
function query(model,filter={}) {
 const search=String(filter.search||"").toLowerCase();
 const items=model.games.filter(g=>(!filter.libraryId||g.libraryId===filter.libraryId)&&
 (!filter.platform||g.platform===filter.platform)&&(!filter.coverStatus||g.coverStatus===filter.coverStatus)&&
 (!filter.status||g.status===filter.status)&&(!filter.availability||g.availability===filter.availability)&&
 (!filter.variantId||g.variants.some(v=>v.variantId===filter.variantId))&&
 (!search||String(g.title||"").toLowerCase().includes(search)||g.gameId.toLowerCase().includes(search)));
 const offset=filter.offset===undefined?0:filter.offset,limit=filter.limit===undefined?100:filter.limit;
 if(!Number.isSafeInteger(offset)||offset<0||!Number.isSafeInteger(limit)||limit<1||limit>1000)fail("PAGE");
 return {total:items.length,offset,limit,items:items.slice(offset,offset+limit)};
}
root.RM_T3_CATALOGUE=Object.freeze({normalize,query});
})(typeof window!=="undefined"?window:globalThis);
