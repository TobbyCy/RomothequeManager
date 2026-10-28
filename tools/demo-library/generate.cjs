#!/usr/bin/env node
(function demo(){
'use strict';
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),zlib=require('node:zlib');
const rootRepo=path.resolve(__dirname,'../..');
const catalogDefault=path.resolve(__dirname,'../../contracts/demo-library-01/catalogue.json');
const MARK='DEMO01_SIMULATED_NON_EXECUTABLE_NO_ROM_OR_BIOS_CONTENT';
const types=[['image','covers',240,336],['thumbnail','thumbnails',120,168],['marquee','logos',320,96],['screenshot','screenshots',320,180],['backcover','backcovers',240,336],['background','backgrounds',480,270]];
const SIGN=Buffer.from('89504e470d0a1a0a','hex');
const font={
 A:["01110","10001","10001","11111","10001","10001","10001"],
 B:["11110","10001","10001","11110","10001","10001","11110"],
 C:["01111","10000","10000","10000","10000","10000","01111"],
 D:["11110","10001","10001","10001","10001","10001","11110"],
 E:["11111","10000","10000","11110","10000","10000","11111"],
 F:["11111","10000","10000","11110","10000","10000","10000"],
 G:["01111","10000","10000","10111","10001","10001","01111"],
 H:["10001","10001","10001","11111","10001","10001","10001"],
 I:["11111","00100","00100","00100","00100","00100","11111"],
 J:["00111","00010","00010","00010","10010","10010","01100"],
 K:["10001","10010","10100","11000","10100","10010","10001"],
 L:["10000","10000","10000","10000","10000","10000","11111"],
 M:["10001","11011","10101","10101","10001","10001","10001"],
 N:["10001","11001","10101","10011","10001","10001","10001"],
 O:["01110","10001","10001","10001","10001","10001","01110"],
 P:["11110","10001","10001","11110","10000","10000","10000"],
 Q:["01110","10001","10001","10001","10101","10010","01101"],
 R:["11110","10001","10001","11110","10100","10010","10001"],
 S:["01111","10000","10000","01110","00001","00001","11110"],
 T:["11111","00100","00100","00100","00100","00100","00100"],
 U:["10001","10001","10001","10001","10001","10001","01110"],
 V:["10001","10001","10001","10001","10001","01010","00100"],
 W:["10001","10001","10001","10101","10101","10101","01010"],
 X:["10001","10001","01010","00100","01010","10001","10001"],
 Y:["10001","10001","01010","00100","00100","00100","00100"],
 Z:["11111","00001","00010","00100","01000","10000","11111"],
 '0':["01110","10011","10101","10101","11001","10001","01110"],
 '1':["00100","01100","00100","00100","00100","00100","01110"],
 '2':["01110","10001","00001","00010","00100","01000","11111"],
 '3':["11110","00001","00001","01110","00001","00001","11110"],
 '4':["00010","00110","01010","10010","11111","00010","00010"],
 '5':["11111","10000","10000","11110","00001","00001","11110"],
 '6':["01110","10000","10000","11110","10001","10001","01110"],
 '7':["11111","00001","00010","00100","01000","01000","01000"],
 '8':["01110","10001","10001","01110","10001","10001","01110"],
 '9':["01110","10001","10001","01111","00001","00001","01110"],
 ':':["00000","00100","00100","00000","00100","00100","00000"],
 '-':["00000","00000","00000","11111","00000","00000","00000"],
 '&':["01100","10010","10100","01000","10101","10010","01101"],
 '.':["00000","00000","00000","00000","00000","00110","00110"],
 ' ':["00000","00000","00000","00000","00000","00000","00000"]};
const crcTable=Array.from({length:256},(_,n)=>{let c=n;for(let k=0;k<8;k++)c=(c&1)?(0xedb88320^(c>>>1)):(c>>>1);return c>>>0;});
function sha(x){return crypto.createHash('sha256').update(x).digest('hex');}
function chunk(name,body){const n=Buffer.alloc(4),tag=Buffer.from(name),crc=Buffer.alloc(4);n.writeUInt32BE(body.length);let c=0xffffffff;for(const b of Buffer.concat([tag,body]))c=(c>>>8)^crcTable[(c^b)&255];crc.writeUInt32BE((c^0xffffffff)>>>0);return Buffer.concat([n,tag,body,crc]);}
function png(w,h,data){const raw=Buffer.alloc((w*3+1)*h);for(let y=0;y<h;y++){const offset=y*(1+w*3);data.copy(raw,offset+1,y*w*3,(y+1)*w*3);}
const ih=Buffer.alloc(13);ih.writeUInt32BE(w,0);ih.writeUInt32BE(h,4);ih[8]=8;ih[9]=2;
return Buffer.concat([SIGN,chunk('IHDR',ih),chunk('IDAT',zlib.deflateSync(raw,{level:6})),chunk('IEND',Buffer.alloc(0))]);}
function art(game,kind,w,h){
const b=Buffer.alloc(w*h*3),s=crypto.createHash('sha256').update(game.id+'|'+kind).digest();
const set=(x,y,r,g,bb)=>{if(x<0||y<0||x>=w||y>=h)return;const i=(y*w+x)*3;b[i]=r;b[i+1]=g;b[i+2]=bb;};
const rect=(x,y,ww,hh,r,g,bb)=>{for(let yy=Math.max(0,y);yy<Math.min(h,y+hh);yy++)for(let xx=Math.max(0,x);xx<Math.min(w,x+ww);xx++)set(xx,yy,r,g,bb);};
for(let y=0;y<h;y++)for(let x=0;x<w;x++){let z=Math.round(y*25/h),q=(x+y+s[5])%55<8?22:0;set(x,y,36+s[0]%110+q+z,35+s[1]%100+z,65+s[2]%100+q);}
const header=Math.max(24,Math.round(h*.24));rect(0,0,w,header,17,23,37);rect(0,h-24,w,24,17,23,37);
rect(Math.floor(w*.16),Math.floor(h*.37),Math.floor(w*.66),Math.floor(h*.27),218,190+s[3]%60,89);
rect(Math.floor(w*.16)+8,Math.floor(h*.37)+8,Math.floor(w*.66)-16,Math.floor(h*.27)-16,37+s[6]%90,45+s[7]%60,86);
for(let j=0;j<4;j++)rect((s[j]*23+j*60)%w,header+j*3,Math.max(3,w>>6),h>>1,230,194,122);
function line(t,y,scale,white=true){let x=Math.max(3,Math.floor((w-(t.length*6-1)*scale)/2));for(const c of t){const rows=font[c]||font[' '];rows.forEach((r,ry)=>{for(let rx=0;rx<5;rx++)if(r[rx]==='1')rect(x+rx*scale,y+ry*scale,scale,scale,white?250:5,white?249:5,white?234:5);});x+=6*scale;}}
let text=game.title.normalize('NFKD').replace(/[\u0300-\u036f]/g,'').toUpperCase().replace(/[^A-Z0-9:&.\- ]/g,' ').replace(/\s+/g,' ').trim();
const size=w>=240?2:1, limit=Math.max(7,Math.floor((w-16)/(6*size))),lines=[];let current='';
for(const word of text.split(' ')){if((current+' '+word).trim().length>limit&&current){lines.push(current);current=word;}else current=(current+' '+word).trim();}
if(current)lines.push(current);
let y=Math.max(3,Math.floor((header-Math.min(3,lines.length)*8*size)/2));
for(const t of lines.slice(0,3)){line(t.slice(0,limit),y,size);y+=8*size;}
const foot=(game.id+' '+kind).toUpperCase().replace(/[^A-Z0-9 ]/g,' ');
line(foot.slice(0,Math.floor((w-12)/6)),h-16,1);
return png(w,h,b);
}
function relative(p){
if(typeof p!=='string'||!p||p.length>220||p.includes('\\')||p.includes(':')||p.startsWith('/')||p.includes('\0')||p.split('/').some(s=>!s||s==='.'||s==='..'||/[<>:"|?*\u0000-\u001f]/.test(s)||/[. ]$/.test(s)))throw Error('Unsafe relative path');
return p;
}
function safeOutput(value){
if(typeof value!=='string'||!path.isAbsolute(value)||/^[\\/]{2}/.test(value)||value.includes('://')||value.includes('\0'))throw Error('Explicit absolute local --output required');
const target=path.resolve(value), parent=path.dirname(target);
if(target.toLowerCase()===rootRepo.toLowerCase()||target.toLowerCase().startsWith(rootRepo.toLowerCase()+path.sep))throw Error('Output under application repository forbidden');
if(target===path.parse(target).root||fs.existsSync(target))throw Error('Output must not exist');
if(!fs.existsSync(parent)||!fs.statSync(parent).isDirectory())throw Error('Output parent must already exist');
let current=path.parse(parent).root;
for(const part of parent.slice(current.length).split(path.sep).filter(Boolean)){current=path.join(current,part);if(fs.lstatSync(current).isSymbolicLink())throw Error('Symlink ancestor refused');}
return target;
}
const xml=s=>String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;').replace(/'/g,'&apos;');
function generate(target,catalog){
 const output=safeOutput(target);
 const catalogBytes=fs.readFileSync(catalog);
 if(catalogBytes.length>1048576)throw Error('Catalogue larger than 1 MiB');
 const data=JSON.parse(catalogBytes.toString('utf8'));
 const contractDir=path.dirname(catalog);
 const scenariosBytes=fs.readFileSync(path.join(contractDir,'scenarios.json'));
 const oraclesBytes=fs.readFileSync(path.join(contractDir,'oracles.json'));
 const scenarios=JSON.parse(scenariosBytes.toString('utf8'));
 const oracles=JSON.parse(oraclesBytes.toString('utf8'));
 if(data.schemaVersion!=='demo-library-01/1'||
    scenarios.schemaVersion!=='demo-library-01/scenarios/1'||
    oracles.schemaVersion!=='demo-library-01/oracles/1'||
    !Array.isArray(data.games)||data.games.length!==30||
    !Array.isArray(data.platformSlugs)||data.platformSlugs.length!==13||
    !Array.isArray(scenarios.variants)||scenarios.variants.length!==3)
   throw Error('Invalid DEV2 canonical contract versions/shape');
 const platforms=new Set(data.platformSlugs);
 if(platforms.size!==13||!['xbox','ps2','ps3','ps4','pc'].every(p=>platforms.has(p)))
   throw Error('13 canonical platforms including Xbox, PS2/3/4 and PC required');
 const byId=new Map(),romKeys=new Set();
 const mediaDimensions={image:[240,336],thumbnail:[120,168],marquee:[320,96],
   screenshot:[320,180],backcover:[240,336],background:[480,270]};
 for(const game of data.games){
   if(!/^demo01-[0-9]{3}$/.test(game.id)||byId.has(game.id)||!platforms.has(game.platform))
     throw Error('Duplicate/invalid ID or unknown platform');
   for(const prop of ['title','genre','description','publisher','region','edition'])
     if(typeof game[prop]!=='string'||!game[prop].trim()||game[prop].length>1200)
       throw Error('Invalid canonical property '+prop);
   if(!Number.isInteger(game.year)||game.year<1980||game.year>2099)
     throw Error('Invalid fictional release year');
   relative(game.romFile);
   if(game.romFile.includes('/')||!/\.(gb|gba|nes|sfc|bin|z64|chd|m3u)$/i.test(game.romFile))
     throw Error('Invalid synthetic ROM filename');
   const key=(game.platform+'/'+game.romFile).toLowerCase();
   if(romKeys.has(key))throw Error('Duplicate canonical ROM filename');
   romKeys.add(key);
   if(!game.media||Object.keys(mediaDimensions).some(k=>
      typeof game.media[k]!=='string'||!relative(game.media[k])||
      !game.media[k].startsWith('MEDIAS/'+game.platform+'/')))
     throw Error('Invalid canonical media');
   if(game.discs!==undefined && (
       !game.romFile.endsWith('.m3u')||!Array.isArray(game.discs)||
       game.discs.length<2||game.discs.length>4||
       game.discs.some(x=>typeof x!=='string'||!relative(x)||x.includes('/')||!x.endsWith('.chd'))))
     throw Error('Invalid multidisc declaration');
   byId.set(game.id,game);
 }
 const refIds=[...byId.keys()].sort();
 const plan=new Map(),outVariants=[],seenVariants=new Set();
 function add(relativePath,value,kind,id=null){
   relative(relativePath);
   const k=relativePath.toLowerCase();
   if(plan.has(k))throw Error('Case-insensitive output collision '+relativePath);
   plan.set(k,{path:relativePath,bytes:Buffer.isBuffer(value)?value:Buffer.from(value,'utf8'),kind,id});
 }
 for(const variant of scenarios.variants){
   const name=variant.name;
   if(!['IDEAL','CHAOS','MYSTERY'].includes(name)||seenVariants.has(name))
      throw Error('Invalid/duplicate variant');
   seenVariants.add(name);
   if(!Array.isArray(variant.entries)||variant.entries.length!==30||
      !Array.isArray(variant.xmlPaths))throw Error('Invalid scenario entries/XML');
   if(JSON.stringify(variant.entries.map(e=>e.id).sort())!==JSON.stringify(refIds))
      throw Error('Scenario ID mismatch');
   const xmlByPlatform=new Map(),records=[];
   let absentRomReferences=0, duplicateFiles=0;
   for(const entry of variant.entries){
     const game=byId.get(entry.id),plat=game.platform;
     if(!game||typeof entry.romPath!=='string'||!relative(entry.romPath)||
        !entry.romPath.startsWith('ROMS/'+plat+'/'))
       throw Error('Unsafe scenario ROM path');
     if(typeof entry.romPresent!=='boolean'||typeof entry.xmlEntryPresent!=='boolean'||
        !Array.isArray(entry.discPaths)||!Array.isArray(entry.discPresent)||
        entry.discPaths.length!==entry.discPresent.length ||
        (entry.discPaths.length !== (game.discs?.length||0)) ||
        !entry.media)throw Error('Scenario entry shape mismatch');
     const payload=MARK+'\n'+'demo-id='+game.id+'\n'+'fictional='+game.romFile+'\n';
     const playlist=game.romFile.endsWith('.m3u');
     let bytes=payload;
     if(playlist){
       const folder=path.posix.dirname(entry.romPath);
       const members=entry.discPaths.map(x=>{
         relative(x);
         if(!x.startsWith('ROMS/'+plat+'/')||path.posix.dirname(x)!==folder)
            throw Error('Unsafe playlist member location');
         return path.posix.basename(x);
       });
       bytes=members.join('\n')+'\n';
     }
     if(entry.romPresent)add(name+'/'+entry.romPath,bytes,playlist?'playlist':'synthetic-rom',game.id);
     if(entry.xmlEntryPresent&&!entry.romPresent)absentRomReferences++;
     for(let j=0;j<entry.discPaths.length;j++){
       const dp=entry.discPaths[j];
       relative(dp);
       if(!dp.startsWith('ROMS/'+plat+'/'))throw Error('Disc outside platform');
       if(entry.discPresent[j])add(name+'/'+dp,MARK+'\n'+'demo-id='+game.id+'\n'+'disc='+j+'\n','synthetic-disc',game.id);
     }
     if(entry.duplicatePath!==null&&entry.duplicatePath!==undefined){
       relative(entry.duplicatePath);
       if(!entry.romPresent||playlist||!entry.duplicatePath.startsWith('ROMS/'+plat+'/'))
         throw Error('Unsafe duplicate');
       add(name+'/'+entry.duplicatePath,bytes,'duplicate-candidate',game.id);
       duplicateFiles++;
     }
     const mediaRefs={};
     for(const kind of Object.keys(mediaDimensions)){
       const m=entry.media[kind];
       if(!m||typeof m.relativePath!=='string'||!relative(m.relativePath)||
           m.relativePath!==game.media[kind]||typeof m.exists!=='boolean')
         throw Error('Media contract mismatch '+game.id+' '+kind);
       mediaRefs[kind]={path:m.relativePath,exists:m.exists};
       if(m.exists){
         const [w,h]=mediaDimensions[kind];
         add(name+'/'+m.relativePath,art(game,kind,w,h),'media-'+kind,game.id);
       }
     }
     if(entry.xmlEntryPresent){
       if(name==='MYSTERY')throw Error('MYSTERY must not materialize XML');
       const short=entry.romPath.slice(('ROMS/'+plat+'/').length);
       const text=['  <game>','    <path>./'+xml(short)+'</path>',
         '    <name>'+xml(game.title)+'</name>',
         '    <desc>'+xml(game.description)+'</desc>',
         '    <developer>'+xml(game.publisher)+'</developer>',
         '    <genre>'+xml(game.genre)+'</genre>',
         '    <releasedate>'+game.year+'0101T000000</releasedate>'];
       for(const [kind,tag] of Object.entries({image:'image',thumbnail:'thumbnail',marquee:'marquee'})){
         const rel=path.posix.relative('MEDIAS/'+plat+'/metadata',entry.media[kind].relativePath);
         if(rel.startsWith('../../')||path.posix.isAbsolute(rel))throw Error('Media escaped platform');
         text.push('    <'+tag+'>'+xml(rel)+'</'+tag+'>');
       }
       text.push('  </game>');
       if(!xmlByPlatform.has(plat))xmlByPlatform.set(plat,[]);
       xmlByPlatform.get(plat).push(text.join('\n'));
     }
     records.push({id:game.id,title:game.title,platform:plat,region:game.region,edition:game.edition,
       romPath:entry.romPath,romExists:entry.romPresent,discs:entry.discPaths,
       xmlEntryPresent:entry.xmlEntryPresent,duplicatePath:entry.duplicatePath||null,media:mediaRefs,
       tags:entry.caseTags||[]});
   }
   for(const xmlPath of variant.xmlPaths){
      relative(xmlPath);
      const match=/^MEDIAS\/([a-z0-9_-]+)\/metadata\/es-de-gamelist\.xml$/.exec(xmlPath);
      if(!match||!platforms.has(match[1]))throw Error('Unsafe ES-DE destination');
      const entries=xmlByPlatform.get(match[1])||[];
      const body='<?xml version="1.0" encoding="UTF-8"?>\n<gameList>\n'+
        entries.join('\n')+'\n</gameList>\n';
      add(name+'/'+xmlPath,body,'esde-xml');
   }
   const entries=Array.from(plan.values()).filter(x=>x.path.startsWith(name+'/'))
     .sort((a,b)=>a.path.localeCompare(b.path,'en'))
     .map(x=>({path:x.path.slice(name.length+1),kind:x.kind,gameId:x.id,
       byteLength:x.bytes.length,sha256:sha(x.bytes)}));
   const roms=entries.filter(x=>x.path.startsWith('ROMS/')).length;
   const media=entries.filter(x=>x.kind.startsWith('media-')).length;
   const xmlCount=records.filter(x=>x.xmlEntryPresent).length;
   const oracle=oracles.counts[name];
   if(!oracle||roms!==oracle.physicalRomFiles||media!==oracle.pngFiles||
      xmlCount!==oracle.esDeGameEntries||
      absentRomReferences!==oracle.missingMainRomReferences||
      duplicateFiles!==oracle.duplicateCandidateFiles ||
      roms!==variant.counts.physicalRomFiles||media!==variant.counts.pngFiles||
      xmlCount!==variant.counts.esDeGameEntries)
      throw Error('Generated plan disagrees with DEV2 oracle for '+name);
   const expected={
      catalogueGameCount:30,esdeGameEntries:xmlCount,xmlImportSupported:name!=='MYSTERY',
      missingRomEntries:absentRomReferences,missingMediaFiles:180-media,
      duplicateCandidateFiles:duplicateFiles,observedFileCount:roms,
      availableGameRomCount:records.filter(x=>x.romExists).length,
      mediaFiles:media,romsPhysicalFiles:roms,confirmedLogicalGames:null};
   const manifest={schemaVersion:'demo-library-manifest/1',variant:name,synthetic:true,
     sourceKind:'DEMO01_SYNTHETIC_NON_EXECUTABLE',
     dev2Contract:{catalogueSha256:sha(catalogBytes),scenariosSha256:sha(scenariosBytes),
       oraclesSha256:sha(oraclesBytes),referenceHead:'968d1f61420a5d0b70eb5160e9d57d505560f2b7'},
     expected,games:records,files:entries};
   const manifestBytes=Buffer.from(JSON.stringify(manifest,null,2)+'\n');
   add(name+'/manifest.json',manifestBytes,'manifest');
   outVariants.push({name,manifestSha256:sha(manifestBytes),...expected});
 }
 if(seenVariants.size!==3)throw Error('Missing variant');
 fs.mkdirSync(output,{recursive:false});
 for(const item of plan.values()){
   const dest=path.join(output,...item.path.split('/'));
   fs.mkdirSync(path.dirname(dest),{recursive:true});
   fs.writeFileSync(dest,item.bytes,{flag:'wx'});
 }
 fs.writeFileSync(path.join(output,'manifest.json'),JSON.stringify({
   schemaVersion:'demo-library-root/1',synthetic:true,catalogueSha256:sha(catalogBytes),
   scenariosSha256:sha(scenariosBytes),oraclesSha256:sha(oraclesBytes),variants:outVariants
 },null,2)+'\n',{flag:'wx'});
 return {root:output,variants:outVariants};
}
function verify(root){
if(!path.isAbsolute(root)||!fs.existsSync(root)||fs.lstatSync(root).isSymbolicLink())throw Error('Absolute generated root required');
const doc=JSON.parse(fs.readFileSync(path.join(root,'manifest.json'),'utf8'));
if(doc.schemaVersion!=='demo-library-root/1'||!Array.isArray(doc.variants)||doc.variants.length!==3)throw Error('Invalid root manifest');
let count=0;
for(const info of doc.variants){
if(!['IDEAL','CHAOS','MYSTERY'].includes(info.name))throw Error('Unknown variant');
const text=fs.readFileSync(path.join(root,info.name,'manifest.json'));if(sha(text)!==info.manifestSha256)throw Error('Manifest digest mismatch');
const variant=JSON.parse(text.toString('utf8'));if(variant.games.length!==30||variant.variant!==info.name)throw Error('Invalid game manifest');
for(const item of variant.files){
relative(item.path);
const filename=path.join(root,info.name,...item.path.split('/'));
if(fs.lstatSync(filename).isSymbolicLink())throw Error('Linked fixture refused');
const b=fs.readFileSync(filename);
if(b.length!==item.byteLength||sha(b)!==item.sha256)throw Error('Fixture digest mismatch');
if(item.kind.startsWith('media-')&&!b.subarray(0,8).equals(SIGN))throw Error('PNG required');
if(item.kind==='synthetic-rom'&&!b.toString('utf8').startsWith(MARK))throw Error('ROM placeholder required');
count++;
}
}
return {root,variants:3,verifiedFiles:count};
}
function cli(args){
let out=null,cat=catalogDefault,check=null;
for(let i=0;i<args.length;i++){const flag=args[i],val=args[++i];
if(!val||!['--output','--catalog','--verify'].includes(flag))throw Error('Usage: node generate.cjs --output ABSOLUTE_NEW_DIR [--catalog JSON] | --verify ABSOLUTE_DIR');
if(flag==='--output')out=val;if(flag==='--catalog')cat=val;if(flag==='--verify')check=val;
}
if(Boolean(out)===Boolean(check))throw Error('Specify exactly --output or --verify');
const result=check?verify(path.resolve(check)):generate(out,path.resolve(cat));
process.stdout.write('DEMO01_'+(check?'VERIFY':'GENERATE')+'_PASS '+JSON.stringify(result)+'\n');
}
if(require.main===module){try{cli(process.argv.slice(2));}catch(e){process.stderr.write('DEMO01_ERROR '+e.message+'\n');process.exitCode=1;}}
module.exports={generate,verify,MARK};
})();
