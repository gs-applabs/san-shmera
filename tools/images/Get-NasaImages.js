// Finds photographs for the space events that have none yet in the NASA Image and Video Library (images.nasa.gov, https://images-api.nasa.gov).
// NASA's own images are public domain (the media usage guidelines say NASA content is generally not subject to copyright); an image whose description
// names a copyright holder is skipped. A picture is taken only when its title or description contains the keyword of the event and its date is in
// the year of the event (+-1): a wrong picture is worse than none.
// Output: data\images\photos\events\oa-nasa-*.jpg and data\images\eventimages-oa.tsv  (MM-DD|year|title|file|caption|creator|license|sourceUrl)
// Honest client name, one request every 2 seconds. Usage: node tools/images/Get-NasaImages.js
const fs = require('fs');
const path = require('path');

const UA = 'Chronikologio-DataTool/0.1 (https://github.com/giorstergiopoulos-design/san-shmera; personal non-commercial tool)';
const root = path.resolve(__dirname, '..', '..');
const imgDir = path.join(root, 'data', 'images', 'photos', 'events');
const outFile = path.join(root, 'data', 'images', 'eventimages-oa.tsv');
const sleep = ms => new Promise(r => setTimeout(r, ms));

// MM-DD|year|title of our event -> query and the keyword that must appear in the title or description of the picture
const wanted = [
  ['01-28|1986|Η καταστροφή του Τσάλεντζερ', 'Challenger STS-51-L crew', 'challenger'],
  ['02-01|2003|Η καταστροφή του Κολούμπια', 'STS-107 Columbia crew', 'columbia'],
  ['02-05|1971|Το Απόλλων 14 προσεληνώνεται', 'Apollo 14 lunar surface', 'apollo 14'],
  ['03-18|1965|Ο Αλεξέι Λεόνοφ κάνει τον πρώτο διαστημικό περίπατο', 'Leonov Voskhod 2 spacewalk', 'leonov'],
  ['04-11|1970|Εκτοξεύεται το Απόλλων 13', 'Apollo 13 launch', 'apollo 13'],
  ['04-13|1970|Η έκρηξη στον Απόλλωνα 13', 'Apollo 13 service module damage', 'apollo 13'],
  ['07-21|1969|Ο Νιλ Άρμστρονγκ πατά στη Σελήνη', 'Apollo 11 Aldrin lunar surface', 'apollo 11'],
  ['07-24|1969|Ο Απόλλων 11 επιστρέφει στη Γη', 'Apollo 11 splashdown', 'apollo 11'],
  ['07-31|1964|Το Ranger 7 στέλνει φωτογραφίες της Σελήνης', 'Ranger 7 moon photograph', 'ranger 7'],
  ['10-11|1968|Εκτοξεύεται ο Apollo 7', 'Apollo 7 launch', 'apollo 7'],
  ['11-14|1969|Εκτοξεύεται ο Απόλλων 12', 'Apollo 12 launch', 'apollo 12'],
  ['11-19|1969|Ο Apollo 12 προσσεληνώνεται', 'Apollo 12 lunar surface Conrad', 'apollo 12'],
  ['12-04|1998|Εκτοξεύεται το πρώτο τμήμα του Διεθνούς Διαστημικού Σταθμού', 'STS-88 Unity Zarya', 'sts-88'],
  ['12-14|1972|Ο Ευγένιος Σέρναν φεύγει από τη Σελήνη', 'Apollo 17 Cernan lunar surface', 'cernan'],
  ['12-24|1968|Η Γη βγαίνει πάνω από τη Σελήνη', 'Earthrise Apollo 8', 'earthrise'],
];

async function get(url, binary) {
  for (let t = 1; t <= 4; t++) {
    try {
      const r = await fetch(url, { headers: { 'User-Agent': UA } });
      if (r.status === 429) { await sleep(60000 * t); continue; }
      if (!r.ok) return null;
      return binary ? Buffer.from(await r.arrayBuffer()) : await r.json();
    } catch { await sleep(5000 * t); }
  }
  return null;
}

(async () => {
  fs.mkdirSync(imgDir, { recursive: true });
  const have = new Set(fs.existsSync(outFile) ? fs.readFileSync(outFile, 'utf8').split('\n').map(l => l.split('|').slice(0, 3).join('|')) : []);
  const lines = fs.existsSync(outFile) ? fs.readFileSync(outFile, 'utf8').replace(/^﻿/, '').split('\n').filter(Boolean) : ['# MM-DD|year|title|file|caption|creator|license|sourceUrl   (made by tools\\images\\Get-NasaImages.js; images of NASA, public domain)'];
  for (const [key, query, kw] of wanted) {
    if (have.has(key)) continue;
    const [md, year, title] = key.split('|');
    const y = +year;
    const search = await get('https://images-api.nasa.gov/search?media_type=image&year_start=' + (y - 1) + '&year_end=' + (y + 1) + '&q=' + encodeURIComponent(query));
    await sleep(2000);
    const items = search && search.collection && search.collection.items ? search.collection.items : [];
    const pick = items.find(it => {
      const d = it.data && it.data[0]; if (!d) return false;
      const text = ((d.title || '') + ' ' + (d.description || '')).toLowerCase();
      if (!text.includes(kw)) return false;
      if (/copyright|©|courtesy of (?!nasa)/i.test(d.description || '')) return false;
      return true;
    });
    if (!pick) { console.log('none  ', key); continue; }
    const d = pick.data[0];
    const manifest = await get(pick.href); await sleep(2000);
    const url = manifest && (manifest.find(u => /~medium\.jpg$/i.test(u)) || manifest.find(u => /~large\.jpg$/i.test(u)) || manifest.find(u => /~small\.jpg$/i.test(u)));
    if (!url) { console.log('nofile', key); continue; }
    const bytes = await get(url.replace('http://', 'https://'), true); await sleep(2000);
    if (!bytes || bytes.length < 5000) { console.log('nodl  ', key); continue; }
    const file = 'photos/events/oa-nasa-' + d.nasa_id.replace(/[^A-Za-z0-9_-]/g, '') + '.jpg';
    fs.writeFileSync(path.join(root, 'data', 'images', file), bytes);
    const caption = (d.title || '').replace(/[|\r\n]/g, ' ').slice(0, 160);
    const creator = ((d.photographer || d.secondary_creator || 'NASA') + '').replace(/[|\r\n]/g, ' ');
    lines.push([md, year, title, file, caption, creator, 'Public Domain', 'https://images.nasa.gov/details/' + d.nasa_id].join('|'));
    console.log('ok    ', key, '->', d.nasa_id, '|', caption.slice(0, 70));
  }
  fs.writeFileSync(outFile, '﻿' + lines.join('\n') + '\n');
})();
