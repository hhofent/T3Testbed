// Run in the real control page console or preview_evaluate, with a fresh seed.
// Resolves after actual HTTP polling, DOM installation and SVG image decoding.
async function benchmarkWorld(seed) {
 const ready=async()=>{const r=revision,t=performance.now();while(settledRevision!==r){await new Promise(r=>setTimeout(r,20));if(performance.now()-t>12000)throw Error('Visible chunks timed out')}await Promise.all([...document.querySelectorAll('#tiles img[src]')].map(i=>i.decode()))};
 clearTimeout(timer);document.getElementById('seed').value=seed;viewport=[0,0,10*mile,10*mile];
 let t=performance.now();queue();await ready();const cold10miMs=performance.now()-t;
 pan(1,0);await ready();pan(-1,0);await ready();t=performance.now();pan(1,0);await Promise.all([...document.querySelectorAll('#tiles img[src]')].map(i=>i.decode()));const cachedPanMs=performance.now()-t;await ready();
 viewport=[0,0,20*mile,20*mile];queue();await ready();viewport=[5*mile,5*mile,10*mile,10*mile];queue();await ready();t=performance.now();zoom(-1);await Promise.all([...document.querySelectorAll('#tiles img[src]')].map(i=>i.decode()));const cachedZoom20miMs=performance.now()-t;await ready();
 return {seed,cold10miMs,cachedPanMs,cachedZoom20miMs};
}
