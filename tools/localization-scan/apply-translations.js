#!/usr/bin/env node
// Belirli m_Id'lerin m_Localized degerini bir StringTable_*.asset dosyasinda gunceller.
// Kullanim: node apply-translations.js <asset-dosyasi> <translations.json>
// translations.json: { "<m_Id>": "yeni deger", ... }  (bos string "" olabilir)

const fs = require('fs');

const filePath = process.argv[2];
const jsonPath = process.argv[3];
if (!filePath || !jsonPath) {
  console.error('Kullanim: node apply-translations.js <asset-dosyasi> <translations.json>');
  process.exit(1);
}

const translations = JSON.parse(fs.readFileSync(jsonPath, 'utf8'));
const lines = fs.readFileSync(filePath, 'utf8').split(/\r?\n/);

function yamlValue(text) {
  if (text === '' || text === null || text === undefined) return '    m_Localized: ';
  const escaped = String(text)
    .replace(/\\/g, '\\\\')
    .replace(/"/g, '\\"')
    .replace(/\n/g, '\\n');
  return `    m_Localized: "${escaped}"`;
}

let applied = 0;
let notFound = [];

for (const [id, value] of Object.entries(translations)) {
  const idLine = `  - m_Id: ${id}`;
  const idx = lines.findIndex(l => l === idLine);
  if (idx === -1) {
    notFound.push(id);
    continue;
  }
  // Deger idx+1'de baslar, "    m_Metadata:" satirina kadar surer
  let end = idx + 1;
  while (end < lines.length && lines[end].trim() !== 'm_Metadata:') {
    end++;
  }
  if (end >= lines.length) {
    notFound.push(id + ' (m_Metadata bulunamadi)');
    continue;
  }
  // idx+1..end-1 arasi eski m_Localized satirlari; tek satirla degistir
  lines.splice(idx + 1, end - (idx + 1), yamlValue(value));
  applied++;
}

fs.writeFileSync(filePath, lines.join('\n'));
console.log(`${filePath}: ${applied} uygulandi, ${notFound.length} bulunamadi`);
if (notFound.length) console.log('Bulunamayan:', notFound.join(', '));
