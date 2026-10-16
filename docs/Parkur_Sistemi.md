# JUMPLINE GO — Chunk parkur sistemi (16 Eki 2026)

Eski parkur (rastgele desenler + `Course_SurIci.json`) söküldü. Yerine elle tasarlanmış, kuralla doğrulanmış küçük
parçalar (chunk) geldi. Rastgelelik yalnız **sıradadır**; chunk'ın içi sabittir ve test edilmiştir.

Kararlar (kullanıcı, 16 Eki): araç üstü koşu kalır (ayrı tür `Climb`) · zorluk bölüm içindeki mesafeye göre ·
şerit genişliği **2.2** (yol 6.6 m) · hız bölüm 1'de **8 → 16 m/sn**, sonraki bölümler +0.4 / +0.5.

| Soru | Cevap |
|---|---|
| Unity | 6000.6.4f1, URP 17.6 |
| Karakter kontrolü | kendi script: `Assets/Scripts/Player/PlayerController.cs` (Input System, kaydırma) |
| Mesh formatı | Meshy GLB → Blender (`Blender/scripts/process_meshy.py`) → FBX (`Assets/Art/...`) |
| Ekran | dikey (portrait), referans 1116×2000 |
| Eski parkur scriptleri | `ObstacleSpawner.cs`, `Course_SurIci.json`, `make_course_json.py`, `check_course.py` — **silindi** |

## 1. Eski sistem nasıl söküldü (adım adım)

1. Bağımlılıklar bulundu: `ObstacleSpawner` → BossChase (fırlatılan sandık, portal), LevelDirector (yoğunluk),
   LevelFlowController (elmasla devamda temizlik), 9 test, sahne kurucu.
2. Yeni sistem yanına kuruldu (`Level/Chunks/`), aynı dış API'yi verdi: `ClearRange`, `IgnoreCoursePortal`, `ThrownTemplate`.
3. Bağlı kodlar yeni tipe çevrildi; `LevelSpec.obstacleGap / courseSpacing` → `portalDistance`.
4. Eski dosyalar `git rm` ile silindi (geçmişte duruyor).
5. Sahne yeniden kuruldu (`Jumpline → Build All Scenes`), testler çalıştırıldı.

## 2. Klasör ve script yapısı

| Yer | İçerik |
|---|---|
| `Assets/Scripts/Level/Chunks/ChunkTypes.cs` | enum'lar: `ObstacleKind` (Jump, Slide, Dodge, FullLane, MovingObstacle, CollectibleLine, SafeZone, Climb, Overhead), zorluk, dünya, para biçimi, görsel vurgu |
| `ObstacleDefinition.cs` | **ScriptableObject — her mesh için bir tane**: prefab, renk çeşitleri, tür, gerçek boyut, ön kenar→pivot, çarpışma kutusu, şerit sayısı, en kolay zorluk, yürünür tavan / rampa, hareket hızı, renk kodu |
| `ObstacleLibrary.cs` | tüm tanımların listesi (kimlikle erişim) |
| `LevelChunk.cs` | **ScriptableObject — chunk verisi**: zorluk, dünya, boy, `ObstaclePlacement[]` (kimlik, şerit, z, grup), `CoinRoute[]`, vurgu, hareketli süs sayısı |
| `ChunkRules.cs` | kurallar + geçilebilirlik simülasyonu (aşağıda) |
| `DifficultyDirector.cs` | mesafeye göre chunk seçer; tekrar ve nefes kuralları |
| `ObjectPool.cs` | prefab başına havuz (Instantiate/Destroy yok) |
| `ChunkSpawner.cs` | chunk'ları koşucunun önüne dizer, arkadakini havuza alır; para, vurgu, hareketli süs, portal meydanı |
| `MovingObstacle.cs` | şeridinde koşucuya doğru yavaşça gelen engel (fizik yok) |
| `ObstacleVisual.cs` | prefab'ın "Visual" çocuğu: malzeme değişimi (mesh'e dokunmadan) |
| `AmbientMotion.cs` | hareketli süs: sallanma, yanıp sönme, süzülme, dönme (fizik yok) |
| `Assets/Scripts/Editor/GameplaySceneBuilder.Chunks.cs` | engel prefab/tanım kurulumu, **chunk tablosu**, vurgu ve süs prefab'ları, sahne bağlantısı |
| `Assets/Prefabs/Obstacles/` | `Obs_<kimlik>.prefab` (+ renk çeşitleri) |
| `Assets/Prefabs/Accents/` | vurgular (kemer, fener, minare, köprü ayağı, tezgâh) ve hareketli süsler (kuş, flama, fener) |
| `Assets/Data/Obstacles/` | `Obs_<kimlik>.asset` tanımlar + `ObstacleLibrary.asset` |
| `Assets/Data/Chunks/` | `Chunk_<kimlik>.asset` |
| `Assets/Tests/EditMode/ChunkRulesTests.cs` | kurallar + bozuk chunk'ların yakalanması + zorluk sırası |
| `Assets/Tests/PlayMode/ChunkRunCapture.cs` | parkur önizleme kareleri → `Logs/ChunkRun/` (elle) |

