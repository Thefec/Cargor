---
name: env-no-python
description: Bu makinede python3/python/py yok — sayısal hesaplar için Node.js kullan
metadata:
  type: project
---

Bu Windows makinesinde (Git Bash ortamı) `python3`, `python`, `py` komutlarının hiçbiri PATH'te bulunamadı (2026-07-07 tarihinde test edildi). Ancak `node` (v24.15.0) kurulu ve çalışıyor.

**Why:** CLAUDE.md/sistem talimatı hesaplamaları Python ile yapmamı söylüyor, ama bu ortamda Python interpreter'ı yok.

**How to apply:** Bu projede ekonomi hesaplamaları için Python yerine **Node.js** kullan (`node -e "..."` veya scratchpad'e `.js` dosyası yazıp `node dosya.js` ile çalıştır). Prensip aynı: hesabı asla kafadan yapma, bir interpreter'a yaptır. Eğer ileride python kurulursa bu notu güncelle/sil.
