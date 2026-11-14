// Run in the real control page console or preview_evaluate, with a fresh seed.
// Resolves after actual HTTP polling, DOM installation and SVG image decoding.
async function benchmarkWorld(seed) {
 const ready=async()=>{const r=revision,t=performance.now();while(settledRevision!==r){await new Promise(r=>setTimeout(r,20));if(performance.now()-t>12000)throw Error('Visible chunks timed out')}await Promise.all([...document.querySelectorAll('#tiles img[src]')].map(i=>i.decode()))};
 clearTimeout(timer);document.getElementById('seed').value=seed;viewport=[0,0,10*mile,10*mile];
 let t=performance.now();queue();await ready();const cold10miMs=performance.now()-t;
 pan(1,0);await ready();pan(-1,0);await ready();t=performance.now();pan(1,0);await Promise.all([...document.querySelectorAll('#tiles img[src]')].map(i=>i.decode()));const cachedPanMs=performance.now()-t;await ready();
 viewport=[0,0,20*mile,20*mile];t=performance.now();queue();await ready();const firstZoom20miMs=performance.now()-t;viewport=[5*mile,5*mile,10*mile,10*mile];queue();await ready();t=performance.now();zoom(-1);await Promise.all([...document.querySelectorAll('#tiles img[src]')].map(i=>i.decode()));const cachedZoom20miMs=performance.now()-t;await ready();
 return {seed,cold10miMs,cachedPanMs,firstZoom20miMs,cachedZoom20miMs};
}

// Regression for the cache state left by a large overview or a long pan session.
// Use the actual controls, HTTP /chunks, LOD switch and image decoding.
async function regressionZoomPanCache() {
 const ready=async()=>{const r=revision,start=performance.now();while(settledRevision!==r){if(performance.now()-start>30000)throw Error('Timed out: '+$('status').textContent);await new Promise(resolve=>setTimeout(resolve,30))}const imgs=[...$('tiles').children];if(imgs.some(i=>i.style.visibility!=='visible'||!i.src))throw Error('Ready view has hidden chunks');await Promise.all(imgs.map(i=>i.decode()))};
 viewport=[0,0,1.8*mile,1.8*mile];queue();await ready();
 $('zoomOut').click();await ready();$('east').click();await ready();
 // Preserve the old visible detail chunk as the oldest cache entry. Entries
 // beyond 256 reproduce what remains after viewing a large overview.
 const key=viewSignature+':detail:0:0',old=chunkImages.get(key);
 if(!old)throw Error('Missing regression precondition');
 const rest=[...chunkImages].filter(([k])=>k!==key);chunkImages.clear();chunkImages.set(key,old);
 for(const [k,v] of rest)chunkImages.set(k,v);
 for(let i=0;i<300;i++)chunkImages.set('regression-offscreen:'+i,'unused');
 $('zoomIn').click();await ready();
 if(!chunkImages.has(key))throw Error('Evicted visible known detail chunk');
 // A genuinely new area must also generate, across negative chunk boundaries.
 viewport=[-13*mile,7*mile,1.8*mile,1.8*mile];queue();await ready();
 $('zoomOut').click();await ready();$('east').click();await ready();$('zoomIn').click();await ready();
 return {pass:true,level:lod(),images:$('tiles').children.length,cache:chunkImages.size};
}

// Full browser acceptance, including decoded pixels (a decoded SVG can be blank).
async function regressionDistantChunks(){
 const results=[];
 for(const seed of [14,2113558218])for(const [x,y] of [[60,0],[-60,0],[0,60],[0,-60],[198,198]]){
  $('seed').value=seed;viewport=[x*mile,y*mile,3*mile,3*mile];queue();
  const r=revision,start=performance.now();
  while(settledRevision!==r){if(performance.now()-start>120000)throw Error($('status').textContent);await new Promise(resolve=>setTimeout(resolve,50))}
  for(const img of $('tiles').children){
   if(!img.src||img.style.visibility!=='visible')throw Error('Missing distant chunk');await img.decode();
   const c=document.createElement('canvas');c.width=c.height=128;const ctx=c.getContext('2d');ctx.drawImage(img,0,0,128,128);
   const d=ctx.getImageData(0,0,128,128).data;
   if(!d.some((v,i)=>i%4===3&&v))throw Error('Transparent distant SVG');
  }
  results.push({seed,x,y,images:$('tiles').children.length});
 }
 return results;
}

// Measure decoded stroke coverage, including subpixel antialiasing.
async function regressionStrokePixels(){
 const results=[];
 for(const pixels of [24,80,256])for(const [kind,minimum] of [["river",1.5],["rail",1.2],["road",1.2]]){
  const span=3*mile,svg=`<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${span} ${span}"><g><title>${kind}:0:0</title><polyline points="${span/2},0 ${span/2},${span}" fill="${kind==='road'?'#525954':'none'}" stroke="#509bc6" stroke-width="100"/></g></svg>`;
  const img=new Image();img.src=rasterChunk('data:image/svg+xml,'+encodeURIComponent(svg),pixels,span);await img.decode();
  const canvas=document.createElement('canvas');canvas.width=canvas.height=pixels;const ctx=canvas.getContext('2d');ctx.drawImage(img,0,0);
  const row=ctx.getImageData(0,Math.floor(pixels/2),pixels,1).data;let width=0;for(let i=3;i<row.length;i+=4)width+=row[i]/255;
  if(Math.abs(width-minimum)>.15)throw Error(kind+' pixel width: '+width);
  results.push({kind,pixels,width});
 }
 return results;
}
