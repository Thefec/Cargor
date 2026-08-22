#!/usr/bin/env node
// Bir .unity/.prefab dosyasindaki TextMeshProUGUI/TextMeshPro metinlerini bulur,
// hangilerinin LocalizeStringEvent'e (ayni GameObject uzerinde) BAGLI OLMADIGINI listeler.
// Kullanim: node tools/localization-scan/scan.js "Assets/Scenes/The Main Office.unity"

const fs = require('fs');

const filePath = process.argv[2];
if (!filePath) {
  console.error('Kullanim: node scan.js <sahne veya prefab yolu>');
  process.exit(1);
}

const text = fs.readFileSync(filePath, 'utf8');
const lines = text.split(/\r?\n/);

// Bloklara ayir: her blok "--- !u!<classId> &<anchorId>" ile baslar
const blocks = [];
let current = null;
for (const line of lines) {
  const header = line.match(/^--- !u!(\d+) &(-?\d+)/);
  if (header) {
    if (current) blocks.push(current);
    current = { classId: header[1], id: header[2], lines: [] };
  } else if (current) {
    current.lines.push(line);
  }
}
if (current) blocks.push(current);

const goNames = new Map(); // goId -> name
const tmpComponents = []; // {goId, compId, text}
const localizedGoIds = new Set();

for (const block of blocks) {
  if (block.classId === '1') {
    // GameObject
    const nameLine = block.lines.find(l => l.startsWith('  m_Name: '));
    const name = nameLine ? nameLine.slice('  m_Name: '.length) : '(isimsiz)';
    goNames.set(block.id, name);
  } else if (block.classId === '114') {
    const editorClassLine = block.lines.find(l => l.startsWith('  m_EditorClassIdentifier: '));
    const editorClass = editorClassLine ? editorClassLine.slice('  m_EditorClassIdentifier: '.length) : '';
    const goLine = block.lines.find(l => l.startsWith('  m_GameObject: '));
    const goMatch = goLine && goLine.match(/fileID: (-?\d+)/);
    const goId = goMatch ? goMatch[1] : null;

    if (editorClass.includes('LocalizeStringEvent')) {
      if (goId) localizedGoIds.add(goId);
    } else if (editorClass.includes('TMPro.TextMeshProUGUI') || editorClass === 'TMPro.TextMeshPro') {
      const textLineIdx = block.lines.findIndex(l => l.startsWith('  m_text: '));
      let textVal = '';
      if (textLineIdx !== -1) {
        const firstLine = block.lines[textLineIdx].slice('  m_text: '.length);
        if (firstLine.startsWith("'") && !(firstLine.endsWith("'") && firstLine.length > 1)) {
          // coklu-satir YAML tek-tirnakli block scalar - kapanana kadar satirlari topla
          const parts = [firstLine.slice(1)];
          let i = textLineIdx + 1;
          while (i < block.lines.length) {
            const l = block.lines[i];
            if (l.endsWith("'")) { parts.push(l.slice(0, -1)); break; }
            parts.push(l);
            i++;
          }
          textVal = parts.join('\n').replace(/''/g, "'");
        } else {
          textVal = firstLine;
        }
      }
      tmpComponents.push({ goId, compId: block.id, text: textVal });
    }
  }
}

const missing = tmpComponents.filter(c => !localizedGoIds.has(c.goId));

function isNoise(t) {
  if (t === '' || t === "''") return true; // bos
  if (/^​*$/.test(t)) return true; // zero-width space
  if (/^-?\d+([.,]\d+)?%?$/.test(t)) return true; // saf sayi / yuzde (runtime deger)
  if (t.length <= 1) return true; // tek karakter (ikon glif olasi)
  return false;
}

console.log(`Dosya: ${filePath}`);
console.log(`Toplam TMP metni: ${tmpComponents.length} | Localization'a bagli: ${tmpComponents.length - missing.length} | BAGLI DEGIL: ${missing.length}`);
console.log('---');
function decode(t) {
  if (t.startsWith('"') && t.endsWith('"')) {
    try { return JSON.parse(t); } catch (e) { /* fallthrough */ }
  }
  return t;
}

for (const m of missing) {
  const goName = goNames.get(m.goId) || '(bilinmiyor)';
  const decoded = decode(m.text).trim();
  if (isNoise(decoded)) continue;
  const preview = decoded.replace(/\n/g, '\\n');
  console.log(`[GO:${m.goId}] ${goName}  ->  "${preview}"`);
}
