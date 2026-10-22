"use strict";
/* VAL-01: real Windows Microsoft Edge / CDP. Synthetic file:// only; no npm packages. */
const fs=require("node:fs"),os=require("node:os"),path=require("node:path");
const {spawn}=require("node:child_process");
const {fileURLToPath,pathToFileURL}=require("node:url");
const project=path.resolve(__dirname,".."),ui=path.join(project,"prototype","ui11");
const pause=ms=>new Promise(resolve=>setTimeout(resolve,ms));
function insist(ok,reason){if(!ok)throw Error("EDGE_VAL01_"+reason)}
function findEdge(){
 const roots=[process.env["ProgramFiles(x86)"],process.env.ProgramFiles].filter(Boolean);
 return roots.map(root=>path.join(root,"Microsoft","Edge","Application","msedge.exe")).find(fs.existsSync);
}
class CDP{
 constructor(ws){
  this.ws=ws;this.id=0;this.waiting=new Map();this.errors=[];this.loads=[];
  ws.addEventListener("message",e=>{
   let msg;try{msg=JSON.parse(String(e.data))}catch(err){this.errors.push("INVALID_CDP_MESSAGE:"+err.message);return}
   if(msg.id){const p=this.waiting.get(msg.id);if(!p)return;this.waiting.delete(msg.id);clearTimeout(p.timeout);msg.error?p.reject(Error("CDP:"+msg.error.message)):p.resolve(msg.result||{});return}
   if(msg.method==="Runtime.exceptionThrown")this.errors.push("JS_EXCEPTION:"+JSON.stringify(msg.params.exceptionDetails).slice(0,1700));
   if(msg.method==="Log.entryAdded"&&msg.params.entry.level==="error")this.errors.push("BROWSER_LOG:"+msg.params.entry.text);
   if(msg.method==="Network.loadingFailed"&&!msg.params.canceled)this.errors.push("RESOURCE_FAILURE:"+msg.params.errorText);
   if(msg.method==="Network.requestWillBeSent"){
    const req=msg.params.request.url;
    if(req.startsWith("http:")||req.startsWith("https:"))this.errors.push("EXTERNAL_REQUEST:"+req);
    if(req.startsWith("file:")){
     try{const target=fileURLToPath(req),relative=path.relative(project,target);
      if(relative===".."||relative.startsWith(".."+path.sep)||path.isAbsolute(relative))this.errors.push("FILE_OUTSIDE_PROJECT:"+req)
     }catch(err){this.errors.push("FILE_URL_INVALID:"+err.message)}
    }
    this.loads.push(req);
   }
  });
  ws.addEventListener("close",()=>{for(const p of this.waiting.values()){clearTimeout(p.timeout);p.reject(Error("CDP_DISCONNECTED"))}this.waiting.clear()});
 }
 send(method,params={},sessionId){
  const id=++this.id;const payload={id,method,params};if(sessionId)payload.sessionId=sessionId;
  return new Promise((resolve,reject)=>{const timeout=setTimeout(()=>{this.waiting.delete(id);reject(Error("CDP_TIMEOUT:"+method))},12000);
   this.waiting.set(id,{resolve,reject,timeout});
   try{this.ws.send(JSON.stringify(payload))}catch(err){clearTimeout(timeout);this.waiting.delete(id);reject(err)}
  });
 }
}
async function browserSocket(profile){
 const file=path.join(profile,"DevToolsActivePort");
 for(let n=0;n<160;n++){
  if(fs.existsSync(file)){const parts=fs.readFileSync(file,"utf8").trim().split(/\r?\n/);
   if(/^\d+$/.test(parts[0])&&parts[1]&&parts[1].startsWith("/devtools/browser/"))return "ws://127.0.0.1:"+parts[0]+parts[1];
  }
  await pause(125);
 }
 throw Error("EDGE_DEVTOOLS_UNAVAILABLE");
}
async function connect(url){
 insist(typeof WebSocket==="function","NODE_WEBSOCKET_UNAVAILABLE");
 const ws=new WebSocket(url);
 await new Promise((resolve,reject)=>{
  const timeout=setTimeout(()=>reject(Error("EDGE_WEBSOCKET_TIMEOUT")),12000);
  ws.addEventListener("open",()=>{clearTimeout(timeout);resolve()},{once:true});
  ws.addEventListener("error",()=>{clearTimeout(timeout);reject(Error("EDGE_WEBSOCKET_ERROR"))},{once:true});
 });
 return ws;
}
async function run(){
 const edge=findEdge();insist(edge,"NOT_INSTALLED");
 for(const name of ["index.html","demo-integree.html"])insist(fs.existsSync(path.join(ui,name)),"MISSING_ENTRY_"+name);
 const tmp=fs.mkdtempSync(path.join(os.tmpdir(),"rm-edge-val01-"));
 let child=null,ws=null;
 let checks=0;
 const check=(condition,label)=>{insist(condition,label);checks++};
 try{
  child=spawn(edge,["--headless=new","--disable-gpu","--no-first-run","--no-default-browser-check","--disable-background-networking","--disable-extensions","--disable-default-apps","--remote-debugging-port=0","--user-data-dir="+tmp,"--window-size=1365,900","about:blank"],{stdio:"ignore",windowsHide:true});
  const wsUrl=await browserSocket(tmp);ws=await connect(wsUrl);
  const client=new CDP(ws),version=await client.send("Browser.getVersion");
  insist(/Edg\//.test(version.product+" "+version.userAgent),"NOT_MICROSOFT_EDGE");
  const target=await client.send("Target.createTarget",{url:"about:blank"});
  const session=(await client.send("Target.attachToTarget",{targetId:target.targetId,flatten:true})).sessionId;
  await client.send("Page.enable",{},session);
  await client.send("Runtime.enable",{},session);
  await client.send("Log.enable",{},session);
  await client.send("Network.enable",{},session);
  await client.send("Network.setBlockedURLs",{urls:["http://*","https://*"]},session);
  async function evaluate(expression){
   const result=await client.send("Runtime.evaluate",{expression,returnByValue:true,awaitPromise:true},session);
   if(result.exceptionDetails)throw Error("JS_EVALUATE:"+JSON.stringify(result.exceptionDetails).slice(0,1500));
   return result.result.value;
  }
  async function waitFor(expression,label){
   for(let i=0;i<60;i++){if(await evaluate(expression))return;await pause(100)}
   throw Error("UI_WAIT_TIMEOUT:"+label);
  }
  async function page(entry){
   client.errors=[];client.loads=[];
   const url=pathToFileURL(path.join(ui,entry)).href;
   const result=await client.send("Page.navigate",{url},session);
   insist(!result.errorText,"PAGE_NAVIGATE_"+entry+":"+result.errorText);
   await waitFor('document.readyState==="complete" && !!document.querySelector("#view h1")',entry);
   const title=await evaluate('document.title');
   check(title.includes("ROMothèque Manager"),"TITLE_"+entry);
   check(await evaluate('getComputedStyle(document.body).backgroundColor !== "rgba(0, 0, 0, 0)"'),"CSS_"+entry);
   check(await evaluate('typeof window.RM_FIXTURES==="object" && typeof window.RM_PROJECTION==="object"'),"SCRIPTS_"+entry);
   check(client.loads.some(item=>item===url),"FILE_NAVIGATED_"+entry);
   assertNoErrors("load-"+entry);
  }
  function assertNoErrors(stage){check(client.errors.length===0,"BROWSER_ERRORS_"+stage+":"+client.errors.join(" | "))}
  async function view(){return evaluate('document.querySelector("#view").innerText')}
  async function contains(part,label){check((await view()).includes(part),label)}
  async function click(selector){
   const rect=await evaluate('(function(){const el=document.querySelector('+JSON.stringify(selector)+');if(!el)return null;el.scrollIntoView({block:"center"});const r=el.getBoundingClientRect();return {x:r.left+r.width/2,y:r.top+r.height/2,w:r.width,h:r.height,disabled:!!el.disabled}})()');
   insist(rect&&rect.w>0&&rect.h>0&&!rect.disabled,"CLICK_TARGET_"+selector);
   await client.send("Input.dispatchMouseEvent",{type:"mouseMoved",x:rect.x,y:rect.y},session);
   await client.send("Input.dispatchMouseEvent",{type:"mousePressed",x:rect.x,y:rect.y,button:"left",clickCount:1},session);
   await client.send("Input.dispatchMouseEvent",{type:"mouseReleased",x:rect.x,y:rect.y,button:"left",clickCount:1},session);
   checks++;
  }
  async function select(selector,value){
   const ok=await evaluate('(function(){const e=document.querySelector('+JSON.stringify(selector)+');if(!e||![...e.options].some(o=>o.value==='+JSON.stringify(value)+'))return false;e.value='+JSON.stringify(value)+';e.dispatchEvent(new Event("change",{bubbles:true}));return true})()');
   check(ok,"SELECT_"+selector+"_"+value);
  }
  await page("index.html");
  await contains("Votre collection","STANDARD_DASHBOARD");
  await click('[data-action="go:libraries"]');
  await contains("Vos espaces de jeux","STANDARD_LIBRARIES_NAV");
  await click('[data-action="library:lib-pocket"]');
  await contains("Pocket nomade","OFFLINE_LIBRARY");
  await contains("Hors ligne","OFFLINE_FLAG");
  await contains("À vérifier","STALE_FLAG");
  await click('[data-action="library:lib-optical"]');
  await contains("Historique indisponible","INACCESSIBLE_INDEX");
  await click('[data-route="compare"]');
  await contains("Impossible de comparer","INACCESSIBLE_REFUSAL");
  check(!/fichiers supprimés|jeux ont disparu/i.test(await view()),"NO_FALSE_DELETION");
  await select("#lib-select","lib-arcade");
  await click('[data-action="run-compare"]');
  await contains("Dénominateur observé","STANDARD_COMPARISON");
  await click('[data-route="collection"]');
  await contains("Catalogue des jeux","STANDARD_COLLECTION");
  await click('[data-action^="catalogue-game:"]');
  await contains("Fermer la fiche","STANDARD_GAME_DETAIL");
  assertNoErrors("standard-journey");
  console.log("EDGE_VAL01_SCENARIO_PASS standard_navigation_offline_stale_inaccessible_compare_collection");
  await page("demo-integree.html");
  await contains("Votre collection","INTEGRATED_DASHBOARD");
  await click('[data-route="libraries"]');
  await contains("Vos espaces de jeux","INTEGRATED_LIBRARIES");
  await click('[data-action="library:lib-mixed"]');
  await contains("Bibliothèque multi-états","INTEGRATED_MIXED");
  await click('[data-route="compare"]');
  await click('[data-action="run-compare"]');
  await contains("Dénominateur observé","INTEGRATED_COMPARE");
  await contains("non prouvé","INTEGRATED_INCOMPLETE_SCOPE");
  await contains("Information insuffisante","INTEGRATED_UNKNOWN_CLASS");
  check(!/fichiers supprimés|jeux ont disparu/i.test(await view()),"INTEGRATED_NO_DELETION_CLAIM");
  await click('[data-action="unit:unit-unknown"]');
  await contains("Information insuffisante","INTEGRATED_UNKNOWN_DETAIL");
  await select("#lib-select","lib-closed");
  await contains("Impossible de comparer","INTEGRATED_INACCESSIBLE_NEGATIVE");
  await click('[data-route="collection"]');
  await contains("Catalogue des jeux","INTEGRATED_COLLECTION");
  await click('[data-action^="catalogue-game:"]');
  await contains("Fermer la fiche","INTEGRATED_GAME_DETAIL");
  assertNoErrors("integrated-journey");
  console.log("EDGE_VAL01_SCENARIO_PASS integrated_mixed_scope_unknown_refusal_collection");
  check(client.loads.every(u=>u.startsWith("file:")||u.startsWith("data:")),"EXTERNAL_RESOURCE_DETECTED");
  console.log("BROWSER_REAL_PASS | browser="+version.product+" | platform="+process.platform+
   " | node="+process.version+" | source="+(process.env.GITHUB_SHA||"LOCAL")+
   " | checks="+checks+" | pages=2 | mouse=CDP_real_input | jsExceptions=0 | synthetic=1 | remoteNetwork=0");
 }finally{
  if(ws)ws.close();
  if(child){child.kill();await pause(250)}
  fs.rmSync(tmp,{recursive:true,force:true});
 }
}
run().catch(err=>{console.error("BROWSER_REAL_FAIL | "+(err.stack||err));process.exitCode=1});