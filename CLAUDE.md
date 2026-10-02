# JUNPLINE — proje notları (Claude için)

3D hyper-casual mobil koşu oyunu (Android). Tek kontrol: **basılı tut = şarj, bırak = zıpla**; basma süresi zıplama gücünü belirler.
Döngü: RUN → CHARGE → RELEASE → JUMP → LAND → RUN. Bölüm tabanlı (ilk hedef 10 bölüm).
Tam tasarım isteği: `docs/BRIEF.md`. Kullanıcı Türkçe konuşur, Türkçe cevap ister.

## Ortam (2 Eki 2026)

| | |
|---|---|
| Unity | **6000.6.4f1** — `C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe` (Hub 3.22.1, lisans etkin) |
| Render | URP 17.6 (şablon `urp-blank`), `Assets/Settings/Mobile_RPAsset` + `PC_RPAsset` |
| Input | Yeni Input System 1.20 (`activeInputHandler: 1`, eski Input Manager kapalı) |
| Test | Unity Test Framework 1.8 (`Assets/Tests/EditMode`, `PlayMode`) |
| Android | **Modül henüz kurulu değil** (Hub → Installs → Add modules: Android Build Support + SDK/NDK + OpenJDK) |
| Blender | 5.2.2 LTS — `C:\Program Files\Blender Foundation\Blender 5.2\blender.exe` (4.5 de kurulu) |

## Komutlar

```powershell
# Toplu mod (editör KAPALIYKEN; açıksa proje kilitli olur)
& "C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe" -batchmode -quit -projectPath D:\Repolar\junpline -logFile - 
# Testler
... -batchmode -projectPath D:\Repolar\junpline -runTests -testPlatform EditMode -testResults Logs\edit.xml
# Blender betiği
& "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" -b -P Blender\scripts\<betik>.py
```

## Klasörler

- `Assets/Scripts/{Core,Player,Camera,Level,Obstacles,UI,Audio,Save,Editor}` — her sistem ayrı (tek dev script yok).
- `Assets/Art`, `Prefabs`, `Audio`, `Scenes/{MainMenu,Levels}`, `Tests`.
- `Blender/scripts` (asset üreten Python), `Blender/source` (.blend) — Unity'ye `.fbx` olarak `Assets/Art/...` altına çıkar.

## Kurallar

- Önce oynanabilir çekirdek; shop/skin/reklam/coin ekonomisi YOK (kullanıcı onayı olmadan).
- Ayarlanabilir her değer `[SerializeField]` ya da ScriptableObject; magic number yok.
- Gameplay mekaniğini değiştiren büyük kararlar kullanıcıya sorulur.
- Keystore / hesap şifrelerini Claude girmez.
