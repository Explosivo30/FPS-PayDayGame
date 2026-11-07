const { chromium } = require('C:/Users/Edgar.DESKTOP-BOONDKL/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
(async()=>{
 const browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
 const page=await browser.newPage({viewport:{width:1440,height:1000}});
 const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.goto('file:///D:/Unity/FPS-PayDayGame/Artifacts/EngravingCharacter/comparacion-autonoma.html',{waitUntil:'load',timeout:120000});
 const check=async label=>{await page.waitForFunction(()=>[...document.querySelectorAll('#cards img,#under,#over')].every(i=>i.complete&&i.naturalWidth>0));return {label,images:await page.locator('#cards img').evaluateAll(a=>a.map(i=>({width:i.naturalWidth,height:i.naturalHeight})))}};
 const results=[await check('initial')];await page.selectOption('#view','detail');results.push(await check('detail'));
 await page.locator('#orbit').fill('12');await page.locator('#orbit').dispatchEvent('input');results.push(await check('orbit12'));
 await page.selectOption('#view','view-155');await check('reset');await page.screenshot({path:'D:/Unity/FPS-PayDayGame/Artifacts/EngravingCharacter/page-verified.png'});
 await browser.close();console.log(JSON.stringify({results,errors}));if(errors.length)process.exitCode=1;
})();
