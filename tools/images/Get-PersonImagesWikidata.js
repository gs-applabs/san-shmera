// Second source of portraits for the people of data\births and data\deaths that have no picture yet (the first source, tools\wikipedia\Get-PersonImages.ps1,
// uses the page image of the Greek Wikipedia article, which is often a non-free picture). Here the picture is the "image" (P18) of the Wikidata item of the person,
// which always lives on Wikimedia Commons. A person is accepted only when the Wikidata item is a human whose birth (or death) year equals the year in our data.
// Licence is read from Commons: public domain, CC0 or CC BY only (never share-alike); author, licence and page are written next to the picture.
// Output: data\images\photos\people\*.jpg and data\images\personimages-wd.tsv  (MM-DD|b or d|year|name|file|caption|creator|license|sourceUrl)
// Honest client name, one request every 2 seconds, longer wait after a 429. Can be stopped and run again.
// Usage: node Get-PersonImagesWikidata.js [maxPeople]
const fs = require('fs'), path = require('path'), crypto = require('crypto');
const root = path.resolve(__dirname, '..', '..');
const UA = 'Chronikologio-DataTool/0.1 (https://github.com/giorstergiopoulos-design/san-shmera; personal non-commercial tool)';
const imgDir = path.join(root, 'data', 'images', 'photos', 'people');
const outFile = path.join(root, 'data', 'images', 'personimages-wd.tsv');
fs.mkdirSync(imgDir, { recursive: true });
const sleep = ms => new Promise(r => setTimeout(r, ms));
const fold = s => s.normalize('NFD').replace(/\p{Mn}/gu, '').toLowerCase().replace(/ς/g, 'σ').replace(/[^\p{L}\p{Nd}]+/gu, ' ').trim();
const LIC = { 'public domain': 'Public Domain', 'cc0': 'CC0 1.0', 'cc by 4.0': 'CC BY 4.0', 'cc by 3.0': 'CC BY 3.0', 'cc by 2.5': 'CC BY 2.5', 'cc by 2.0': 'CC BY 2.0' };
async function api(url, binary) {
  for (let t = 1; t <= 5; t++) {
    const r = await fetch(url, { headers: { 'User-Agent': UA } });
    await sleep(2000);
    if (r.status === 429) { await sleep(60000 * t); continue; }
    if (!r.ok) throw new Error(r.status + ' ' + url.slice(0, 80));
    return binary ? Buffer.from(await r.arrayBuffer()) : r.json();
  }
  throw new Error('429 too often');
}
const strip = s => (s || '').replace(/<[^>]+>/g, '').replace(/&amp;/g, '&').replace(/&quot;/g, '"').replace(/&#0?39;/g, "'").replace(/\s+/g, ' ').trim();

// the people
const people = [];
for (const [dir, files, kind] of [['births', ['births.tsv', 'births-wiki.tsv', 'births-cy.tsv'], 'b'], ['deaths', ['deaths-wiki.tsv'], 'd']]) {
  for (const f of files) {
    const p = path.join(root, 'data', dir, f); if (!fs.existsSync(p)) continue;
    for (const l of fs.readFileSync(p, 'utf8').replace(/^﻿/, '').split(/\r?\n/)) {
      const m = /^(\d\d-\d\d)\|(-?\d+)\|([^|]*)\|([^|]+)\|/.exec(l); if (!m) continue;
      people.push({ day: m[1], year: +m[2], name: m[4].trim(), kind });
    }
  }
}
const key = p => `${p.kind}|${p.day}|${p.year}|${fold(p.name)}`;
const done = new Set();
const lines = [];
for (const f of ['personimages-wiki.tsv', 'personimages-wd.tsv']) {
  const p = path.join(root, 'data', 'images', f); if (!fs.existsSync(p)) continue;
  for (const l of fs.readFileSync(p, 'utf8').replace(/^﻿/, '').split(/\r?\n/)) {
    const m = /^(\d\d-\d\d)\|([bd])\|(-?\d+)\|([^|]+)\|/.exec(l); if (m) done.add(`${m[2]}|${m[1]}|${+m[3]}|${fold(m[4])}`);
    if (f === 'personimages-wd.tsv' && l) lines.push(l);
  }
}
if (!lines.length) lines.push('# MM-DD|b (birth) or d (death)|year|name|file|caption|creator|license|sourceUrl   (portraits from Wikimedia Commons through Wikidata, free licences only; made by tools\\images\\Get-PersonImagesWikidata.js)');
const tried = new Set(); const triedFile = path.join(root, 'tools', 'images', 'events', 'wd-tried.txt');
if (fs.existsSync(triedFile)) for (const l of fs.readFileSync(triedFile, 'utf8').split(/\r?\n/)) if (l) tried.add(l);
const todo = people.filter(p => !done.has(key(p)) && !tried.has(key(p))).slice(0, +process.argv[2] || 100000);
console.log('people', people.length, 'to do', todo.length);
const seenFile = new Set();
let added = 0;
const yearOf = c => { const v = c?.mainsnak?.datavalue?.value?.time; if (!v) return null; const m = /^([+-])0*(\d+)-/.exec(v); return m ? (m[1] === '-' ? -+m[2] : +m[2]) : null; };

(async () => {
  for (let i = 0; i < todo.length; i += 40) {
    const chunk = todo.slice(i, i + 40);
    try {
      const titles = [...new Set(chunk.map(p => p.name))];
      const r = await api('https://el.wikipedia.org/w/api.php?action=query&format=json&formatversion=2&redirects=1&prop=pageprops&ppprop=wikibase_item&titles=' + encodeURIComponent(titles.join('|')));
      const norm = {}, redir = {}, qid = {};
      for (const x of r.query.normalized || []) norm[x.from] = x.to;
      for (const x of r.query.redirects || []) redir[x.from] = x.to;
      for (const pg of r.query.pages || []) if (pg.pageprops?.wikibase_item) qid[pg.title] = pg.pageprops.wikibase_item;
      const ids = [...new Set(chunk.map(p => { let t = p.name; t = norm[t] || t; t = redir[t] || t; p.qid = qid[t]; return p.qid; }).filter(Boolean))];
      const ent = {};
      if (ids.length) {
        const w = await api('https://www.wikidata.org/w/api.php?action=wbgetentities&format=json&props=claims&ids=' + ids.join('|'));
        Object.assign(ent, w.entities || {});
      }
      const cands = [];
      for (const p of chunk) {
        tried.add(key(p));
        const e = ent[p.qid]; if (!e?.claims) continue;
        const human = (e.claims.P31 || []).some(c => c.mainsnak?.datavalue?.value?.id === 'Q5');
        const ys = (e.claims[p.kind === 'b' ? 'P569' : 'P570'] || []).map(yearOf);
        if (!human || !ys.includes(p.year)) continue;
        const img = e.claims.P18?.[0]?.mainsnak?.datavalue?.value; if (!img) continue;
        cands.push({ p, file: img });
      }
      if (cands.length) {
        const c = await api('https://commons.wikimedia.org/w/api.php?action=query&format=json&formatversion=2&prop=imageinfo&iiprop=url|size|mime|extmetadata&iiurlwidth=900&iiextmetadatafilter=LicenseShortName|Artist&titles=' + encodeURIComponent(cands.map(x => 'File:' + x.file).join('|')));
        const info = {};
        for (const pg of c.query.pages || []) if (pg.imageinfo) info[pg.title.replace(/^File:/, '').replace(/_/g, ' ')] = pg.imageinfo[0];
        for (const { p, file } of cands) {
          const ii = info[file.replace(/_/g, ' ')]; if (!ii) continue;
          const lic = LIC[String(ii.extmetadata?.LicenseShortName?.value || '').toLowerCase()];
          if (!lic) continue;
          if (!['image/jpeg', 'image/png'].includes(ii.mime) || Math.max(ii.width, ii.height) < 300) continue;
          if (/\b(map|flag|logo|coat of arms|signature|stamp)\b|\.svg$/i.test(file) || seenFile.has(file)) continue;
          const url = ii.width > 900 && ii.thumburl ? ii.thumburl : ii.url;
          const name = `${p.day}-${p.kind}-${p.year}-${crypto.randomBytes(3).toString('hex')}${ii.mime === 'image/png' ? '.png' : '.jpg'}`;
          try { fs.writeFileSync(path.join(imgDir, name), await api(url, true)); } catch (e) { console.log('download failed', name, e.message); continue; }
          seenFile.add(file);
          const creator = strip(ii.extmetadata?.Artist?.value) || 'Unknown author';
          lines.push([p.day, p.kind, p.year, p.name, 'photos/people/' + name, p.name.replace(/\|/g, '/'), creator.replace(/\|/g, '/'), lic, ii.descriptionurl].join('|'));
          added++;
        }
      }
    } catch (e) { console.log('chunk failed:', e.message); }
    fs.writeFileSync(outFile, '﻿' + lines.join('\n') + '\n');
    fs.mkdirSync(path.dirname(triedFile), { recursive: true });
    fs.writeFileSync(triedFile, [...tried].join('\n') + '\n');
    console.log(Math.min(i + 40, todo.length), 'of', todo.length, 'looked at,', added, 'portraits');
  }
})();
