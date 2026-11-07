const {chromium}=require('C:/Users/Edgar.DESKTOP-BOONDKL/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const {pathToFileURL}=require('url');const fs=require('fs');const path=require('path');
(async()=>{
 const root=__dirname;const browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
 try{const page=await browser.newPage({viewport:{width:1440,height:1000}});let errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(pathToFileURL(path.join(root,'comparacion-rayado.html')).href);
 await page.locator('img').evaluateAll(async images=>{await Promise.all(images.map(i=>i.decode()));});
 const images=await page.locator('img').evaluateAll(a=>a.map(i=>({width:i.naturalWidth,height:i.naturalHeight,inline:i.src.startsWith('data:image/png;base64,')})));
 if(images.length!==2||images.some(i=>i.width!==1280||i.height!==720||!i.inline)||errors.length)throw Error('Gallery validation failed');
 await page.screenshot({path:path.join(root,'rayado-page-verified.png')});fs.writeFileSync(path.join(root,'hatch-browser-checks.json'),JSON.stringify({images,errors,passed:true},null,2));
 console.log('Gallery verified: two embedded frames, no page errors');
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exit(1);});
