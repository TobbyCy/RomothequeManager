#!/usr/bin/env node
(function demo(){
'use strict';
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),zlib=require('node:zlib');
const rootRepo=path.resolve(__dirname,'../..');
const catalogDefault=path.join(__dirname,'catalogue.demo01.candidate.json');
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
function readCatalog(file){
const source=fs.readFileSync(file);if(source.length>1048576)throw Error('Catalogue too large');
const data=JSON.parse(source.toString('utf8'));if(data.schemaVersion!=='demo-library-01/1'||!Array.isArray(data.games)||data.games.length!==30)throw Error('Exactly 30 canonical games required');
const ids=new Set(),files=new Set();
for(const g of data.games){
if(!/^demo01-[0-9]{3}$/.test(g.id)||ids.has(g.id))throw Error('Invalid/duplicate ID');ids.add(g.id);
if(!/^[a-z0-9_-]{2,20}$/.test(g.platform))throw Error('Invalid platform');
for(const field of ['title','description','genre','publisher','region','edition'])if(typeof g[field]!=='string'||!g[field].trim()||g[field].length>1200)throw Error('Invalid '+field);
if(!Number.isInteger(g.year)||g.year<1980||g.year>2099)throw Error('Invalid year');
relative(g.romFile);if(g.romFile.includes('/')||!/\.(gb|gba|nes|sfc|bin|z64|chd|m3u)$/i.test(g.romFile))throw Error('Invalid ROM extension');
if(files.has(g.platform+'/'+g.romFile))throw Error('Duplicate ROM path');files.add(g.platform+'/'+g.romFile);
if(g.discs!==undefined){if(!g.romFile.endsWith('.m3u')||!Array.isArray(g.discs)||g.discs.length<2||g.discs.length>4)throw Error('Invalid discs');for(const d of g.discs){relative(d);if(d.includes('/')||!d.endsWith('.chd'))throw Error('Invalid disc name');}}
}
return {data,source};
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
const output=safeOutput(target),{data,source}=readCatalog(catalog),plan=new Map(),summaries=[];
function add(p,content,kind,id=null){relative(p);const key=p.toLowerCase();if(plan.has(key))throw Error('Case-insensitive collision '+p);plan.set(key,{path:p,bytes:Buffer.isBuffer(content)?content:Buffer.from(content,'utf8'),kind,id});}
for(const variant of ['IDEAL','CHAOS','MYSTERY']){
const gameRecords=[],xmlByPlatform=new Map();let missingRom=0,missingMedia=0,duplicates=0;
for(let i=0;i<data.games.length;i++){
const g=data.games[i],plat=g.platform,short=g.romFile;
const variantName=variant==='IDEAL'?short:variant==='CHAOS'?'Odd Names/Edition_'+i%4+'/'+(i%3===0?'Très bizarre ':'')+short.replace('demo01-','fichier-'):'unclassified/'+(i%4===0?'À trier ':'')+short.replace('demo01-','objet-');
const romPath=variant+'/ROMS/'+plat+'/'+variantName, absent=variant==='CHAOS'&&(i===4||i===16);
const payload=MARK+'\n'+'game-id='+g.id+'\n'+'fictional='+short+'\n';
if(!absent)add(romPath,g.discs?g.discs.join('\n')+'\n':payload,g.discs?'playlist':'synthetic-rom',g.id);else missingRom++;
const discs=[];
if(g.discs)for(const d of g.discs){
const dirname=path.posix.dirname(variantName),p=variant+'/ROMS/'+plat+'/'+(dirname==='.'?'':dirname+'/')+d;
add(p,MARK+'\n'+'disc='+d+'\n'+'game-id='+g.id+'\n','synthetic-disc',g.id);discs.push(p.slice(variant.length+1));
}
const medias={},refs={};
if(variant!=='MYSTERY'){
for(const [kind,folder,w,h] of types){
const filepath='MEDIAS/'+plat+'/'+folder+'/'+g.id+'.png',ref='../'+folder+'/'+g.id+'.png';
const unavailable=variant==='CHAOS'&&((kind==='image'&&i%7===0)||(kind==='screenshot'&&i%5===1)||(kind==='thumbnail'&&i%11===2));
if(unavailable)missingMedia++;else add(variant+'/'+filepath,art(g,kind,w,h),'media-'+kind,g.id);
medias[kind]={path:filepath,exists:!unavailable};refs[kind]=ref;
}
const entry=['  <game>',
'    <path>./'+xml(variantName)+'</path>',
'    <name>'+xml(g.title)+'</name>',
'    <desc>'+xml(g.description)+'</desc>',
'    <developer>'+xml(g.publisher)+'</developer>',
'    <genre>'+xml(g.genre)+'</genre>',
'    <releasedate>'+g.year+'0101T000000</releasedate>',
'    <image>'+xml(refs.image)+'</image>',
'    <thumbnail>'+xml(refs.thumbnail)+'</thumbnail>',
'    <marquee>'+xml(refs.marquee)+'</marquee>','  </game>'].join('\n');
if(!xmlByPlatform.has(plat))xmlByPlatform.set(plat,[]);
xmlByPlatform.get(plat).push(entry);
}
if(variant==='CHAOS'&&i%9===3&&!absent&&!g.discs){
const p=variant+'/ROMS/'+plat+'/EXTRAS/DUP_'+g.id+path.posix.extname(short);
add(p,payload,'duplicate-candidate',g.id);duplicates++;
}
gameRecords.push({id:g.id,title:g.title,platform:plat,region:g.region,edition:g.edition,
romPath:romPath.slice(variant.length+1),romExists:!absent,discs,media:medias});
}
if(variant!=='MYSTERY')for(const [plat,entries] of xmlByPlatform){
const folder=variant==='CHAOS'?'  <folder><path>./Odd Names</path><name>Dossier fictif</name></folder>\n':'';
add(variant+'/MEDIAS/'+plat+'/metadata/es-de-gamelist.xml',
'<?xml version="1.0" encoding="UTF-8"?>\n<gameList>\n'+folder+entries.join('\n')+'\n</gameList>\n','esde-xml');
}
const fileRecords=Array.from(plan.values()).filter(x=>x.path.startsWith(variant+'/'))
.sort((a,b)=>a.path.localeCompare(b.path,'en')).map(x=>({path:x.path.slice(variant.length+1),kind:x.kind,gameId:x.id,byteLength:x.bytes.length,sha256:sha(x.bytes)}));
const expected={catalogueGameCount:30,esdeGameEntries:variant==='MYSTERY'?0:30,
xmlImportSupported:variant!=='MYSTERY',missingRomEntries:missingRom,missingMediaFiles:missingMedia,
duplicateCandidateFiles:duplicates,observedFileCount:fileRecords.filter(x=>x.path.startsWith('ROMS/')).length,
availableGameRomCount:gameRecords.filter(x=>x.romExists).length};
const manifest={schemaVersion:'demo-library-manifest/1',variant,synthetic:true,
sourceKind:'DEMO01_SYNTHETIC_NON_EXECUTABLE',catalogueSha256:sha(source),expected,
games:gameRecords,files:fileRecords};
const manifestBytes=Buffer.from(JSON.stringify(manifest,null,2)+'\n');
add(variant+'/manifest.json',manifestBytes,'manifest');
summaries.push({name:variant,manifestSha256:sha(manifestBytes),...expected});
}
fs.mkdirSync(output,{recursive:false});
for(const item of plan.values()){
const filename=path.join(output,...item.path.split('/'));
fs.mkdirSync(path.dirname(filename),{recursive:true});
fs.writeFileSync(filename,item.bytes,{flag:'wx'});
}
fs.writeFileSync(path.join(output,'manifest.json'),JSON.stringify({
schemaVersion:'demo-library-root/1',synthetic:true,catalogueSha256:sha(source),variants:summaries
},null,2)+'\n',{flag:'wx'});
return {root:output,variants:summaries};
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
