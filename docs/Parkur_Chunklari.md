# Parkur chunk'ları (otomatik — GameplaySceneBuilder.Chunks.cs tablosundan üretilir; elle düzenlemeyin)

Kurallar `ChunkRules` ile her kurulumda ve EditMode testinde doğrulanır. z = engelin ön kenarı (chunk başından m).

## Engeller (gerçek ölçü, m)

| Kimlik | Tür | Genişlik × Yükseklik × Boy | Çarpışma (yarı gen. / yük. veya tavan) | En kolay chunk | Not |
|---|---|---|---|---|---|
| crates | Jump | 1,17 × 1,10 × 0,71 | 0,58 / 1,10 | Easy |  |
| jug | Jump | 0,71 × 1,00 × 0,71 | 0,45 / 1,00 | Easy |  |
| stalldecor | Jump | 2,27 × 1,05 × 1,84 | 1,00 / 1,05 | Easy |  |
| watermelon | Jump | 1,18 × 0,85 × 0,81 | 0,59 / 0,85 | Easy |  |
| teatable | Jump | 1,60 × 0,85 × 0,78 | 0,80 / 0,85 | Easy |  |
| patpat | Jump | 1,44 × 1,12 × 2,62 | 0,70 / 1,09 | Medium |  |
| bar | Slide | 2,30 × 1,87 × 0,60 | 1,00 / 1,25 | Easy |  |
| teacart | Dodge | 1,87 × 2,34 × 1,20 | 0,93 / 4,00 | Easy |  |
| simitcart | Dodge | 1,35 × 1,85 × 1,09 | 0,68 / 4,00 | Easy |  |
| melonstall | Dodge | 2,30 × 2,48 × 2,30 | 1,00 / 4,00 | Easy |  |
| grill | Dodge | 1,58 × 3,10 × 2,51 | 0,79 / 4,00 | Easy |  |
| taxi | Dodge | 1,84 × 1,62 × 4,43 | 0,86 / 1,62 | Easy | tavanı yürünür 2 renk çeşidi |
| busstop | FullLane | 2,13 × 2,95 × 3,82 | 1,00 / 4,00 | Easy |  |
| minibus | FullLane | 2,30 × 2,60 × 5,80 | 1,00 / 2,59 | Easy | tavanı yürünür |
| citybus | FullLane | 2,16 × 3,00 × 9,15 | 1,00 / 2,99 | Medium | tavanı yürünür |
| truck | Climb | 2,10 × 2,47 × 6,50 | 0,95 / 2,47 | Medium | tavanı yürünür rampalı 2 renk çeşidi |
| melon | Climb | 2,30 × 2,38 × 6,26 | 1,00 / 2,38 | Medium | tavanı yürünür rampalı |
| tractor | Climb | 2,30 × 2,41 × 6,79 | 1,00 / 2,41 | Medium | tavanı yürünür rampalı |
| taxi_moving | MovingObstacle | 1,84 × 1,62 × 4,43 | 0,86 / 1,62 | Medium | tavanı yürünür 3 m/sn gelir |
| gate | Overhead | 9,50 × 9,00 × 5,22 | 3,65 / 5,20 | Hard |  |

## Sokak chunk'ları