Prefab düzeni: **kök** (boş transform + `Obstacle` çarpışma profili, ölçek 1) → **Visual** (mesh, `ObstacleVisual`) →
çalışma anında **Hint** (yerdeki renkli işaret). Zemin ve kenar dekoru harita parçalarında (`MapChain`, 30 m);
chunk yalnız oynanış katmanıdır, girişi/çıkışı boş zemindir → her chunk her chunk'a bağlanır.

## 3. Kurallar (ChunkRules — kurulumda, testte ve oyunda denetlenir)

| Kural | Değer |
|---|---|
| Chunk girişi ve çıkışı boş | ≥ 6 m (iki chunk arası ≥ 12 m) |
| Engel grubu sayısı | ≤ 3; grup 1–3 engel (aynı şeritte bitişik araçlar tek "tren") |
| Gruplar arası boşluk | ≥ 12 m (toparlanma ≥ 8 m da sağlanır) |
| Üç şerit aynı anda kapalı | asla (rampalı araç yol sayılır) |
| İki şeridi kapatan | yalnız orta / zor chunk |
| Zıplanan / kayılan engelin yanı | komşu şeritte boş geçiş |
| 6 m içinde farklı hareket | ≤ 2 |
| Hareketli engel | yaklaşırken chunk başını geçmez |
| Geçilebilirlik | **her şeritten girilince sona varılabilir** (0.25 m adımlı simülasyon: yer / araç tavanı, şerit değiştirme payı 3 m, tavandan tavana 3.2 m atlama, kemer altında tavanda kayma) |
| Engel ölçüsü | zıpla 0.8–1.2 m, kayma boşluğu 1.2–1.5 m, çarpışma yarı genişliği ≤ 1.0, görsel genişlik ≤ 2.3 m |

Zorluk (`DifficultyDirector`): ilk **300 m** kolay · **300–800 m** kolay + orta · **800 m+** orta + zor
(zor olasılığı %35 → %60). Aynı chunk üst üste en fazla 2 · zordan sonra hep kolay/orta. Bölüm başına eşikler
30 / 60 m öne çekilir (en az 120 / 400). Portal meydanı bölüm içinde 1400 m'de (+50 m/bölüm), canavar kovalamasında yok.

## 4. Chunk listesi

Tam liste (34 chunk: 27 sokak, 6 çatı, 1 portal meydanı) ve engel ölçüleri **otomatik** üretilir:
[Parkur_Chunklari.md](Parkur_Chunklari.md). Her chunk için zorluk, boy, engel yerleşimi (grup/şerit/z),
para rotası, vurgu ve tasarım notu orada.

Yeni chunk eklemek: `GameplaySceneBuilder.Chunks.cs` → `ChunkTable()`'a bir satır → `Build All Scenes`.
Kural bozuksa konsolda `Chunk kuralı:` hatası çıkar ve chunk havuza girmez.

## 5. Meshy meshleri için kontrol listesi

