/* STAB-03B / INT-01 — same application, three synthetic entry modes.
 * node prototype/ui11/ui-journey.test.cjs
 * DOM simulated, not an Edge accessibility or PS5.1 parity proof. */
"use strict";
const fs=require("node:fs"),path=require("node:path"),vm=require("node:vm"),assert=require("node:assert/strict");
const common=["fixtures.js","projection-adapter.js"];
const scenarios=[
 {name:"standard",scripts:[...common,"projection-data.js","catalogue-adapter.js","../../adapters/shared/catalogue-t3-consumer.js","t2-catalogue-model.js","t2-catalogue-fixture.js","app.js"],
  game:["lib-arcade","Arcade",""],comparison:"observed"},
 {name:"integrated",scripts:[...common,"projection-integrated.js","catalogue-adapter.js","t2-catalogue-model.js","app.js"],
  game:["lib-demo","GBA","game-demo"],comparison:"precomputed"},
 {name:"t4",scripts:[...common,"t4-catalogue-demo.js","catalogue-adapter.js","../../adapters/shared/catalogue-t3-consumer.js","t2-catalogue-model.js","t2-catalogue-fixture.js","app.js"],
  game:["lib-t3-ref","GBA","game-ref"],comparison:"unavailable"}
];
let checks=0;
const check=(condition,message)=>{assert.ok(condition,message);checks++};
function boot(spec){
 const nodes=new Map(),events={},window={};
 const element=id=>{if(!nodes.has(id))nodes.set(id,{innerHTML:"",textContent:"",hidden:true,focus(){},setSelectionRange(){},classList:{toggle(){}},setAttribute(){}});return nodes.get(id)};
 const document={getElementById:element,querySelectorAll:()=>[],addEventListener:(k,fn)=>events[k]=fn};
 const ctx=vm.createContext({window,document,Date,Intl,Map,Set,Object,Array,String,Number,RegExp,Error,console});
 for(const file of spec.scripts)vm.runInContext(fs.readFileSync(path.join(__dirname,file),"utf8"),ctx,{filename:file});
 const click=(action,route=false)=>events.click({target:{closest:sel=>
  route?(sel==="[data-route]"?{dataset:{route:action}}:null):
   (sel==="[data-action]"?{dataset:{action}}:null)}});
 const input=(id,value)=>events.input({target:{id,value,selectionStart:value.length}});
 const change=(id,value)=>events.change({target:{id,value}});
 return {window,element,events,click,input,change};
}
for(const spec of scenarios){
 const a=boot(spec),view=()=>a.element("view").innerHTML;
 check(/Votre collection/.test(view()),spec.name+": dashboard");
 a.click("libraries",true);
 check(/Vos espaces de jeux/.test(view()),spec.name+": libraries");
 a.click("compare",true);
 check(/Comparer deux générations/.test(view()),spec.name+": compare");
 a.click("run-compare");
 if(spec.comparison==="unavailable"){
  check(/Deux snapshots accessibles sont requis|Sélectionnez deux générations distinctes|Comparaison/.test(view()),"T4: never invent missing pair");
 }else{
  check(/Dénominateur observé|Ajout observé|Observation identique/.test(view()),spec.name+": comparison results");
 }
 a.click("collection",true);
 check(/Catalogue des jeux|Explorer la collection/.test(view()),spec.name+": collection");
 check(!/Projection refusée/.test(view()),spec.name+": no projection exception");
 const dto=spec.name==="integrated"?a.window.RM_PROJECTION_DATA.catalogue:
  spec.name==="t4"?a.window.RM_PROJECTION_DATA.catalogue:a.window.RM_T2_FIXTURE;
 const game=dto.games[0],p=spec.name==="integrated"?game.provenance[0]:game.provenance;
 const libraryId=spec.name==="integrated"?p.libraryId:game.libraryId;
 const identity=JSON.stringify([libraryId,game.platform,game.gameId]);
 check(view().includes(game.title),spec.name+": game listed");
 a.click("catalogue-game:"+identity);
 check(/Fermer la fiche/.test(view()),spec.name+": game inspector opened");
 check(view().includes(game.gameId),spec.name+": identity displayed");
 const search=spec.name==="t4"?"Étoile":game.title.slice(0,4);
 a.input("collection-search",search);
 check(view().includes(game.title),spec.name+": search preserved");
 a.input("collection-search","not-a-matching-synthetic-game");
 check(/Aucun résultat dans ce filtre/.test(view()),spec.name+": search filters");
 a.input("collection-search","");
 a.change("collection-platform",game.platform);
 check(view().includes(game.title),spec.name+": platform filter");
 a.click("close-game");
 check(!/Fermer la fiche/.test(view()),spec.name+": inspector closed");
 a.click("dashboard",true);
 check(/Votre collection/.test(view()),spec.name+": return dashboard");
 console.log("PASS STAB03B_UI_JOURNEY "+spec.name);
}
console.log("STAB03B_UI_JOURNEY_PASS checks="+checks+" | modes=3 | simulated_dom=true | edge=NOT_EXECUTED");
