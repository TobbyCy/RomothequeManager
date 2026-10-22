/* UI #11 — all names, dates and identities below are synthetic. No disk or network access. */
(function(){
"use strict";
const a="a".repeat(64), b="b".repeat(64), c="c".repeat(64);
const unit=(id,game,variant,console,digest,title)=>({romUnitId:id,gameId:game,variantId:variant,console,observationDigest:digest,title});
const coverage=(gba,ps1)=>[{console:"GBA",status:gba},{console:"PS1",status:ps1}];
const snap=(libraryId,generationId,capturedAtUtc,scanStatus,sourceAvailability,freshness,coverage,units)=>({
schemaVersion:"1.0.0",libraryId,generationId,capturedAtUtc,scanStatus,sourceAvailability,freshness,coverage,units
});
const arcadeA=snap("lib-arcade","arcade-001","2026-09-20T10:00:00Z","complete","ONLINE","FRESH",coverage("complete","complete"),[
unit("unit-mario","game-mario","var-eu","GBA",a,"Super Plombier Advance"),
unit("unit-zelda","game-zelda","var-eu","GBA",a,"Légende de la Forêt"),
unit("unit-chrono","game-chrono","var-eu","PS1",a,"Chroniques du Temps"),
unit("unit-puzzle","game-puzzle","var-eu","GBA",a,"Puzzle Cosmos"),
unit("unit-conflict","game-orbit","var-eu","PS1",a,"Orbit Racer"),
unit("unit-nohash","game-nohash","var-eu","GBA",null,"Mystery Quest")]);
const arcadeB=snap("lib-arcade","arcade-002","2026-10-10T09:30:00Z","complete","ONLINE","FRESH",coverage("complete","complete"),[
unit("unit-mario","game-mario","var-eu","GBA",a,"Super Plombier Advance"),
unit("unit-zelda","game-zelda","var-eu","GBA",b,"Légende de la Forêt"),
unit("unit-new","game-new","var-eu","GBA",b,"Sky Explorer"),
unit("unit-conflict","game-orbit","var-us","PS1",a,"Orbit Racer"),
unit("unit-nohash","game-nohash","var-eu","GBA",null,"Mystery Quest")]);
const arcadeC=snap("lib-arcade","arcade-003","2026-10-20T15:45:00Z","partial","ONLINE","STALE",coverage("partial","unobserved"),[
unit("unit-mario","game-mario","var-eu","GBA",c,"Super Plombier Advance"),
unit("unit-new","game-new","var-eu","GBA",b,"Sky Explorer")]);
const pocketA=snap("lib-pocket","pocket-001","2026-08-04T12:00:00Z","complete","ONLINE","FRESH",[{console:"GBA",status:"complete"}],[
unit("pocket-01","game-pocket-1","var-eu","GBA",a,"Pocket Adventures"),
unit("pocket-02","game-pocket-2","var-jp","GBA",b,"Star Circuit")]);
const pocketB=snap("lib-pocket","pocket-002","2026-09-01T12:00:00Z","complete","OFFLINE","STALE",[{console:"GBA",status:"complete"}],[
unit("pocket-01","game-pocket-1","var-eu","GBA",a,"Pocket Adventures"),
unit("pocket-02","game-pocket-2","var-jp","GBA",b,"Star Circuit")]);
const unknownA=snap("lib-unknown","unknown-001","2026-10-17T09:00:00Z","partial","UNKNOWN","UNKNOWN",[{console:"GBA",status:"partial"}],[]);
const unknownB=snap("lib-unknown","unknown-002","2026-10-20T09:00:00Z","failed","UNKNOWN","UNKNOWN",[{console:"GBA",status:"unobserved"}],[]);
const libraries=[
{libraryId:"lib-arcade",name:"Arcade & classiques",description:"Collection multi-plateformes de démonstration",platforms:["GBA","PS1"],availability:"ONLINE",freshness:"FRESH",observedAtUtc:"2026-10-21T12:00:00Z",indexAvailable:true,lastVerifiedCompleteRef:"arcade-002",snapshots:[arcadeA,arcadeB,arcadeC]},
{libraryId:"lib-pocket",name:"Pocket nomade",description:"Support USB déconnecté, historique conservé",platforms:["GBA"],availability:"OFFLINE",freshness:"STALE",observedAtUtc:"2026-10-21T11:00:00Z",indexAvailable:true,lastVerifiedCompleteRef:"pocket-002",snapshots:[pocketA,pocketB]},
{libraryId:"lib-optical",name:"Archives optiques",description:"Index local indisponible — nombre de jeux inconnu",platforms:["PS1"],availability:"INACCESSIBLE",freshness:"UNKNOWN",observedAtUtc:"2026-10-20T08:00:00Z",indexAvailable:false,lastVerifiedCompleteRef:null,snapshots:[]},
{libraryId:"lib-unknown",name:"Source à identifier",description:"Dernière tentative incomplète, aucune unité prouvée",platforms:[],availability:"UNKNOWN",freshness:"UNKNOWN",observedAtUtc:null,indexAvailable:true,lastVerifiedCompleteRef:null,snapshots:[unknownA,unknownB]}
];
window.RM_FIXTURES=Object.freeze({libraries});
})();