# JUNPLINE — tasarım özeti (proje sahibinin brief'inden, 2 Eki 2026)

**Tür:** 3D hyper-casual / arcade / runner · **Platform:** Android · **Hedef:** Google Play'e hazır ürün.
**Öz:** Simple controls + fast gameplay + satisfying jumps + challenging obstacles + replayability.

## Oynanış
- Karakter sürekli ileri koşar. Tek kontrol dokunmatik: basılı tut = şarj, bırak = zıpla.
- Süre → normalize şarj → zıplama kuvveti (min/max şarj süresi, sonsuz kuvvet yok). Kısa = alçak, orta = normal, uzun = yüksek.
- Havada karmaşık kontrol yok. Engele/tehlikeye çarpınca ölüm, anında yeniden deneme.
- Oyuncunun tek sorusu: "Ne kadar zıplamalıyım, ne zaman bırakmalıyım?"
- Felsefe: ilk saniyede anlaşılır, kısa bölümler, hızlı retry, yavaş artan zorluk, engeller önceden görünür ve adil.

## Kamera
Third-person, karakterin arkasından; karakter ekranın alt-ortasında. Ani hareket yok, yaklaşan engeller görünür, hafif anticipation olabilir.

## Karakter
Önce placeholder, sonra Blender'da özgün stylized low-poly, renkli, okunur siluet, sevimli ama çocuksu değil.
Animasyonlar: Idle, Run, Jump preparation, Jump, Fall, Land, Death, Finish.

## Blender assetleri
Player, Ground, Platform, Wall, Spike, Gap parçaları, Moving obstacle, Rotating obstacle, Ramp, Finish gate, Coin, basit çevre.
Low-poly, mobil optimize, kontrollü texture, yeniden kullanılabilir; Unity'de prefab. Gerekirse Blender Python ile otomatik üretim.

## Engeller
Basic Wall · High Wall · Gap · Spikes · Low Ceiling (çok yüksek zıplama hep doğru değil) · Moving Obstacle · Rotating Obstacle · Ramp · Narrow Landing.
İleride adil kombinasyonlar (ör. Wall → Gap → Low Ceiling → Gap). Her engelin Inspector parametreleri olur.

## Bölümler (ilk 10)
START → OBSTACLES → FINISH.
1: tutorial · 2–3: basit duvar, küçük boşluk · 4–5: uzun boşluk, farklı güçler · 6–7: low ceiling, moving · 8–9: kombine · 10: ilk challenge.
İlerleme cihazda kaydedilir (açılan bölüm kaybolmaz).

## Sistemler
GameManager, LevelManager, PlayerController, CameraController, ObstacleManager, UIManager, AudioManager, SaveManager.
PlayerController: otomatik ileri hareket, şarj, değişken zıplama, yerçekimi, zemin/iniş/ölüm/bitiş algılama, restart.
Inspector: Run Speed, Min/Max Jump Force, Min/Max Charge Time, Gravity, Air Control.
Input: Touch Down/Up (Android), Mouse Down/Up (Editor).

## UI (Unity UI, Blender değil)
Main Menu (Play, Level Select, Settings) · Gameplay (level no, ilerleme, pause) · Pause (Continue, Restart, Main Menu) ·
Game Over (Retry, Main Menu) · Level Complete (Next, Replay, Main Menu). Sade, modern, responsive.

## Ses
Jump, Land, Death, Button, Level complete, Coin, UI, müzik. AudioManager; Settings'te Music + SFX volume.

## Görsel
Clean + Colorful + Low-poly + Modern Mobile Arcade. Aşırı partikül/bloom/post-process/ağır shader/texture yok.
Şarj geri bildirimi: squash/stretch, ölçek, gösterge, hafif partikül, UI — oynanışı engellemeden. Ölüm kısa, retry anında.

## Performans
Gerekirse object pooling, az Instantiate/Destroy, az draw call, düşük poligon, optimize texture, realtime ışık az, mobil shader, az GC.

## Süreç (fazlar)
1 Foundation · 2 Playable prototype (player, ground, camera, run, input, charge/release jump, gravity, landing, basic obstacle, death, restart) ·
3 Gameplay test (döngü akıcı olmadan yeni sistem yok) · 4 Obstacles · 5 Levels · 6 Art · 7 UI/Audio · 8 Polish · 9 Mobile optimization · 10 Android build.

## Kurallar
- Önce oynanabilir çekirdek. Shop, skin, reklam, coin ekonomisi, gelişmiş menü, yüzlerce level, gereksiz efekt YOK.
- Teknik kararlar Claude'da; temel gameplay'i değiştiren kararlar sahibine bildirilir.
- Mevcut sistemi incele, yeniden kullan, gereksiz dosya açma, çalışanı bozma. Hata: kök neden + en küçük güvenli düzeltme + yeniden test.
- Her önemli sistemden sonra test (hareket, touch, mouse, şarj, iniş, çarpışma, ölüm, retry, bitiş, kilit açma, kayıt, ekran oranları).
