/* T3-B — strict read-only consumer of Dev1 T1 candidate, not a third catalogue DTO. */
(function(global){"use strict";
const VERSION="0.1.0-ui-catalogue-t1-candidate";
const fail=c=>{throw Error("T1_UI_"+c)};
const str=(x,c)=>typeof x==="string"&&x.trim()?x:fail(c);
const list=(x,c)=>Array.isArray(x)?x:fail(c);
const state=(x,values,c)=>values.includes(x)?x:fail(c);
const date=(x,c)=>{str(x,c);if(!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z$/.test(x)||Number.isNaN(Date.parse(x)))fail(c);return x};
const key=(...parts)=>JSON.stringify(parts);

const T3_VERSION="0.2.0-ui-catalogue-candidate";
/* Independently enforce the source-state, title and disc invariants of the
   PowerShell validator before binding candidate data to the existing UI. */
function normalizeT3(dto){
 if(!global.RM_T3_CATALOGUE||!global.RM_PROJECTION)fail("T3_DEPENDENCY_MISSING");
 const base=global.RM_T3_CATALOGUE.normalize(dto);
 const cards=global.RM_PROJECTION.registry(dto.registry);
 const refs=new Map(cards.map(c=>[c.libraryId,c]));
 const normalized=[];
 for(const g of base.games){
  const l=refs.get(g.libraryId);
  if(!l||g.availability!==l.availability||g.freshness!==l.freshness)fail("T3_LIBRARY_STATE");
  if(g.quality==="STALE"&&g.freshness!=="STALE")fail("T3_STALE_QUALITY");
  if(g.quality==="UNKNOWN"&&g.freshness!=="UNKNOWN")fail("T3_UNKNOWN_QUALITY");
  if(g.quality==="PARTIALLY_OBSERVED"&&g.coverStatus!=="UNKNOWN")fail("T3_PARTIAL_QUALITY");
  if(g.quality==="CONTRADICTORY"&&g.title!==null)fail("T3_CONTRADICTORY_TITLE");
  const variants=new Set();
  for(const v of g.variants){
   str(v.variantId,"T3_VARIANT_ID");str(v.label,"T3_VARIANT_LABEL");
   if(variants.has(v.variantId))fail("T3_DUPLICATE_VARIANT");variants.add(v.variantId);
  }
  const names=new Set();
  for(const o of g.observations){
   if(o.discNumber!==undefined&&o.discNumber!==null&&(!Number.isSafeInteger(o.discNumber)||o.discNumber<1))fail("T3_DISC_NUMBER");
   if(o.observedTitle!==null&&o.observedTitle!==undefined)names.add(str(o.observedTitle,"T3_OBSERVED_TITLE"));
  }
  if(names.size>1&&(g.quality!=="CONTRADICTORY"||g.title!==null))fail("T3_TITLE_CONFLICT");
  if(names.size===0&&g.title!==null)fail("T3_TITLE_UNKNOWN");
  if(names.size===1&&(!names.has(g.title)||g.quality==="CONTRADICTORY"))fail("T3_TITLE_EVIDENCE");
  const p=g.provenance[0];
  normalized.push({...g,identity:key(g.libraryId,g.platform,g.gameId),
   coverageStatus:g.coverStatus==="KNOWN"?"COMPLETE":"INCOMPLETE",
   provenance:{...p},variants:g.variants.map(v=>v.variantId),
   units:g.observations.map(o=>({romUnitId:o.romUnitId,variantId:o.variantId,
    discNumber:o.discNumber??null,observedTitle:o.observedTitle??null,
    provenance:{libraryId:o.libraryId,snapshotId:o.snapshotId,
      generationId:o.generationId,observedAtUtc:p.observedAtUtc}}))});
 }
 for(const d of list(dto.diagnostics,"T3_DIAGNOSTICS")){
  str(d.code,"T3_DIAGNOSTIC_CODE");
  if(d.libraryId!=null&&!refs.has(d.libraryId))fail("T3_DIAGNOSTIC_LIBRARY");
 }
 return Object.freeze({schemaVersion:T3_VERSION,readOnly:true,games:normalized,
  registry:refs,diagnostics:dto.diagnostics,
  limitations:["CANDIDATE_NON_NORMATIVE","SYNTHETIC_ONLY","RUNTIME_PARITY_NOT_VERIFIED"],
  countKnown:base.countKnown,gameCount:base.gameCount});
}

function normalize(dto,registry){
 if(dto&&dto.schemaVersion===T3_VERSION)return normalizeT3(dto);
 if(!dto||dto.schemaVersion!==VERSION||dto.readOnly!==true)fail("SCHEMA");
 const libMap=new Map();
 for(const l of list(registry,"REGISTRY")){str(l.libraryId,"LIB_ID");if(libMap.has(l.libraryId))fail("DUPLICATE_LIB");libMap.set(l.libraryId,l)}
 const seen=new Set(),games=[];
 for(const g of list(dto.games,"GAMES")){
  const libraryId=str(g.libraryId,"GAME_LIBRARY"),platform=str(g.platform,"PLATFORM"),gameId=str(g.gameId,"GAME_ID"),id=key(libraryId,platform,gameId);
  if(seen.has(id))fail("DUPLICATE_GAME");seen.add(id);
  const lib=libMap.get(libraryId);if(!lib||!lib.indexAvailable)fail("INDEX_UNAVAILABLE");
  if(!lib.platforms.includes(platform))fail("PLATFORM_UNDECLARED");
  state(g.quality,["KNOWN","STALE","PARTIALLY_OBSERVED","CONTRADICTORY"],"QUALITY");
  state(g.availability,["ONLINE","OFFLINE","INACCESSIBLE","UNKNOWN"],"AVAILABILITY");
  state(g.freshness,["FRESH","STALE","UNKNOWN"],"FRESHNESS");
  state(g.coverageStatus,["COMPLETE","INCOMPLETE"],"COVERAGE");
  if(g.availability!==lib.availability||g.freshness!==lib.freshness)fail("LIBRARY_STATE_MISMATCH");
  if(g.quality==="KNOWN"&&(g.coverageStatus!=="COMPLETE"||g.freshness==="STALE"))fail("KNOWN_UNPROVEN");
  if(g.quality==="STALE"&&g.freshness!=="STALE")fail("STALE_MISMATCH");
  if(g.quality==="PARTIALLY_OBSERVED"&&g.coverageStatus!=="INCOMPLETE")fail("PARTIAL_MISMATCH");
  if(g.title!==null)str(g.title,"TITLE");
  if(g.quality==="CONTRADICTORY"&&g.title!==null)fail("CONTRADICTORY_TITLE");
  const p=g.provenance;if(!p||p.libraryId!==libraryId)fail("PROVENANCE_LIBRARY");
  const snap=lib.snapshots.find(s=>(s.snapshotId??s.generationId)===p.snapshotId);
  if(!snap||snap.generationId!==p.generationId||snap.capturedAtUtc!==date(p.observedAtUtc,"OBSERVED_AT"))fail("PROVENANCE_SNAPSHOT");
  const variants=list(g.variants,"VARIANTS"),variantIds=new Set();
  for(const v of variants){str(v,"VARIANT");if(variantIds.has(v))fail("DUPLICATE_VARIANT");variantIds.add(v)}
  const unitIds=new Set(),units=list(g.units,"UNITS");
  for(const u of units){
   str(u.romUnitId,"ROM_ID");str(u.variantId,"UNIT_VARIANT");
   if(unitIds.has(u.romUnitId)||!variantIds.has(u.variantId))fail("UNIT_IDENTITY");unitIds.add(u.romUnitId);
   if(u.discNumber!==null&&u.discNumber!==undefined&&(!Number.isSafeInteger(u.discNumber)||u.discNumber<1))fail("DISC_NUMBER");
   const up=u.provenance;if(!up||up.libraryId!==libraryId||up.snapshotId!==p.snapshotId||up.generationId!==p.generationId||up.observedAtUtc!==p.observedAtUtc)fail("UNIT_PROVENANCE");
  }
  if(!units.length)fail("EMPTY_GAME");
  games.push({...g,identity:id});
 }
 const diagnostics=list(dto.diagnostics,"DIAGNOSTICS"),limitations=list(dto.limitations,"LIMITATIONS");
 for(const d of diagnostics){str(d.code,"DIAGNOSTIC_CODE");if(d.libraryId!==null&&d.libraryId!==undefined&&!libMap.has(d.libraryId))fail("DIAGNOSTIC_LIBRARY")}
 for(const v of limitations)str(v,"LIMITATION");
 if(typeof dto.countKnown!=="boolean")fail("COUNT_FLAG");
 if(dto.countKnown){if(!Number.isSafeInteger(dto.gameCount)||dto.gameCount!==games.length||libMap.size!==list(dto.libraries,"DTO_LIBRARIES").length||[...libMap.values()].some(l=>!l.indexAvailable)||games.some(g=>g.coverageStatus!=="COMPLETE"))fail("COUNT_FALSE_PROOF")}
 else if(dto.gameCount!==null)fail("UNKNOWN_COUNT_NOT_NULL");
 const dtoIds=list(dto.libraries,"DTO_LIBRARIES").map(l=>str(l.libraryId,"DTO_LIBRARY"));
 if(new Set(dtoIds).size!==dtoIds.length||dtoIds.some(id=>!libMap.has(id))||dtoIds.length!==libMap.size)fail("REGISTRY_MISMATCH");
 return Object.freeze({games,diagnostics,limitations,countKnown:dto.countKnown,gameCount:dto.gameCount,registry:libMap,readOnly:true,schemaVersion:VERSION});
}
function query(model,f={}){
 const text=String(f.search||"").trim().toLocaleLowerCase("fr");
 const arr=model.games.filter(g=>(f.library==="ALL"||!f.library||g.libraryId===f.library)&&
 (f.platform==="ALL"||!f.platform||g.platform===f.platform)&&
 (f.status==="ALL"||!f.status||g.quality===f.status)&&
 (f.coverage==="ALL"||!f.coverage||g.coverageStatus===f.coverage)&&
 (!text||[g.title||"",g.gameId,g.platform,...g.variants].some(v=>v.toLocaleLowerCase("fr").includes(text))));
 const sort=["title","platform","quality","observed"].includes(f.sort)?f.sort:"title";
 return arr.slice().sort((a,b)=>{
 if(sort==="observed")return Date.parse(b.provenance.observedAtUtc)-Date.parse(a.provenance.observedAtUtc)||a.identity.localeCompare(b.identity);
 return String(sort==="title"?a.title??"":a[sort]).localeCompare(String(sort==="title"?b.title??"":b[sort]),"fr")||a.identity.localeCompare(b.identity);
 });
}
global.RM_T2_CATALOGUE=Object.freeze({normalize,query,VERSION});
})(typeof window!=="undefined"?window:globalThis);
