/* UI #11 — no framework, no network, no PowerShell runtime. Read-only synthetic projection. */
(function(){
"use strict";
let payload=window.RM_PROJECTION_DATA;let projectionMode=!!payload;let projectionError=null;let LIBS=window.RM_FIXTURES.libraries;if(projectionMode){try{LIBS=window.RM_PROJECTION.registry(payload.registry)}catch(e){projectionError=e.message;LIBS=[]}}
const CLASSIFICATIONS=[
["ADDED","Ajout observé","added"],
["CHANGED","Observation différente","changed"],
["UNCHANGED","Observation identique","unchanged"],
["NOT_OBSERVED_IN_B_COVERED_SCOPE","Non retrouvé dans B","missing"],
["UNKNOWN","Information insuffisante","unknown"],
["NOT_COMPARABLE","Comparaison impossible","notcomparable"]
];
const labels={ONLINE:"En ligne",OFFLINE:"Hors ligne",INACCESSIBLE:"Inaccessible",UNKNOWN:"Inconnu",FRESH:"À jour",STALE:"À vérifier",complete:"Complet",partial:"Partiel",failed:"Échec",unobserved:"Non observé"};
const reasonLabels={
SCOPE_INTERSECTION_NOT_DECLARED:"Cette plateforme n'est pas déclarée dans les deux générations.",
SCOPE_OR_SCAN_INCOMPLETE:"Un scan ou une zone n'est pas complètement observé.",
IDENTITY_OWNER_CONFLICT:"L'identité de l'unité est associée à des jeux, variantes ou plateformes différents.",
OBSERVATION_DIGEST_UNKNOWN:"L'empreinte d'observation manque dans au moins une génération.",
OBSERVATION_DIGEST_DIFFERENT:"Les empreintes d'observation diffèrent (pas une preuve de changement des octets ROM).",
OBSERVATION_DIGEST_EQUAL:"Les empreintes d'observation concordent (pas une preuve d'identité des octets ROM).",
ABSENT_FROM_VERIFIED_PRIOR_SCOPE:"Unité observée dans B mais non retrouvée dans la zone entièrement couverte de A.",
ABSENT_FROM_VERIFIED_CURRENT_SCOPE:"Unité observée dans A mais non retrouvée dans la zone entièrement couverte de B."
};
const routeHistory=[];
const s={route:"dashboard",libraryId:"lib-arcade",before:"arcade-001",after:"arcade-002",result:null,filter:"ALL",console:"ALL",search:"",selectedUnit:null,collectionSearch:"",collectionMode:"grid",collectionLibrary:"ALL",collectionPlatform:"ALL",collectionUnit:null};
if(projectionMode&&LIBS.length){s.libraryId=LIBS[0].libraryId;s.before=LIBS[0].snapshots[0]?.generationId||"";s.after=LIBS[0].snapshots[1]?.generationId||""}
const $=id=>document.getElementById(id);
const safe=x=>String(x??"").replace(/[&<>"']/g,c=>({"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;","'":"&#39;"}[c]));
const fmt=x=>x?new Intl.DateTimeFormat("fr-CH",{dateStyle:"medium",timeStyle:"short",timeZone:"UTC"}).format(new Date(x))+" UTC":"Date inconnue";
const label=x=>labels[x]||x;
const tag=(x,type)=>'<span class="tag tag-'+safe(type||({ONLINE:"online",OFFLINE:"offline",INACCESSIBLE:"inaccessible",FRESH:"fresh",STALE:"stale",UNKNOWN:"unknown",partial:"partial"}[x]||"muted"))+'">'+safe(label(x))+'</span>';
const cls=x=>CLASSIFICATIONS.find(c=>c[0]===x)||[x,x,"unknown"];
const lib=()=>LIBS.find(x=>x.libraryId===s.libraryId)||LIBS[0];
const getSnap=(l,id)=>l?.snapshots.find(x=>x.generationId===id||x.snapshotId===id);
const unique=a=>[...new Set(a)];
const button=(text,action,variant="")=>'<button class="btn '+variant+'" data-action="'+safe(action)+'">'+text+'</button>';
function validateSnapshot(x){
if(!x||x.schemaVersion!=="1.0.0"||!x.libraryId||!x.generationId||!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z$/.test(x.capturedAtUtc)||Number.isNaN(Date.parse(x.capturedAtUtc)))throw Error("SNAPSHOT_INVALID");
if(!["complete","partial","failed"].includes(x.scanStatus)||!["ONLINE","OFFLINE","INACCESSIBLE","UNKNOWN"].includes(x.sourceAvailability)||!["FRESH","STALE","UNKNOWN"].includes(x.freshness)||!Array.isArray(x.coverage)||!Array.isArray(x.units))throw Error("SNAPSHOT_FIELDS_INVALID");
const scopes=new Map();
for(const c of x.coverage){if(!c.console||scopes.has(c.console)||!["complete","partial","unobserved"].includes(c.status))throw Error("SNAPSHOT_COVERAGE_INVALID");scopes.set(c.console,c.status)}
const units=new Map();
for(const u of x.units){if(!u.romUnitId||!u.gameId||!u.variantId||!u.console||"fileId" in u||units.has(u.romUnitId)||!scopes.has(u.console)||scopes.get(u.console)==="unobserved")throw Error("SNAPSHOT_UNIT_INVALID");
if(u.observationDigest!=null&&!/^[a-fA-F0-9]{64}$/.test(u.observationDigest))throw Error("SNAPSHOT_DIGEST_INVALID");units.set(u.romUnitId,u)}
return {scopes,units};
}
/* Browser mirror of the PR #26 projection contract, NOT a PowerShell invocation.
   Strict scope intersection on BOTH sides, with six classifications and no deletion claim. */
function compare(before,after){
const A=validateSnapshot(before),B=validateSnapshot(after);
if(before.libraryId!==after.libraryId)throw Error("SNAPSHOT_LIBRARY_MISMATCH");
if(before.generationId===after.generationId)throw Error("SNAPSHOT_GENERATION_REUSED");
if(Date.parse(before.capturedAtUtc)>Date.parse(after.capturedAtUtc))throw Error("SNAPSHOT_DATE_ORDER_INVALID");
const counts=Object.fromEntries(CLASSIFICATIONS.map(x=>[x[0],0]));
const ids=unique([...A.units.keys(),...B.units.keys()]).sort();
const entries=ids.map(id=>{
const prior=A.units.get(id),current=B.units.get(id),owner=current||prior,c=owner.console;
let classification="UNKNOWN",reason="SCOPE_OR_SCAN_INCOMPLETE",changedFields=[],comparedFields=[];
if(!A.scopes.has(c)||!B.scopes.has(c)){classification="NOT_COMPARABLE";reason="SCOPE_INTERSECTION_NOT_DECLARED"}
else if(before.scanStatus!=="complete"||after.scanStatus!=="complete"||A.scopes.get(c)!=="complete"||B.scopes.get(c)!=="complete"){}
else if(prior&&current){
if(prior.gameId!==current.gameId||prior.variantId!==current.variantId||prior.console!==current.console){classification="NOT_COMPARABLE";reason="IDENTITY_OWNER_CONFLICT"}
else if(prior.observationDigest==null||current.observationDigest==null){reason="OBSERVATION_DIGEST_UNKNOWN"}
else if(prior.observationDigest.toLowerCase()!==current.observationDigest.toLowerCase()){
classification="CHANGED";reason="OBSERVATION_DIGEST_DIFFERENT";changedFields=[{field:"observationDigest",before:prior.observationDigest,after:current.observationDigest,level:"observedStat",evidence:"unit-observation-only"}]}
else{classification="UNCHANGED";reason="OBSERVATION_DIGEST_EQUAL";comparedFields=[{field:"observationDigest",before:prior.observationDigest,after:current.observationDigest,level:"observedStat",evidence:"unit-observation-only"}]}
}else if(current){classification="ADDED";reason="ABSENT_FROM_VERIFIED_PRIOR_SCOPE"}
else{classification="NOT_OBSERVED_IN_B_COVERED_SCOPE";reason="ABSENT_FROM_VERIFIED_CURRENT_SCOPE"}
counts[classification]++;
return {romUnitId:id,gameId:owner.gameId,variantId:owner.variantId,console:c,title:owner.title||id,classification,reason,beforeDigest:prior?.observationDigest??null,afterDigest:current?.observationDigest??null,changedFields,comparedFields,sourceRefs:{beforeGenerationId:before.generationId,afterGenerationId:after.generationId}};
});
const scopes=unique([...A.scopes.keys(),...B.scopes.keys()]).sort().map(console=>({console,before:A.scopes.get(console)||null,after:B.scopes.get(console)||null,comparable:before.scanStatus==="complete"&&after.scanStatus==="complete"&&A.scopes.get(console)==="complete"&&B.scopes.get(console)==="complete"}));
return {schemaVersion:"1.0.0",libraryId:before.libraryId,beforeGenerationId:before.generationId,afterGenerationId:after.generationId,counts,entries,scopes,complete:scopes.length>0&&scopes.every(x=>x.comparable),beforeScanStatus:before.scanStatus,afterScanStatus:after.scanStatus,beforeAvailability:before.sourceAvailability,afterAvailability:after.sourceAvailability,beforeFreshness:before.freshness,afterFreshness:after.freshness};
}
window.RM_COMPARE=compare;
function heading(k,title,sub,right=""){return '<div class="page-heading"><div><div class="eyebrow">'+k+'</div><h1>'+title+'</h1><p>'+sub+'</p></div>'+right+'</div>'}
function libCard(l){
const last=l.snapshots.at(-1),verified=getSnap(l,l.lastVerifiedCompleteRef);
const countLabel=!l.indexAvailable||!last||last.countKnown===false?"Inconnu (index indisponible ou non prouvé)":last.countKnown===true?String(last.observedUnitCount):Array.isArray(last.units)?String(last.units.length):"Inconnu";
return '<article class="panel library-card '+(s.libraryId===l.libraryId&&s.route==="libraries"?"selected":"")+'"><div class="row between"><h3 class="library-title">'+safe(l.name)+'</h3>'+tag(l.availability)+'</div><div class="small muted">'+safe(l.description)+'</div><div class="meta-row">'+(l.platforms.length?l.platforms.map(p=>tag(p,"muted")).join(""):tag("Plateformes inconnues","unknown"))+tag(l.freshness)+'</div><div class="small muted">Dernière tentative : <strong>'+safe(last?fmt(last.capturedAtUtc):"Non disponible")+'</strong><br>Dernier snapshot complet vérifié : <strong>'+safe(verified?verified.generationId+" · "+fmt(verified.capturedAtUtc):"Inconnu")+'</strong><br>Unités observées (dernière génération) : <strong>'+safe(countLabel)+'</strong></div><div class="card-bottom"><span class="tiny muted">'+(l.indexAvailable?"Historique local consultable":"Index indisponible")+'</span>'+button("Consulter →","library:"+l.libraryId,"btn-sm")+'</div></article>';
}
function dashboard(){
const known=unique(LIBS.flatMap(l=>l.platforms)),unknown=LIBS.some(l=>!l.platforms.length),offline=LIBS.filter(l=>l.availability==="OFFLINE").length,stale=LIBS.filter(l=>l.freshness==="STALE").length;
const latest=LIBS.filter(l=>l.indexAvailable).flatMap(l=>l.snapshots.map(x=>({name:l.name,...x}))).sort((a,b)=>b.capturedAtUtc.localeCompare(a.capturedAtUtc)).slice(0,4);
return heading("Vue générale","Votre collection, en un coup d'œil","Vue synthétique en lecture seule, sans accès aux fichiers physiques.",button("Ouvrir les bibliothèques →","go:libraries","btn-primary"))
+'<section class="grid stats"><div class="panel stat"><div class="stat-label">Bibliothèques enregistrées</div><div class="stat-number">'+LIBS.length+'</div><div class="stat-detail">Registre synthétique</div></div><div class="panel stat"><div class="stat-label">Plateformes connues</div><div class="stat-number">'+(unknown?"≥ ":"")+known.length+'</div><div class="stat-detail">'+(unknown?"Au moins : une bibliothèque non renseignée":"Plateformes déclarées")+'</div></div><div class="panel stat"><div class="stat-label">Sources hors ligne</div><div class="stat-number">'+offline+'</div><div class="stat-detail">Inaccessible : '+LIBS.filter(l=>l.availability==="INACCESSIBLE").length+' · inconnu : '+LIBS.filter(l=>l.availability==="UNKNOWN").length+'</div></div><div class="panel stat"><div class="stat-label">Inventaires à vérifier</div><div class="stat-number">'+stale+'</div><div class="stat-detail">Fraîcheur inconnue : '+LIBS.filter(l=>l.freshness==="UNKNOWN").length+'</div></div></section>'
+'<div class="hero"><h2>Retrouvez vos bibliothèques, même hors ligne.</h2><p>Une source déconnectée peut conserver un historique consultable. Aucune opération sur les fichiers n’est lancée depuis ce prototype.</p><div class="row" style="margin-top:17px">'+button("Parcourir les bibliothèques","go:libraries","btn-primary")+button("Comparer deux snapshots","go:compare")+'</div></div>'
+'<div class="grid two-col"><section class="panel"><div class="panel-header"><div><h2>Dernières observations synthétiques</h2><p>Générations datées, sans inférence sur les supports actuels.</p></div></div><div class="table-wrap"><table class="table"><thead><tr><th>Bibliothèque</th><th>Génération</th><th>Date</th><th>Scan</th></tr></thead><tbody>'+latest.map(x=>'<tr><td>'+safe(x.name)+'</td><td>'+safe(x.generationId)+'</td><td>'+safe(fmt(x.capturedAtUtc))+'</td><td>'+tag(x.scanStatus)+'</td></tr>').join("")+'</tbody></table></div></section><section class="panel panel-pad"><h2 class="section-title">État des sources</h2>'+LIBS.map(l=>'<div class="row between" style="padding:10px 0;border-bottom:1px solid var(--line)"><span>'+safe(l.name)+'</span><span class="row">'+tag(l.availability)+tag(l.freshness)+'</span></div>').join("")+'<p class="small muted">Les valeurs inconnues ne sont pas comptées comme zéro.</p></section></div>';
}
function libraries(){
const l=lib(),verified=getSnap(l,l.lastVerifiedCompleteRef);
return heading("Bibliothèques","Vos espaces de jeux","Choisissez une bibliothèque pour consulter son historique synthétique, même si le support est hors ligne.")
+'<div class="grid three-col">'+LIBS.map(libCard).join("")+'</div>'
+'<div style="height:23px"></div><section class="panel"><div class="panel-header"><div><h2>Bibliothèque sélectionnée · '+safe(l.name)+'</h2><p>Identité logique : '+safe(l.libraryId)+'</p></div>'+tag(l.availability)+'</div><div class="panel-pad"><div class="grid two-col"><div><div class="row">'+tag(l.freshness)+(l.indexAvailable?tag("Index consultable","fresh"):tag("Index indisponible","inaccessible"))+'</div><p class="small muted">Dernière observation du support : '+safe(fmt(l.observedAtUtc))+' · état physique distinct des snapshots historiques.</p><p class="small muted">Dernière génération complète vérifiée : '+safe(verified?verified.generationId+" · "+fmt(verified.capturedAtUtc):"Aucune preuve disponible")+'</p></div><div><h3 class="section-title">Historique synthétique</h3>'+(l.indexAvailable&&l.snapshots.length?'<div class="stack">'+[...l.snapshots].reverse().map(x=>'<div class="row between"><div><strong>'+safe(x.generationId)+'</strong><div class="tiny muted">'+safe(fmt(x.capturedAtUtc))+'</div></div><span class="row">'+tag(x.scanStatus)+button('Comparer →','snapshot-select:'+l.libraryId+':'+x.snapshotId,'btn-sm')+'</span></div>').join("")+'</div>':'<div class="empty"><strong>Historique indisponible</strong>'+(l.indexAvailable?"Aucun snapshot enregistré.":"L’index local n’est pas accessible. Les jeux ne sont pas considérés absents.")+'</div>')+'</div></div><hr class="divider">'+button("Comparer les générations →","go:compare","btn-primary")+'</div></section>';
}
function snapshotOption(x){return '<option value="'+safe(x.generationId)+'">'+safe(x.generationId+" · "+fmt(x.capturedAtUtc)+" · "+label(x.scanStatus))+'</option>'}
function coverageBlock(x,title){return '<div><div class="small muted" style="margin-bottom:8px">'+title+'</div><div class="coverage">'+(x.coverage.length?x.coverage.map(c=>'<span class="scope">'+safe(c.console)+' · '+safe(label(c.status))+'</span>').join(""):'<span class="scope">Périmètre non déclaré</span>')+'</div></div>'}
function comparePage(){
const l=lib(),A=getSnap(l,s.before),B=getSnap(l,s.after);
let header=heading("Comparaison","Comparer deux générations","Comprendre les observations entre deux inventaires datés, sans interpréter une absence comme une suppression.");
let chooser='<section class="panel panel-pad"><div class="row between"><h2 class="section-title">Choisir les snapshots</h2>'+tag(l.availability)+'</div><div class="field" style="margin-bottom:15px"><label for="lib-select">Bibliothèque</label><select id="lib-select" class="select">'+LIBS.map(x=>'<option value="'+safe(x.libraryId)+'" '+(x.libraryId===l.libraryId?"selected":"")+'>'+safe(x.name)+'</option>').join("")+'</select></div>';
if(!l.indexAvailable||!l.snapshots.length){chooser+='<div class="notice danger">Impossible de comparer : aucun index historique accessible pour cette bibliothèque. Cela ne signifie pas que la collection est vide.</div></section>';return header+chooser}
chooser+='<div class="grid two-col"><div class="field"><label for="before-select">Génération A · référence</label><select class="select" id="before-select">'+l.snapshots.map(x=>snapshotOption(x).replace('value="'+safe(s.before)+'"','value="'+safe(s.before)+'" selected')).join("")+'</select></div><div class="field"><label for="after-select">Génération B · cible</label><select class="select" id="after-select">'+l.snapshots.map(x=>snapshotOption(x).replace('value="'+safe(s.after)+'"','value="'+safe(s.after)+'" selected')).join("")+'</select></div></div>';
chooser+='<div class="grid two-col" style="margin:19px 0">'+(A?coverageBlock(A,"Couverture A"):"")+(B?coverageBlock(B,"Couverture B"):"")+'</div><div class="row between"><span class="small muted">La comparaison ne lance aucun scan.</span>'+button("Lancer la comparaison","run-compare","btn-primary")+'</div></section>';
if(!s.result)return header+chooser+'<div class="panel empty" style="margin-top:18px"><strong>Prêt pour une comparaison</strong>Sélectionnez deux générations puis lancez l’analyse synthétique.</div>';
if(s.result.error)return header+chooser+'<div class="notice danger" style="margin-top:17px">Comparaison refusée : '+safe(s.result.error)+'</div>';
const r=s.result;
const filtered=r.entries.filter(x=>(s.filter==="ALL"||x.classification===s.filter)&&(s.console==="ALL"||x.console===s.console)&&(!s.search||[x.romUnitId,x.title,x.gameId,x.variantId].some(y=>String(y).toLocaleLowerCase().includes(s.search.toLocaleLowerCase()))));
const item=r.entries.find(x=>x.romUnitId===s.selectedUnit);
const warning=!r.complete?'<div class="notice" style="margin:15px 0">Couverture incomplète ou inconnue : les compteurs portent uniquement sur les unités observées. Une liste vide ne prouve ni une bibliothèque vide, ni l’absence de différences. Les zones non prouvées restent inconnues.</div>':'<div class="notice info" style="margin:15px 0">Couverture complète déclarée sur les plateformes de ces deux générations synthétiques. Les empreintes décrivent des observations, pas les octets des ROM.</div>';
return header+chooser+'<section class="panel" style="margin-top:18px"><div class="panel-header"><div><h2>Résultats · '+safe(r.beforeGenerationId||r.beforeSnapshotId)+' → '+safe(r.afterGenerationId||r.afterSnapshotId)+'</h2><p>'+r.entries.length+' unités distinctes dans A∪B · état actuel : '+safe(label(l.availability))+'</p></div>'+tag(l.freshness)+'</div><div class="panel-pad"><div class="pill-list">'+CLASSIFICATIONS.map(c=>'<button class="summary-pill" data-action="filter:'+c[0]+'" aria-pressed="'+(s.filter===c[0])+'"><b>'+r.counts[c[0]]+'</b><span>'+c[1]+'</span></button>').join("")+'</div>'+warning+'<p class="small muted">Dénominateur observé : '+safe(r.countDenominator??r.entries.length)+' · classifications : '+CLASSIFICATIONS.length+' · aucun état physique déduit.</p><div class="small muted">Périmètres : '+r.scopes.map(c=>safe(c.console)+" — "+(c.comparable?"comparable":"non prouvé")+(c.reason?" ("+safe(c.reason)+")":"")).join(" · ")+'</div>'+(projectionMode?'<div class="small muted" style="margin-top:10px"><strong>Limites de la projection :</strong> '+(r.limitations.length?r.limitations.map(x=>safe(x)).join(" · "):"Aucune limitation fournie")+'</div>':'')+'</div></section>'
+'<div class="split" style="margin-top:17px"><section class="panel"><div class="panel-header"><h2>Unités observées</h2><span class="small muted">'+filtered.length+' résultat(s) affiché(s)</span></div><div class="panel-pad"><div class="filter-row"><select id="filter-class" class="select" aria-label="Filtrer par résultat"><option value="ALL">Tous les résultats</option>'+CLASSIFICATIONS.map(c=>'<option value="'+c[0]+'" '+(s.filter===c[0]?"selected":"")+'>'+c[1]+'</option>').join("")+'</select><select id="filter-console" class="select" aria-label="Filtrer par plateforme"><option value="ALL">Toutes plateformes</option>'+unique(r.entries.map(x=>x.console)).map(c=>'<option '+(s.console===c?"selected":"")+'>'+safe(c)+'</option>').join("")+'</select><input id="filter-search" class="input" type="search" value="'+safe(s.search)+'" placeholder="Rechercher une unité" aria-label="Rechercher une unité"></div></div><div class="table-wrap">'+(filtered.length?'<table class="table"><thead><tr><th>Unité</th><th>Plateforme</th><th>Résultat</th></tr></thead><tbody>'+filtered.map(x=>'<tr class="clickable '+(s.selectedUnit===x.romUnitId?"selected":"")+'" data-action="unit:'+safe(x.romUnitId)+'" tabindex="0" role="button" aria-label="Voir le détail de '+safe(x.title||x.romUnitId||"Unité sans titre")+'"><td><strong>'+safe(x.title||x.romUnitId||"Unité sans titre")+'</strong><div class="tiny muted">'+safe(x.romUnitId)+'</div></td><td>'+safe(x.console)+'</td><td>'+tag(cls(x.classification)[1],cls(x.classification)[2])+'</td></tr>').join("")+'</tbody></table>':'<div class="empty"><strong>Aucune unité correspondant aux filtres</strong>La couverture doit être examinée séparément ; ce résultat ne prouve pas qu’il n’y a aucune différence.</div>')+'</div></section>'
+'<section class="panel"><div class="panel-header"><h2>Détail de l’unité</h2></div>'+(item?'<div class="panel-pad"><div class="row">'+tag(cls(item.classification)[1],cls(item.classification)[2])+'</div><h3>'+safe(item.title||item.romUnitId||"Unité sans titre")+'</h3><p class="muted small">'+safe(reasonLabels[item.reason]||item.reason)+'</p><dl class="detail-grid"><div><dt>Identité ROM</dt><dd>'+safe(item.romUnitId)+'</dd></div><div><dt>Plateforme</dt><dd>'+safe(item.console)+'</dd></div><div><dt>Jeu</dt><dd>'+safe(item.gameId)+'</dd></div><div><dt>Variante</dt><dd>'+safe(item.variantId)+'</dd></div><div><dt>Génération A</dt><dd>'+safe(item.sourceRefs.beforeGenerationId)+'</dd></div><div><dt>Génération B</dt><dd>'+safe(item.sourceRefs.afterGenerationId)+'</dd></div></dl><hr class="divider"><p class="small muted">Observation A : '+safe(item.beforeDigest?item.beforeDigest.slice(0,12)+"…":"Inconnue ou absente")+'</p><p class="small muted">Observation B : '+safe(item.afterDigest?item.afterDigest.slice(0,12)+"…":"Inconnue ou absente")+'</p><p class="tiny muted">Code de preuve : '+safe(item.reason)+' · observation uniquement, aucune vérification du contenu binaire.</p></div>':'<div class="empty"><strong>Sélectionnez une unité</strong>Les motifs et preuves disponibles s’affichent ici.</div>')+'</section></div>';
}
function collection(){
if(projectionMode&&payload.catalogue&&(window.RM_T2_CATALOGUE||window.RM_CATALOGUE))return t1Collection();
if(!projectionMode&&window.RM_T2_CATALOGUE&&window.RM_T2_FIXTURE)return t1Collection();
// Catalogue absent: preserve the historical snapshot-only fallback instead of raising a missing-view exception.
const items=LIBS.filter(l=>l.indexAvailable&&(s.collectionLibrary==="ALL"||s.collectionLibrary===l.libraryId)).flatMap(l=>{const snap=getSnap(l,l.lastVerifiedCompleteRef);return snap&&Array.isArray(snap.units)?snap.units.map(u=>({...u,library:l})):[]});
const filtered=items.filter(u=>(s.collectionLibrary==="ALL"||u.library.libraryId===s.collectionLibrary)&&(s.collectionPlatform==="ALL"||u.console===s.collectionPlatform)&&(!s.collectionSearch||[u.title,u.romUnitId,u.gameId].some(x=>String(x).toLocaleLowerCase().includes(s.collectionSearch.toLocaleLowerCase()))));
const selected=items.find(x=>x.romUnitId===s.collectionUnit);
return heading("Collection","Explorer la collection","Consultation fictive basée sur le dernier snapshot complet connu, pas sur les fichiers physiques.")
+'<div class="notice info" style="margin-bottom:18px">Cette vue montre uniquement des unités de snapshots synthétiques vérifiés. Les bibliothèques sans index accessible restent inconnues, jamais vides.</div>'
+'<section class="panel panel-pad"><div class="filter-row"><input class="input" id="collection-search" type="search" placeholder="Rechercher un titre…" aria-label="Rechercher un titre" value="'+safe(s.collectionSearch)+'"><select class="select" id="collection-library" aria-label="Filtrer par bibliothèque"><option value="ALL">Toutes les bibliothèques</option>'+LIBS.map(l=>'<option value="'+safe(l.libraryId)+'" '+(s.collectionLibrary===l.libraryId?"selected":"")+'>'+safe(l.name)+'</option>').join("")+'</select><select class="select" id="collection-platform" aria-label="Filtrer par plateforme"><option value="ALL">Toutes les plateformes</option>'+unique(items.map(x=>x.console)).map(p=>'<option value="'+safe(p)+'" '+(s.collectionPlatform===p?"selected":"")+'>'+safe(p)+'</option>').join("")+'</select><select class="select" id="collection-mode" aria-label="Mode d’affichage"><option value="grid" '+(s.collectionMode==="grid"?"selected":"")+'>Grille</option><option value="list" '+(s.collectionMode==="list"?"selected":"")+'>Liste</option></select></div><p class="small muted">'+filtered.length+' unité(s) issues des inventaires connus · aucune fusion automatique de variantes</p></section>'
+'<div class="split" style="margin-top:16px"><div>'+(filtered.length?'<div class="'+(s.collectionMode==="grid"?"collection-grid":"stack")+'">'+filtered.map(u=>'<button class="game-card" data-action="game:'+safe(u.romUnitId)+'">'+(s.collectionMode==="grid"?'<div class="cover" aria-hidden="true">▣</div>':'')+'<strong>'+safe(u.title)+'</strong><small>'+safe(u.console)+' · '+safe(u.variantId)+'<br>'+safe(u.library.name)+'</small></button>').join("")+'</div>':'<div class="panel empty"><strong>Aucune unité dans ce filtre</strong>Les données inconnues ou inaccessibles ne sont pas comptées comme zéro.</div>')+'</div><section class="panel"><div class="panel-header"><h2>Inspecteur</h2></div>'+(selected?'<div class="panel-pad"><div class="cover" aria-label="Jaquette non disponible">▣</div><h2>'+safe(selected.title)+'</h2><div class="row">'+tag(selected.library.availability)+tag(selected.library.freshness)+'</div><dl class="detail-grid" style="margin-top:18px"><div><dt>Plateforme</dt><dd>'+safe(selected.console)+'</dd></div><div><dt>Bibliothèque</dt><dd>'+safe(selected.library.name)+'</dd></div><div><dt>Identité ROM</dt><dd>'+safe(selected.romUnitId)+'</dd></div><div><dt>Variante</dt><dd>'+safe(selected.variantId)+'</dd></div></dl><p class="small muted">Jaquette indisponible · aucune fiche enrichie ni édition à ce stade.</p>'+button("Voir ses snapshots","library:"+selected.library.libraryId)+'</div>':'<div class="empty"><strong>Choisissez un jeu</strong>Les détails connus apparaîtront ici.</div>')+'</section></div>';
}
/* Compatibility with the older #17 candidate DTO: validate with its original
   adapter, then present it through the SAME Collection/Fiche renderer.
   Never infer games from inaccessible indexes or synthesize disc evidence. */
function legacyProjectionCatalogue(dto){
 if(!window.RM_CATALOGUE||!window.RM_T2_CATALOGUE)throw Error("LEGACY_CATALOGUE_DEPENDENCY_MISSING");
 const source=window.RM_CATALOGUE.parse(dto,LIBS),byId=new Map(LIBS.map(l=>[l.libraryId,l])),games=[],seen=new Set();
 for(const g of source.games)for(const p of g.provenance){
  const library=byId.get(p.libraryId),snapshot=library?.snapshots.find(x=>x.snapshotId===p.snapshotId);
  if(!library||!library.indexAvailable||!snapshot)throw Error("LEGACY_CATALOGUE_ORPHAN_REFERENCE");
  const identity=JSON.stringify([p.libraryId,g.platform,g.gameId]);
  if(seen.has(identity))throw Error("LEGACY_CATALOGUE_COMPOUND_COLLISION");seen.add(identity);
  const coverageStatus=snapshot.scanStatus==="complete"&&snapshot.scopeIsExhaustive&&
   snapshot.coverage.length===library.platforms.length&&
   snapshot.coverage.every(c=>c.status==="complete")?"COMPLETE":"INCOMPLETE";
  const quality=g.status==="INCOMPLETE"?"PARTIALLY_OBSERVED":
   library.freshness==="STALE"?"STALE":library.freshness==="UNKNOWN"?"UNKNOWN":
   coverageStatus==="COMPLETE"?"KNOWN":"PARTIALLY_OBSERVED";
  games.push({identity,gameId:g.gameId,libraryId:p.libraryId,platform:g.platform,
   title:g.title,quality,coverageStatus,availability:library.availability,freshness:library.freshness,
   variants:g.variants.map(v=>v.variantId),
   provenance:{libraryId:p.libraryId,snapshotId:p.snapshotId,generationId:snapshot.generationId,observedAtUtc:p.observedAtUtc,source:p.source},
   units:g.observations.filter(o=>o.libraryId===p.libraryId&&o.snapshotId===p.snapshotId)
    .map(o=>({romUnitId:o.romUnitId,variantId:o.variantId,discNumber:null,observationStatus:o.observationStatus,observedTitle:null}))});
 }
 return {schemaVersion:dto.schemaVersion,games,diagnostics:[],
  limitations:["LEGACY_PROJECTION_HAND_AUTHORED","NO_ROM_BYTES_VERIFIED"],
  countKnown:false,gameCount:null};
}
function t1Collection(){
 let model;
 try{
  const dto=projectionMode?payload.catalogue:window.RM_T2_FIXTURE;
  if(!dto)throw Error("T1_UI_CATALOGUE_MISSING");
  model=dto.schemaVersion==="0.1.0-ui-catalogue-candidate"?legacyProjectionCatalogue(dto):window.RM_T2_CATALOGUE.normalize(dto,LIBS);
 }catch(e){return heading("Collection","Catalogue indisponible","Le contrat catalogue n'a pas été accepté.")+'<div class="notice danger" role="alert">Projection refusée : '+safe(e.message)+'. Aucun jeu ne peut être déduit de cet échec.</div>'}
 const filtered=window.RM_T2_CATALOGUE.query(model,{search:s.collectionSearch,platform:s.collectionPlatform,library:s.collectionLibrary,status:s.collectionStatus||"ALL",coverage:s.collectionCoverage||"ALL",sort:s.collectionSort||"title"});
 const selected=model.games.find(g=>g.identity===s.collectionUnit);
 const options=(values,selected)=>values.map(v=>'<option value="'+safe(v)+'" '+(selected===v?'selected':'')+'>'+safe(v)+'</option>').join("");
 const filter='<section class="panel panel-pad"><div class="filter-row"><input id="collection-search" class="input" type="search" aria-label="Rechercher un jeu" placeholder="Titre, identité, variante…" value="'+safe(s.collectionSearch)+'"><select id="collection-library" class="select" aria-label="Bibliothèque"><option value="ALL">Toutes bibliothèques</option>'+LIBS.map(l=>'<option value="'+safe(l.libraryId)+'" '+(s.collectionLibrary===l.libraryId?'selected':'')+'>'+safe(l.name)+'</option>').join("")+'</select><select id="collection-platform" class="select" aria-label="Plateforme"><option value="ALL">Toutes plateformes</option>'+options(unique(model.games.map(g=>g.platform)),s.collectionPlatform)+'</select><select id="collection-status" class="select" aria-label="Qualité"><option value="ALL">Toutes qualités</option>'+options(["KNOWN","STALE","PARTIALLY_OBSERVED","CONTRADICTORY","UNKNOWN"],s.collectionStatus||"ALL")+'</select><select id="collection-coverage" class="select" aria-label="Couverture"><option value="ALL">Toutes couvertures</option>'+options(["COMPLETE","INCOMPLETE"],s.collectionCoverage||"ALL")+'</select><select id="collection-sort" class="select" aria-label="Trier par"><option value="title">Titre</option><option value="platform" '+(s.collectionSort==="platform"?'selected':'')+'>Plateforme</option><option value="quality" '+(s.collectionSort==="quality"?'selected':'')+'>Qualité</option><option value="observed" '+(s.collectionSort==="observed"?'selected':'')+'>Observation</option></select></div><p class="small muted">'+filtered.length+' fiche(s) observée(s) dans ce filtre · Total global : '+(model.countKnown?model.gameCount:'inconnu — couverture non prouvée')+'.</p></section>';
 const cards=filtered.length?'<div class="'+(s.collectionMode==="list"?'stack':'collection-grid')+'">'+filtered.map(g=>'<button class="game-card" data-action="catalogue-game:'+safe(g.identity)+'" aria-pressed="'+(selected===g)+'"><div class="cover" aria-hidden="true">▣</div><strong>'+safe(g.title??'Titre inconnu ou contradictoire')+'</strong><small>'+safe(g.platform)+' · '+safe(g.quality)+' · '+safe(LIBS.find(l=>l.libraryId===g.libraryId)?.name||g.libraryId)+'</small></button>').join("")+'</div>':'<div class="panel empty"><strong>Aucun résultat dans ce filtre</strong>La couverture du catalogue n’est pas nécessairement exhaustive ; ne pas interpréter comme zéro jeu physique.</div>';
 const detail=selected?'<div class="panel-pad"><div class="row between"><h2>'+safe(selected.title??'Titre non établi')+'</h2>'+button("Fermer la fiche","close-game")+'</div><div class="cover" aria-label="Média non disponible">▣</div><p class="small muted">Fiche de jeu · '+safe(selected.gameId)+' · '+safe(selected.platform)+'</p><div class="row">'+tag(selected.availability)+tag(selected.freshness)+tag(selected.quality==="KNOWN"?'Connu':selected.quality,'partial')+tag(selected.coverageStatus==="COMPLETE"?'Couverture prouvée par le registre':'Couverture non prouvée','partial')+'</div><p class="small muted">Qualité et couverture déclarées par le producteur candidat ; aucune vérification physique dans le navigateur.</p><h3>Variantes</h3><p>'+selected.variants.map(safe).join(' · ')+'</p><h3>Provenance</h3><p class="small">'+safe(selected.libraryId)+' · snapshot '+safe(selected.provenance.snapshotId)+' · génération '+safe(selected.provenance.generationId)+' · '+safe(fmt(selected.provenance.observedAtUtc))+(selected.provenance.source?' · '+safe(selected.provenance.source):'')+'</p>'+button("Voir le snapshot","snapshot:"+selected.libraryId+":"+selected.provenance.snapshotId,"btn-sm")+'<h3>Unités ROM observées</h3><p class="small muted">Ces unités ne constituent pas d’autres jeux. Aucun chemin physique n’est exposé.</p><div class="stack">'+selected.units.map(u=>'<div class="small">'+safe(u.romUnitId)+' · variante '+safe(u.variantId)+' · '+(u.discNumber===null?'Disque non renseigné':'Disque '+safe(u.discNumber))+(u.observationStatus?' · '+safe(u.observationStatus):'')+(u.observedTitle?' · '+safe(u.observedTitle):'')+'</div>').join("")+'</div></div>':'<div class="empty"><strong>Sélectionnez un jeu</strong>Les variantes et la provenance apparaîtront ici.</div>';
 const importer=window.RM_T3_CATALOGUE?'<section class="panel panel-pad" style="margin-bottom:14px"><label class="small" for="synthetic-catalogue-import">Charger un JSON UTF-8 issu de l’exporteur <strong>synthétique</strong> PS5.1 (démonstration uniquement)</label><input class="input" id="synthetic-catalogue-import" type="file" accept=".json,application/json" aria-label="Sélectionner un export JSON synthétique"><p class="tiny muted">Lecture locale sur sélection explicite, aucun réseau, aucune donnée réelle. Une comparaison précalculée n’est pas inventée.</p></section>':'';
 return heading("Collection","Catalogue des jeux","Consommateur "+safe(model.schemaVersion)+" · lecture seule · "+(projectionMode?'projection candidate fournie':'fixture synthétique manuelle'))+importer+
 '<div class="notice info" style="margin-bottom:18px">Les bibliothèques hors ligne conservent leur historique. Index inaccessible : aucun jeu inféré. ' +model.diagnostics.length+' diagnostic(s) : '+(model.diagnostics.map(d=>safe(d.code)).join(', ')||'aucun')+' · '+model.limitations.map(safe).join(', ')+'</div>'+filter+'<div class="split" style="margin-top:16px"><div>'+cards+'</div><section class="panel"><div class="panel-header"><h2>Fiche de jeu</h2></div>'+detail+'</section></div>';
}
function chooseLibrary(id){s.libraryId=id;const l=lib();s.before=l.snapshots[0]?.generationId||"";s.after=l.snapshots[1]?.generationId||"";s.result=null;s.selectedUnit=null;s.filter="ALL";s.console="ALL";s.search=""}
function route(to){if(s.route!==to)routeHistory.push(s.route);s.route=to;render();if(typeof $("view").focus==="function")$("view").focus({preventScroll:true})}
function goBack(){if(routeHistory.length){s.route=routeHistory.pop();render();if(typeof $("view").focus==="function")$("view").focus({preventScroll:true})}}
function runCompare(){
const l=lib();try{if(!l.indexAvailable)throw Error("INDEX_UNAVAILABLE");const A=getSnap(l,s.before),B=getSnap(l,s.after);if(!A||!B)throw Error("SNAPSHOT_UNAVAILABLE");if(projectionMode){const dto=(payload.comparisons||[]).find(x=>x.library?.libraryId===l.libraryId&&x.before?.snapshotId===A.snapshotId&&x.after?.snapshotId===B.snapshotId);if(!dto)throw Error('PROJECTION_PAIR_UNAVAILABLE');s.result=window.RM_PROJECTION.comparison(dto,l.libraryId,l)}else{s.result=compare(A,B)}s.selectedUnit=null;s.filter="ALL";s.console="ALL";s.search=""}
catch(e){s.result={error:({"INDEX_UNAVAILABLE":"Index indisponible.","SNAPSHOT_UNAVAILABLE":"Deux snapshots accessibles sont requis.","SNAPSHOT_LIBRARY_MISMATCH":"Bibliothèques différentes.","SNAPSHOT_GENERATION_REUSED":"Sélectionnez deux générations distinctes.","SNAPSHOT_DATE_ORDER_INVALID":"La génération B doit être aussi récente ou plus récente que A.","PROJECTION_PAIR_UNAVAILABLE":"Comparaison précalculée indisponible : aucun recalcul dans le navigateur."}[e.message]||"Contrat de snapshot invalide : "+e.message)}}
render();
}
function render(){
if(projectionError){$('view').innerHTML='<div class="notice danger">Projection refusée : '+safe(projectionError)+'</div>';return}if(!LIBS.length){$('view').innerHTML='<div class="panel empty"><strong>Registre vide</strong>Aucune bibliothèque déclarée ; ce n’est pas une preuve de collection vide.</div>';return}const titles={dashboard:"Tableau de bord",libraries:"Bibliothèques",collection:"Collection",compare:"Comparaison"};
$("breadcrumb").textContent=titles[s.route]||"Tableau de bord";
document.querySelectorAll("[data-route]").forEach(x=>{x.classList.toggle("active",x.dataset.route===s.route);x.setAttribute("aria-current",x.dataset.route===s.route?"page":"false")});
$("view").innerHTML=(routeHistory.length?'<div class="row" style="margin-bottom:12px">'+button("← Retour","back","btn-sm")+'</div>':"")+({dashboard,libraries,collection,compare:comparePage}[s.route]||dashboard)();
}
document.addEventListener("click",e=>{
const nav=e.target.closest("[data-route]");if(nav){route(nav.dataset.route);return}
const el=e.target.closest("[data-action]");if(!el)return;
const [act,...rest]=el.dataset.action.split(":"),id=rest.join(":");
if(act==="go")route(id);
else if(act==="back")goBack();
else if(act==="snapshot-select"){const parts=id.split(":");chooseLibrary(parts[0]);const target=getSnap(lib(),parts[1]);if(target){s.after=target.generationId;s.before=lib().snapshots.find(x=>x.snapshotId!==target.snapshotId)?.generationId||s.before}route("compare")}
else if(act==="library"){chooseLibrary(id);route("libraries")}
else if(act==="run-compare")runCompare();
else if(act==="filter"){s.filter=s.filter===id?"ALL":id;s.selectedUnit=null;render()}
else if(act==="unit"){s.selectedUnit=id;render()}
else if(act==="game"){s.collectionUnit=id;render()}
else if(act==="catalogue-game"){s.collectionUnit=id;render()}
else if(act==="close-game"){s.collectionUnit=null;render()}
else if(act==="snapshot"){const parts=id.split(":");chooseLibrary(parts[0]);const target=getSnap(lib(),parts[1]);if(target){s.after=target.generationId;s.before=lib().snapshots.find(x=>x.snapshotId!==target.snapshotId)?.generationId||s.before}route("compare")}
});
document.addEventListener("keydown",e=>{if((e.key==="Enter"||e.key===" ")&&e.target.matches("tr[data-action]")){e.preventDefault();e.target.click()}});
function importSyntheticCatalogue(file){
 const msg=$("toast"),report=message=>{if(msg){msg.textContent=message;msg.hidden=false}};
 if(!file)return;
 if(file.size>5*1024*1024){report("Import refusé : JSON synthétique supérieur à 5 Mio.");return}
 // Read only the explicitly selected local synthetic PS export; no network or writes.
 file.text().then(raw=>{
  if(raw.charCodeAt(0)===0xFEFF)raw=raw.slice(1); // UTF-8 BOM only, no text repair.
  if(raw.includes("\uFFFD"))throw Error("UNICODE_REPLACEMENT_CHARACTER");
  const dto=JSON.parse(raw);
  if(dto.schemaVersion!=="0.2.0-ui-catalogue-candidate")throw Error("UNSUPPORTED_SYNTHETIC_CATALOGUE_VERSION");
  if(!window.RM_T3_CATALOGUE||!window.RM_T2_CATALOGUE||!window.RM_PROJECTION)throw Error("CATALOGUE_DEPENDENCY_MISSING");
  const model=window.RM_T2_CATALOGUE.normalize(dto,[]);
  const cards=window.RM_PROJECTION.registry(dto.registry);
  if(!cards.length)throw Error("SYNTHETIC_REGISTRY_EMPTY");
  // Mutate view state only after complete fail-closed contract validation.
  payload={registry:dto.registry,catalogue:dto,comparisons:[]};window.RM_PROJECTION_DATA=payload;
  projectionMode=true;projectionError=null;LIBS=cards;routeHistory.length=0;
  s.route="collection";s.libraryId=cards[0].libraryId;s.before=cards[0].snapshots[0]?.generationId||"";
  s.after=cards[0].snapshots[1]?.generationId||"";s.result=null;s.selectedUnit=null;s.collectionUnit=null;
  s.collectionLibrary="ALL";s.collectionPlatform="ALL";s.collectionStatus="ALL";s.collectionCoverage="ALL";s.collectionSearch="";
  render();report("JSON synthétique accepté : "+model.games.length+" jeu(x) observé(s). Aucun accès à des ROM.");
 }).catch(err=>report("Import refusé : "+String(err.message||err)));
}
document.addEventListener("change",e=>{
const id=e.target.id,v=e.target.value;
if(id==="synthetic-catalogue-import"){importSyntheticCatalogue(e.target.files?.[0]);return}
if(id==="lib-select"){chooseLibrary(v);render()}
else if(id==="before-select"){s.before=v;s.result=null;render()}
else if(id==="after-select"){s.after=v;s.result=null;render()}
else if(id==="filter-class"){s.filter=v;s.selectedUnit=null;render()}
else if(id==="filter-console"){s.console=v;s.selectedUnit=null;render()}
else if(id==="collection-library"){s.collectionLibrary=v;s.collectionUnit=null;render()}
else if(id==="collection-mode"){s.collectionMode=v;render()}
else if(id==="collection-platform"){s.collectionPlatform=v;s.collectionUnit=null;render()}
else if(id==="collection-status"){s.collectionStatus=v;s.collectionUnit=null;render()}
else if(id==="collection-coverage"){s.collectionCoverage=v;s.collectionUnit=null;render()}
else if(id==="collection-sort"){s.collectionSort=v;render()}
});
document.addEventListener("input",e=>{
if(e.target.id==="filter-search"){const v=e.target.value;s.search=v;const pos=e.target.selectionStart;render();const next=$("filter-search");next.focus();next.setSelectionRange(pos,pos)}
if(e.target.id==="collection-search"){const v=e.target.value;s.collectionSearch=v;const pos=e.target.selectionStart;render();const next=$("collection-search");next.focus();next.setSelectionRange(pos,pos)}
});
render();
})();