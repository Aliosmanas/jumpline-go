# JUMPLINE GO — Google Play mağaza sayfası taslağı (aşama 17)

> Taslak. Yayın öncesi Tolga gözden geçirir; fiyat/reklam bilgisi aşama 16 kararlarına göre güncellenir.
> Görseller: `docs/store/feature_graphic.png` (1024×500), `docs/store/screenshots/*.png` (1080×1920),
> ikon `Assets/Art/UI/AppIcon.png` (1024; Play 512×512 ister — dışa aktarırken küçült).
> Yeniden üretmek: `python Tools/make_store_assets.py`

## Türkçe

**Uygulama adı (≤30):** JUMPLINE GO: Efsaneler Kitabı

**Kısa açıklama (≤80):** Efsaneler kitaptan kaçtı! Kaydır, zıpla, fırlat — 8 şehirde 8 canavarı kovala.

**Tam açıklama:**

Diyarbakır Sur'unda tozlu bir kitapçı rafında kimsenin açmaya cesaret edemediği bir kitap duruyor: **Efsaneler Kitabı**. Sen açtın — ve içindeki canavarlar sokaklara kaçtı!

Koş, kaydır, zıpla ve eline geçen karpuzu, testiyi efsanelerin sırtına fırlat. Her isabet bir kalp kırar; son kalbi kırınca efsane kitabına geri döner.

**8 ŞEHİR · 8 EFSANE**
• Diyarbakır — Tepegöz
• Mardin — Şahmeran
• Hasankeyf — Humbaba
• Şanlıurfa — Taş Bekçi
• Van — Van Gölü Canavarı
• Kars — Karakoncolos
• Kayıp şehir — Lamassu
• Son sayfa — Ejderha

**NASIL OYNANIR**
• Yukarı kaydır: zıpla · Aşağı kaydır: kay / havadan hızlı in · Sağa-sola: şerit değiştir
• Fırlatılabilir nesneleri topla, AT düğmesiyle canavarı vur
• Her canavarın kendi huyu var: kimi kıvrak, kimi taş gibi sert, kimi çok atar

**DAHA FAZLASI**
• Hızlı Yarış (2x altın) ve Günlük Görev
• Güçler: mıknatıs, kalkan, çift altın, hız — Geliştir ekranında süreleri uzat
• Günlük görevler, başarımlar, oyuncu seviyesi, 7 günlük giriş ödülü
• Kostümler, Türkçe ve İngilizce, solak modu, kontrastlı mod
• İnternetsiz oynanır; ilerlemen yalnızca cihazında saklanır

**Kategori:** Oyun › Arcade (alternatif: Aksiyon)
**Etiketler:** sonsuz koşu, koşu oyunu, canavar, efsane, macera
**İçerik derecelendirmesi (öneri):** PEGI 7 / Herkes 10+ — çizgi film tarzı, kan yok, şiddet hafif (karpuz fırlatma)

## English

**App name (≤30):** JUMPLINE GO: Book of Legends

**Short description (≤80):** The legends escaped the book! Swipe, jump, throw — chase 8 monsters in 8 cities.

**Full description:**

In an old bookshop inside the ancient walls of Diyarbakır lies a book nobody dares to open: **The Book of Legends**. You opened it — and its monsters ran into the streets!

Run, swipe, jump and throw melons and jugs at the legends' backs. Every hit breaks a heart; break the last one and the legend returns to its page.

**8 CITIES · 8 LEGENDS** — Tepegöz, Shahmaran, Humbaba, the Stone Guardian, the Van Lake Monster, Karakoncolos, Lamassu and the Dragon.

**HOW TO PLAY**
• Swipe up to jump · swipe down to slide or drop fast · swipe left/right to change lanes
• Grab throwables and tap THROW to hit the monster
• Every legend has its own temper: some weave between lanes, some never dodge, some throw a lot

**MORE**
• Quick Race (2x gold) and Daily Challenge
• Power-ups: magnet, shield, double gold, speed — upgrade their duration
• Daily tasks, achievements, player level, 7-day login rewards
• Costumes, Turkish & English, left-handed mode, high-contrast mode
• Plays offline; your progress is stored only on your device

## Kontrol listesi (yayın öncesi — Tolga)
- [ ] Google Play geliştirici hesabı (25 $, tek sefer) — Tolga açar
- [ ] Keystore oluştur ve sakla (Claude girmez; kaybolursa güncelleme yayınlanamaz)
- [ ] Gizlilik politikasını bir web adresinde yayınla (`docs/store/privacy_policy.md` → GitHub Pages olabilir)
- [ ] Hedef yaş kararı (öneri 13+; çocuklar dahilse reklam kuralları sıkılaşır — aşama 16)
- [ ] Veri güvenliği formu: veri toplanmıyor (reklam eklenirse güncellenir)
- [ ] İç test kanalı → kapalı test (arkadaşlar, aşama 15) → üretim
