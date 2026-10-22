/* T2 standalone read-only catalogue consumer. No PowerShell or network calls. */
(function(){"use strict";
const $=id=>document.getElementById(id),escape=s=>String(s??"").replace(/[&<>"']/g,c=>({"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;","'":"&#39;"}[c]));
const ids=["search","platform","library","status","coverage","sort"],filters={search:"",platform:"ALL",library:"ALL",status:"ALL",coverage:"ALL",sort:"title"};
let model=null,selected=null;
const badge=(v,type="muted")=>'<span class="tag tag-'+type+'">'+escape(v)+'</span>';
const date=v=>new Intl.DateTimeFormat("fr-CH",{dateStyle:"medium",timeZone:"UTC"}).format(new Date(v))+" UTC";
const opt=(value,label)=>'<option value="'+escape(value)+'">'+escape(label)+'</option>';
function detail(g){
 const el=$("t2-detail");
 if(!g){el.innerHTML='<h2>Fiche du jeu</h2><p class="muted">Choisissez un jeu pour consulter les observations disponibles.</p>';return}
 const sources=g.provenance.map(p=>{const l=model.registry.get(p.libraryId);return '<div class="t2-detail-section"><strong>'+escape(l.name)+'</strong> '+badge(l.availability,l.availability==="ONLINE"?"online":l.availability==="OFFLINE"?"offline":"unknown")+' '+badge(l.freshness,l.freshness==="STALE"?"stale":"muted")+'<dl class="t2-facts"><dt>Snapshot</dt><dd>'+escape(p.snapshotId)+'</dd><dt>Observé le</dt><dd>'+escape(date(p.observedAtUtc))+'</dd><dt>Source</dt><dd>'+escape(p.source)+'</dd><dt>Index</dt><dd>'+(l.indexAvailable?"Consultable":"Indisponible")+'</dd></dl></div>'}).join("");
 const obs=g.observations.map(o=>'<li>'+escape(o.romUnitId)+' · '+escape(o.variantId)+' · '+badge(o.observationStatus,o.observationStatus==="PARTIAL"?"partial":"online")+'</li>').join("");
 el.innerHTML='<h2>'+escape(g.title)+'</h2>'+badge(g.status,g.status==="KNOWN"?"online":"partial")+' '+badge(g.coverStatus==="UNKNOWN"?"Couverture inconnue":"Couverture déclarée","unknown")+'<dl class="t2-facts"><dt>ID jeu</dt><dd>'+escape(g.gameId)+'</dd><dt>Plateforme</dt><dd>'+escape(g.platform)+'</dd><dt>Variantes</dt><dd>'+escape(g.variants.map(v=>v.label+" ("+v.variantId+")").join(", ")||"Non renseignées")+'</dd></dl><h3>Provenance historique</h3>'+sources+'<h3>Unités observées</h3><ul>'+obs+'</ul><p class="muted">Disponibilité actuelle et observation historique sont distinctes. Aucun média ou fichier ROM n’est lu.</p>';
}
function render(){
 if(!model)return;
 const games=window.RM_T2_CATALOGUE.query(model,filters),list=$("t2-results");
 $("t2-count").textContent=games.length+" / "+model.games.length+" jeux de la fixture · couverture non exhaustive";
 if(selected&&!games.includes(selected))selected=null;
 list.innerHTML=games.length?games.map(g=>'<button class="t2-result" data-game="'+escape(g.gameId)+'" data-platform="'+escape(g.platform)+'" aria-pressed="'+(g===selected)+'"><strong>'+escape(g.title)+'</strong><span class="t2-meta">'+badge(g.platform)+badge(g.status,g.status==="KNOWN"?"online":"partial")+badge(g.coverStatus==="UNKNOWN"?"Couverture inconnue":"Couverture connue","unknown")+'<span>'+g.provenance.length+' source(s) · '+g.variants.length+' variante(s)</span></span></button>').join(""):'<div class="t2-empty">Aucun résultat pour ces filtres. Cela ne signifie pas qu’aucun jeu n’existe dans les bibliothèques.</div>';
 detail(selected);
}
function init(){
 try{
  if(window.RM_PROJECTION_DATA)throw Error("MODE_PROJECTION_CATALOGUE_NON_RECEPTIONNE");
  if(!window.RM_T2_FIXTURE||!window.RM_FIXTURES||!window.RM_T2_CATALOGUE)throw Error("FICHIERS_CATALOGUE_MANQUANTS");
  model=window.RM_T2_CATALOGUE.normalize(window.RM_T2_FIXTURE,window.RM_FIXTURES.libraries);
  const platforms=[...new Set(model.games.map(g=>g.platform))].sort();
  $("t2-platform").innerHTML+=platforms.map(x=>opt(x,x)).join("");
  $("t2-library").innerHTML+=[...model.registry.values()].filter(l=>l.indexAvailable).map(l=>opt(l.libraryId,l.name)).join("");
  for(const id of ids){$("t2-"+id).addEventListener(id==="search"?"input":"change",e=>{filters[id]=e.target.value;render()})}
  $("t2-reset").addEventListener("click",()=>{for(const id of ids){filters[id]=id==="search"?"":id==="sort"?"title":"ALL";$("t2-"+id).value=filters[id]}selected=null;render()});
  $("t2-results").addEventListener("click",e=>{const b=e.target.closest("[data-game]");if(!b)return;selected=model.games.find(g=>g.gameId===b.dataset.game&&g.platform===b.dataset.platform)||null;render()});
  render();
 }catch(e){$("t2-error").hidden=false;$("t2-error").className="t2-error";$("t2-error").textContent="Catalogue indisponible : "+e.message+". Aucun résultat de substitution n'est affiché.";$("t2-results").textContent="Consultation bloquée : données invalides ou mode non pris en charge.";$("t2-count").textContent="Non vérifié"}
}
if(document.readyState==="loading")document.addEventListener("DOMContentLoaded",init);else init();
})();