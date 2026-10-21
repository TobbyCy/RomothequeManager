/* Entirely fictional catalogue candidate. No real games, ROMs, paths or metadata. */
(function(global){"use strict";
const p=(libraryId,snapshotId,observedAtUtc)=>({libraryId,snapshotId,observedAtUtc,source:"SYNTHETIC_FIXTURE"});
const o=(libraryId,snapshotId,romUnitId,variantId,observationStatus="OBSERVED")=>({libraryId,snapshotId,romUnitId,variantId,observationStatus});
global.RM_T2_FIXTURE=Object.freeze({schemaVersion:"0.1.0-ui-catalogue-candidate",readOnly:true,games:[
{gameId:"game-mario",platform:"GBA",title:"Super Plombier Advance",status:"KNOWN",coverStatus:"UNKNOWN",variants:[{variantId:"var-eu",label:"Europe"}],provenance:[p("lib-arcade","arcade-002","2026-10-10T09:30:00Z")],observations:[o("lib-arcade","arcade-002","unit-mario","var-eu")]},
{gameId:"game-zelda",platform:"GBA",title:"Légende de la Forêt",status:"KNOWN",coverStatus:"UNKNOWN",variants:[{variantId:"var-eu",label:"Europe"}],provenance:[p("lib-arcade","arcade-002","2026-10-10T09:30:00Z")],observations:[o("lib-arcade","arcade-002","unit-zelda","var-eu")]},
{gameId:"game-orbit",platform:"PS1",title:"Orbit Racer",status:"INCOMPLETE",coverStatus:"UNKNOWN",variants:[{variantId:"var-us",label:"Amérique du Nord"}],provenance:[p("lib-arcade","arcade-002","2026-10-10T09:30:00Z")],observations:[o("lib-arcade","arcade-002","unit-conflict","var-us","PARTIAL")]},
{gameId:"game-pocket-1",platform:"GBA",title:"Pocket Adventures",status:"KNOWN",coverStatus:"KNOWN",variants:[{variantId:"var-eu",label:"Europe"}],provenance:[p("lib-pocket","pocket-002","2026-09-01T12:00:00Z")],observations:[o("lib-pocket","pocket-002","pocket-01","var-eu")]},
{gameId:"game-pocket-2",platform:"GBA",title:"Star Circuit",status:"INCOMPLETE",coverStatus:"UNKNOWN",variants:[{variantId:"var-jp",label:"Japon"}],provenance:[p("lib-pocket","pocket-002","2026-09-01T12:00:00Z")],observations:[o("lib-pocket","pocket-002","pocket-02","var-jp","PARTIAL")]}
]});
})(typeof window!=="undefined"?window:globalThis);