| Adım | Ne yapılır | Nerede |
|---|---|---|
| Format | GLB indir → Blender'da `process_meshy.py` (sadeleştir, ölçekle, dokuları adlı PNG'ye çıkar) → FBX | `Blender/scripts` |
| Pivot | taban merkezi yerde (y = 0), ön yüz koşucuya | builder `PlaceMeshy` tabanı oturtur |
| Ölçek | **tanımdaki gerçek boyut**: zıplanan engel yüksekliği tabloda; görsel genişlik ≤ 2.3 m (taşan döndürülür ya da orantılı küçültülür) | `BuildObstacleLibrary` → `FitHeight / FitWidth / FitCeiling` |
| Rotasyon | uzun kenar yol boyunca (durak, mangal 90° döner); araçlar FBX ekseniyle | spec `turn` |
| Çarpışma | fizik collider yok: `Obstacle` yükseklik profili (kutu ya da araçta mesh'ten dilimli profil, rampa düz eğim) | `Obstacle.cs`, `AutoSegments` |
| Malzeme | URP Lit, beyaz taban rengi, metalik 0, pürüzsüzlük ~0.3; normal haritası "Normal map" | `MeshyImport.Tune` |
| Doku | 1024 (bina 2048); FBX'in yanındaki `_Tex` klasörü | `tex_export.py`, `MeshyImport.Prepare` |
| LOD | engellerde gerek yok (küçük); binalarda yapılacak (performans işi) | — |
| Prefab | kök + Visual + ObstacleVisual; renk çeşidi = ayrı prefab (yalnız malzeme farklı) | `SaveObstaclePrefab` |
| Doğrulama | `ChunkRules.ValidateDefinition` (yükseklik, genişlik) + EditMode testi | `ChunkRulesTests` |

## 6. Renk bozukluğu ve çıkıntı düzeltme rehberi

**Renk / doku**

| Belirti | Sebep | Düzeltme |
|---|---|---|
| Model gri/beyaz, doku yok | FBX'te paketli adsız doku | `tex_export.py` ile adlı PNG; `MeshyImport.Prepare` yeniden eşler |
| Mavi-gri lekeler | normal haritası renk olarak bağlanmış | `MeshyImport.Tune` dokunun türünü "Normal map" yapar |
| Çok koyu / metalik | Meshy metalik haritası | `Tune` `_MetallicGlossMap`'i kaldırır, metalik 0 |
| Renk çeşidi istendi | — | mesh'e dokunma: `ObstacleVisual` yuva adı → yeni malzeme (taksi beyaz/kırmızı, kamyon kırmızı/sarı) |
| Engel zeminden ayrışmıyor | renk yakın | yer işareti renk koduyla: turuncu zıpla, turkuaz kay, kırmızı kaç, sarı-siyah hareketli, yeşil çık |

**Çıkıntı / boyut**

| Belirti | Düzeltme |
|---|---|
| Görsel yan şeride taşıyor | `MaxVisualWidth` 2.3: uzun kenar döndürülür, değilse orantılı küçültülür, bariyer yalnız enine |
| Görünmez duvar / havada çarpma | çarpışma profili görselden: kutu engelde görsel sınırları, araçta mesh dilimleri |
| Araç üstüne çıkınca "ışınlanma" | dilim farkı ≤ 0.32 m ise eğim (pürüzsüz), rampa ilk düzlüğe kadar düz |
| Yolda gereksiz yükselti | alçak kemer kaldırıldı; süsler yalnız yol ortasından ≥ 4.6 m yanda (yolun dışında), kemer 3.7 m üstte |
| Kemer tavanında takılma | yalnız zor tren chunk'ında; tavanda kayarak geçilir (simülasyon denetler) |

## 7. Görsel canlılık (chunk alanlarına bağlı)

| Kural | Uygulama |
|---|---|
| Her 100 m'de vurgu | chunk `accent` alanı; yoksa en geç 90 m'de yedek vurgu (fener, minare, tezgâh, köprü ayağı) |
| Chunk başına ≥ 3 hareketli süs | `movingDecor` (güvenli alanlarda 4–5): kuş sürüsü, flama, sallanan fener — `AmbientMotion`, fizik yok |
| Kenar dekoru yola taşmaz | süs konumu yol ortasından ≥ 4.6 m yanda |
| Para = yön gösterici | düz dizi zıplanan engelin üstünden **yay**, bariyerin altında **alçak hat**; şerit değişiminde **S** |
| Tema 500–800 m'de değişir | `MapChain`: mahalle / sur üstü 17–26 parça (30 m), arada 30 m geçiş parçası |
| Engel renk kodu sabit | `ObstacleDefinition.CodeColor` + yer işaretleri (`GameplaySceneBuilder.Hints.cs`) |

## 8. Bu hafta yapılacaklar (küçük, test edilebilir)

| # | Görev | Bitti sayılır |
|---|---|---|
| 1 | Telefonda 3 koşu (bölüm 1): ilk 300 m yalnız kolay mı, işaretler 20–25 m'den okunuyor mu | not: hangi chunk zor/kolay geldi |
| 2 | Zor tren chunk'larını (h_train_*) tek tek dene: kemer altında kayma rahat mı | gerekirse kemer yalnız tavanı yüksek trende |
| 3 | 8 → 16 m/sn hız eğrisi: 800 m'de hız hissi yeterli mi | `speedTimeConstant` ayarı |
| 4 | Para rotalarını gözle: S ve yaylar yol gösteriyor mu | `Logs/ChunkRun` kareleri |
| 5 | Çatı chunk'ları (portal evreni) sayısını artır: 30 m'lik 3 yeni | `ChunkRulesTests` geçer |
| 6 | Bina LOD'u (performans) — chunk sisteminden bağımsız | Oppo'da FPS ölçümü |
| 7 | Yeni Meshy engeli gelirse: tabloya satır + ölçü → `Build All Scenes` | `Definitions_AreSizedForLanes` geçer |
