/* T2: read-only candidate catalogue presentation. No ROM inspection or business comparisons. */
(function(global){"use strict";
const fail=(code)=>{throw Error("T2_CATALOGUE_"+code)};
const txt=(v,k)=>{if(typeof v!=="string"||!v.trim())fail(k);return v};
function normalize(dto,registry){
 if(!dto||dto.schemaVersion!=="0.1.0-ui-catalogue-candidate"||dto.readOnly!==true||!Array.isArray(dto.games)||!Array.isArray(registry))fail("CONTRACT");
 const libs=new Map(),games=new Set(),out=[];
 for(const l of registry){txt(l.libraryId,"LIBRARY");if(libs.has(l.libraryId))fail("DUPLICATE_LIBRARY");libs.set(l.libraryId,l)}
 for(const g of dto.games){
  const id=txt(g.gameId,"GAME_ID"),platform=txt(g.platform,"PLATFORM"),title=txt(g.title,"TITLE"),key=JSON.stringify([id,platform]);
  if(games.has(key))fail("DUPLICATE_GAME");games.add(key);
  if(!["KNOWN","INCOMPLETE","INACCESSIBLE"].includes(g.status)||!Array.isArray(g.provenance)||!g.provenance.length||!Array.isArray(g.variants)||!Array.isArray(g.observations))fail("GAME_FIELDS");
  const prov=new Set(),variants=new Set(),observations=new Set();
  for(const p of g.provenance){
   const l=libs.get(txt(p.libraryId,"SOURCE_LIBRARY"));if(!l||!l.indexAvailable)fail("SOURCE_INDEX");
   const snapshotId=txt(p.snapshotId,"SOURCE_SNAPSHOT");
   if(!l.snapshots.some(s=>s.snapshotId===snapshotId||s.generationId===snapshotId))fail("SOURCE_SNAPSHOT_UNKNOWN");
   if(!/^\d{4}-\d{2}-\d{2}T/.test(p.observedAtUtc)||Number.isNaN(Date.parse(p.observedAtUtc)))fail("SOURCE_DATE");
   txt(p.source,"SOURCE_TYPE");const pk=JSON.stringify([p.libraryId,snapshotId]);if(prov.has(pk))fail("DUPLICATE_PROVENANCE");prov.add(pk);
  }
  for(const v of g.variants){txt(v.variantId,"VARIANT_ID");txt(v.label,"VARIANT_LABEL");if(variants.has(v.variantId))fail("DUPLICATE_VARIANT");variants.add(v.variantId)}
  for(const o of g.observations){
   const k=JSON.stringify([txt(o.libraryId,"OBS_LIBRARY"),txt(o.snapshotId,"OBS_SNAPSHOT")]);
   if(!prov.has(k)||!variants.has(txt(o.variantId,"OBS_VARIANT"))||!txt(o.romUnitId,"OBS_UNIT"))fail("OBS_REFERENCE");
   if(!["OBSERVED","PARTIAL"].includes(o.observationStatus))fail("OBS_STATUS");
   const ok=JSON.stringify([o.libraryId,o.snapshotId,o.romUnitId]);if(observations.has(ok))fail("DUPLICATE_OBSERVATION");observations.add(ok);
  }
  if(g.coverStatus!=="KNOWN"&&g.coverStatus!=="UNKNOWN")fail("COVER_STATUS");
  out.push(g);
 }
 return Object.freeze({games:out,registry:libs,readOnly:true,mode:"SYNTHETIC_CANDIDATE"});
}
function query(model,filters){
 const f=filters||{},search=String(f.search||"").trim().toLocaleLowerCase("fr");
 const found=model.games.filter(g=>(!search||[g.title,g.gameId,g.platform,...g.variants.map(v=>v.label)].some(v=>v.toLocaleLowerCase("fr").includes(search)))&&
 (!f.platform||f.platform==="ALL"||g.platform===f.platform)&&
 (!f.status||f.status==="ALL"||g.status===f.status)&&
 (!f.library||f.library==="ALL"||g.provenance.some(p=>p.libraryId===f.library))&&
 (!f.coverage||f.coverage==="ALL"||g.coverStatus===f.coverage));
 const sort=["title","platform","status","observed"].includes(f.sort)?f.sort:"title";
 return found.slice().sort((a,b)=>{if(sort==="observed")return Math.max(...b.provenance.map(p=>Date.parse(p.observedAtUtc)))-Math.max(...a.provenance.map(p=>Date.parse(p.observedAtUtc)));return String(a[sort]).localeCompare(String(b[sort]),"fr",{sensitivity:"base"})||a.gameId.localeCompare(b.gameId)});
}
global.RM_T2_CATALOGUE=Object.freeze({normalize,query});
})(typeof window!=="undefined"?window:globalThis);