| # | Kimlik | Zorluk | Boy | Engel yerleşimi | Para rotası | Vurgu / hareketli süs | Tasarım notu |
|---|---|---|---|---|---|---|---|
| 1 | e_crates_center | Easy | 30 | g0: crates orta @12 | düz orta @3 (20 m) | Lanterns / 3 | Ortada sandık: zıpla ya da yana geç. Para yayı üstünden. |
| 2 | e_jug_watermelon | Easy | 30 | g0: jug sol @8<br>g1: watermelon sağ @21,5 | düz orta @3 (22 m) | ShopFront / 3 | Solda testi, sağda karpuz; orta şerit hep açık. |
| 3 | e_bar_intro | Easy | 30 | g0: bar orta @12 | düz orta @6 (12 m) | None / 3 | İlk kayma: ortada bariyer, paralar altından alçak hat. |
| 4 | e_cart_dodge | Easy | 30 | g0: teacart sol @8<br>g1: simitcart sağ @22 | düz orta @3 (22 m) | ShopFront / 3 | Solda çay arabası, sağda simit arabası: ortadan koş. |
| 5 | e_safe_bazaar | Easy | 30 | — (güvenli alan) | S sol→sağ @4 (22 m) | Arch / 5 | Güvenli alan: çarşı kemeri, S para rotası, bol süs. |
| 6 | e_stall_crates | Easy | 30 | g0: stalldecor sol @7<br>g1: crates sağ @21,5 | düz orta @3 (22 m) | None / 3 | Solda tezgâh süsü, sağda sandık: ortada düz para. |
| 7 | e_teatable_bar | Easy | 30 | g0: teatable orta @8<br>g1: bar sol @22 | düz orta @2 (14 m)<br>S orta→sağ @17 (9 m) | Lanterns / 3 | Ortada çay masası (zıpla), sonra solda bariyer; paralar sağa kıvrılır. |
| 8 | e_busstop_side | Easy | 30 | g0: busstop sağ @9 | düz sol @3 (22 m) | Column / 3 | Sağda durak: tek şerit kapalı, sol şeritte paralar. |
| 9 | e_safe_lanterns | Easy | 30 | — (güvenli alan) | düz orta @4 (22 m) | Lanterns / 4 | Güvenli alan: fener dizisi, ortada para. |
| 10 | m_weave | Medium | 45 | g0: crates sol @6<br>g1: bar orta @19<br>g2: jug sağ @32 | düz sol @1 (9 m)<br>S sol→orta @11 (6 m)<br>düz orta @17 (5 m)<br>S orta→sağ @24 (6 m)<br>düz sağ @30 (6 m) | ShopFront / 3 | Zikzak: sol sandık → orta bariyer → sağ testi; S paralar yolu gösterir. |
| 11 | m_two_carts | Medium | 30 | g0: teacart sol @8<br>g0: melonstall sağ @8<br>g1: crates orta @22,5 | düz orta @2 (24 m) | None / 3 | İki yanda araba (iki şerit kapalı) → ortadan geç, sonra ortadaki sandığı zıpla. |
| 12 | m_bus_wall | Medium | 30 | g0: minibus sol @6<br>g0: busstop sağ @7 | düz orta @2 (22 m) | Lanterns / 3 | Solda minibüs, sağda durak: orta şerit tek yol. |
| 13 | m_taxi_rows | Medium | 45 | g0: taxi sol @6<br>g1: taxi sağ @22,5 | düz orta @2 (36 m) | Column / 3 | Solda sonra sağda taksi; ortada uzun para dizisi. |
| 14 | m_bar_cart | Medium | 30 | g0: bar sol @9<br>g0: teacart sağ @8<br>g1: crates orta @22 | düz orta @2 (25 m) | None / 3 | Solda bariyer (kay) + sağda araba; orta açık, sonra ortada sandık. |
| 15 | m_patpat | Medium | 30 | g0: patpat orta @7<br>g1: simitcart sol @22 | düz orta @1 (12 m)<br>S orta→sağ @14 (8 m) | ShopFront / 3 | Ortada patpat (üstünden zıpla), sonra solda simit arabası; paralar sağa döner. |
| 16 | m_citybus | Medium | 45 | g0: citybus sol @6<br>g0: minibus sağ @7<br>g1: bar orta @28 | düz orta @2 (34 m) | BridgePier / 3 | Solda otobüs, sağda minibüs (iki şerit) → ortadan; sonra ortada bariyer. |
| 17 | m_moving_taxi | Medium | 45 | g0: taxi_moving orta @20<br>g1: crates sol @37 | düz sol @2 (30 m) | Lanterns / 3 | Ortada karşıdan gelen taksi (sarı-siyah işaret): sola geç, sonra soldaki sandığı zıpla. |
| 18 | m_climb_melon | Medium | 45 | g0: melon orta @6<br>g1: jug sol @25<br>g1: watermelon sağ @25 | düz orta @2 (20 m)<br>düz orta @22 (8 m) | None / 3 | Ortada karpuz kamyonu: rampadan çık, tavanda koş; sonra iki yanda testi/karpuz. |
| 19 | m_tractor | Medium | 45 | g0: tractor sağ @6<br>g1: bar sol @26 | düz sağ @2 (16 m)<br>S sağ→orta @19 (6 m)<br>düz orta @25 (6 m) | Column / 3 | Sağda traktör (çıkılır), sonra solda bariyer; paralar traktörden ortaya iner. |
| 20 | h_train_left | Hard | 45 | g0: truck sol @6<br>g0: minibus sol @14,5<br>g0: minibus sol @22,4<br>g0: minibus orta @8<br>g0: minibus orta @15,9 | düz sol @1 (30 m) | None / 3 | Tren: solda kamyon + iki minibüs (tavandan tavana), ortada minibüsler; sağ şerit yerden kaçış. |
| 21 | h_train_gate | Hard | 45 | g0: truck sağ @6<br>g0: minibus sağ @14,5<br>g0: minibus sağ @22,4<br>g0: minibus orta @8<br>g0: minibus orta @15,9<br>g0: gate orta @9 | düz sağ @1 (30 m) | None / 3 | Tren sağda, üstünden çarşı kemeri geçer (tavanda koşarken kemerin altından geçilir). Sol şerit yerden kaçış. |
| 22 | h_double_ramp | Hard | 45 | g0: truck sol @6<br>g0: minibus sol @14,5<br>g0: melon sağ @6<br>g0: minibus sağ @14,5 | düz sağ @1 (22 m) | None / 3 | İki yanda rampalı araç + minibüs; orta yerden açık. |
| 23 | h_wall_slide | Hard | 45 | g0: minibus sol @6<br>g0: bar orta @8<br>g1: crates sağ @24<br>g2: teacart orta @37 | düz sağ @2 (34 m) | Lanterns / 3 | Solda minibüs + ortada bariyer (sağ açık) → sağda sandık → ortada araba. |
| 24 | h_cart_maze | Hard | 45 | g0: teacart sol @6<br>g0: melonstall orta @6<br>g1: simitcart sağ @20,6<br>g1: grill orta @20,6<br>g2: bar sol @35,4 | düz sağ @2 (9 m)<br>S sağ→sol @12 (7 m)<br>düz sol @20 (12 m) | ShopFront / 3 | Araba labirenti: sol+orta kapalı → sağ; orta+sağ kapalı → sol; sonda solda bariyer. |
| 25 | h_moving_flank | Hard | 45 | g0: minibus sağ @6<br>g1: taxi_moving sol @24,5 | düz orta @2 (34 m) | None / 3 | Sağda minibüs, sonra solda karşıdan gelen taksi; orta hep açık. |
| 26 | h_roof_hop | Hard | 45 | g0: truck sol @6<br>g0: minibus sol @14,5<br>g0: citybus orta @9 | düz sol @1 (26 m) | None / 3 | Solda kamyon + minibüs, ortada otobüs: tavandan otobüse atlanır; sağ yerden açık. |
| 27 | h_slide_jump | Hard | 45 | g0: bar sol @6<br>g0: crates sağ @6<br>g1: teacart orta @19<br>g2: bar sol @33<br>g2: jug sağ @33 | düz orta @1 (14 m)<br>S orta→sol @15 (6 m)<br>düz sol @21 (8 m) | ShopFront / 3 | Solda bariyer + sağda sandık → ortada araba → solda bariyer + sağda testi. |
| 28 | portal_plaza (özel) | Medium | 90 | g0: jug sol @26<br>g0: watermelon sağ @26<br>g1: busstop sol @48<br>g1: melonstall sağ @48<br>g2: portal orta @66 | düz orta @20 (44 m) | Arch / 3 | Portal meydanı: iki yanda testi/karpuz, sonra durak + tezgâh orta şeride toplar, portal ortada. |

