# 📝 Değişiklik Günlüğü — 2026-07 (arşiv)

> Bitmiş işlerin ve kararların kronolojik kaydı. Canlı plan için [../../PLAN.md](../../PLAN.md).

- **2026-07-06**: İlk yol haritası oluşturuldu (kod taraması + GDD karşılaştırması). → [../roadmap.md](../roadmap.md)
- **2026-07-06**: **Q1** (sprint sırası: stabilizasyon→doğrulama→belge→cila) ve **Q2** (kod organizasyon ikiliğine şimdilik dokunulmayacak, NewCss vs Scripts birleştirme ertelendi) onaylandı.
- **2026-07-07**: Ekonomi denge sprint'i başladı. Faz 1 (7 temel değer) uygulandı — kod + sahne/prefab override'ları. Faz 2 fiyat raporu hazırlandı. qa doğrulama + bug analizi. Bug'lar düzeltildi (BoxFallPenalty ters ceza, MoreCapacity bedava açığı, para-sıfırlama guard). → [../economy-balance.md](../economy-balance.md)
- **2026-07-08**: Fable 5 **kontrol** kalite kapısı eklendi (`.claude/agents/kontrol.md` + CLAUDE.md iş akışı kuralı 4 — zorunlu final review, 3 tur, ONAY şartı). Kullanıcı gerçek upgrade envanterini getirdi (9 upgrade); Faz 2 raporunun seviye sayıları gerçekle uyuşmadı.
  - ⚙️ **Kurulum notu:** Departman agent'ları + kontrol yalnızca Claude Code **Cargor klasöründen** açıldığında yüklenir; üst `GitHub/` klasöründen açılan oturumda çözülmez.
- **2026-07-08 (2. oturum)**: economist Faz 2 v2 → kontrol DÜZELTME GEREKLİ (bütçe fizibilitesi + doğrusal fiyat modeli). Ardından kullanıcı **pivot**: fiziksel upgrade'ler kalsın, soyut statlar kaldırılıp sıfırdan dengeli **roguelite perk havuzuna** dönüşsün. Roguelite spec tamamlandı (16 perk, T1/T2/T3 tier+kilit, sabit fiyat, veri-güdümlü mimari). economist v3.2 → kontrol **ONAY** (genel toplam 9945 TL). Uygulama başladı (subagent-driven): Task 0-2 commit'li. → [../roguelite-draft.md](../roguelite-draft.md)