## Çatı chunk'ları (portal evreni)

| # | Kimlik | Zorluk | Boy | Engel yerleşimi | Para rotası | Vurgu / hareketli süs | Tasarım notu |
|---|---|---|---|---|---|---|---|
| 1 | r_crates | Easy | 30 | g0: crates orta @12 | düz orta @3 (20 m) | None / 3 | Çatı: ortada sandık. |
| 2 | r_bar_jug | Easy | 30 | g0: bar sol @9<br>g1: jug sağ @22 | düz orta @3 (22 m) | None / 3 | Çatı: solda bariyer, sağda testi. |
| 3 | r_safe | Easy | 30 | — (güvenli alan) | S sağ→sol @4 (22 m) | None / 4 | Çatı: güvenli alan, S paralar. |
| 4 | r_walls | Medium | 30 | g0: melonstall sol @8<br>g0: simitcart sağ @8 | düz orta @2 (22 m) | None / 3 | Çatı: iki yanda tezgâh, ortadan geç. |
| 5 | r_weave | Medium | 45 | g0: crates sol @6<br>g1: bar orta @19<br>g2: jug sağ @32 | düz sol @1 (9 m)<br>S sol→orta @11 (6 m)<br>düz orta @17 (5 m)<br>S orta→sağ @24 (6 m)<br>düz sağ @30 (6 m) | None / 3 | Çatı zikzak: sol sandık → orta bariyer → sağ testi. |
| 6 | r_mix | Hard | 45 | g0: teacart sol @6<br>g0: busstop orta @6<br>g1: bar sağ @22<br>g2: crates sol @35 | düz sağ @2 (16 m) | None / 3 | Çatı: sol+orta kapalı → sağda bariyer → solda sandık. |
