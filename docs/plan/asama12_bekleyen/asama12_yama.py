import re
def patch(f, pairs):
    s=open(f,encoding="utf-8").read()
    for a,b in pairs:
        assert a in s, (f,a)
        s=s.replace(a,b,1)
    open(f,"w",encoding="utf-8").write(s)
B=r"D:\Repolar\junpline\Assets\Scripts"

# ---- SaveManager: solak, kontrast, para imzası
patch(B+r"\Save\SaveManager.cs",[
('        public const string SensitivityKey = "jl.sensitivity";',
 '        public const string SensitivityKey = "jl.sensitivity";\n        public const string LeftHandedKey = "jl.leftHanded";\n        public const string HighContrastKey = "jl.highContrast";\n        public const string CurrencySigKey = "jl.sig";'),
('        private static void Boot() => Migrate();',
 '''        private static void Boot()
        {
            Migrate();
            VerifyCurrency();
        }'''),
('            set { if (!UnlimitedCurrency) PlayerPrefs.SetInt(GoldKey, Mathf.Max(0, value)); }',
 '            set { if (!UnlimitedCurrency) { PlayerPrefs.SetInt(GoldKey, Mathf.Max(0, value)); SignCurrency(); } }'),
('            set { if (!UnlimitedCurrency) PlayerPrefs.SetInt(GemsKey, Mathf.Max(0, value)); }',
 '            set { if (!UnlimitedCurrency) { PlayerPrefs.SetInt(GemsKey, Mathf.Max(0, value)); SignCurrency(); } }'),
('''        public static bool Vibration
        {''','''        public static bool LeftHanded
        {
            get => PlayerPrefs.GetInt(LeftHandedKey, 0) == 1;
            set => PlayerPrefs.SetInt(LeftHandedKey, value ? 1 : 0);
        }

        public static bool HighContrast
        {
            get => PlayerPrefs.GetInt(HighContrastKey, 0) == 1;
            set => PlayerPrefs.SetInt(HighContrastKey, value ? 1 : 0);
        }

        // ---------- Basit hile koruması ----------
        // Altın/elmas kayıt dosyasında elle değiştirilirse imza tutmaz → CurrencyTampered (ileride liderlik tablosundan
        // ayırmak için). İlerleme SİLİNMEZ: yanlış alarm oyuncuya zarar vermesin.

        private const string SigSalt = "jl-sur-1453";

        /// <summary>Bu açılışta para kaydı imzayla uyuşmadı.</summary>
        public static bool CurrencyTampered { get; private set; }

        private static string CurrencySignature()
        {
            int gold = PlayerPrefs.GetInt(GoldKey, DefaultGold), gems = PlayerPrefs.GetInt(GemsKey, DefaultGems);
            unchecked
            {
                int h = 17;
                foreach (char c in SigSalt + gold + ":" + gems) h = h * 31 + c;
                return h.ToString("X8");
            }
        }

        private static void SignCurrency() => PlayerPrefs.SetString(CurrencySigKey, CurrencySignature());

        public static void VerifyCurrency()
        {
            string sig = PlayerPrefs.GetString(CurrencySigKey, "");
            CurrencyTampered = sig.Length > 0 && sig != CurrencySignature();
            if (CurrencyTampered) Debug.LogWarning("[Save] para kaydı imzası tutmuyor (elle değiştirilmiş olabilir)");
            SignCurrency();
        }

        public static bool Vibration
        {'''),
('                if (refund > 0) PlayerPrefs.SetInt(GoldKey, PlayerPrefs.GetInt(GoldKey, DefaultGold) + refund);',
 '                if (refund > 0) { PlayerPrefs.SetInt(GoldKey, PlayerPrefs.GetInt(GoldKey, DefaultGold) + refund); SignCurrency(); }'),
])

# ---- SettingsView
patch(B+r"\UI\SettingsView.cs",[
('        [SerializeField] private SwitchToggle vibrationToggle;',
 '        [SerializeField] private SwitchToggle vibrationToggle;\n        [SerializeField] private SwitchToggle leftHandedToggle;\n        [SerializeField] private SwitchToggle contrastToggle;'),
('''                if (v) Haptics.Pulse();
            });''','''                if (v) Haptics.Pulse();
            });
            if (leftHandedToggle != null) leftHandedToggle.Toggle.onValueChanged.AddListener(v => SaveManager.LeftHanded = v);
            if (contrastToggle != null) contrastToggle.Toggle.onValueChanged.AddListener(v => SaveManager.HighContrast = v);'''),
('''            vibrationToggle.Snap();
        }''','''            vibrationToggle.Snap();
            if (leftHandedToggle != null) { leftHandedToggle.Toggle.SetIsOnWithoutNotify(SaveManager.LeftHanded); leftHandedToggle.Snap(); }
            if (contrastToggle != null) { contrastToggle.Toggle.SetIsOnWithoutNotify(SaveManager.HighContrast); contrastToggle.Snap(); }
        }'''),
])

# ---- MainMenuBuilder: ayarlar düzeni + jenerik
patch(B+r"\Editor\MainMenuBuilder.cs",[
('var graphics = Section(s.top, "Graphics", "GRAPHICS", new Rect(63f, 798f, 990f, 279f));\n            SegmentedControl quality = QualitySelector(graphics, new Rect(123f, 935f, 870f, 95f));',
 'var graphics = Section(s.top, "Graphics", "GRAPHICS", new Rect(63f, 776f, 990f, 260f));\n            SegmentedControl quality = QualitySelector(graphics, new Rect(123f, 900f, 870f, 95f));'),
('''            var controls = Section(s.top, "Controls", "CONTROLS", new Rect(63f, 1142f, 990f, 380f));
            Txt(controls, "SensitivityLabel", "Sensitivity", new Rect(123f, 1296f, 260f, 56f), fMedium, 40f, C.TextSoft, TextAlignmentOptions.Left);
            Slider sensitivity = RefSlider(controls, "SensitivitySlider", new Rect(388f, 1305f, 605f, 36f), 0.54f);
            Txt(controls, "VibrationLabel", "Vibration", new Rect(123f, 1406f, 260f, 56f), fMedium, 40f, C.TextSoft, TextAlignmentOptions.Left);
            SwitchToggle vibration = RefSwitch(controls, "VibrationToggle", new Rect(860f, 1400f, 133f, 68f), true);''',
'''            var controls = Section(s.top, "Controls", "CONTROLS", new Rect(63f, 1066f, 990f, 560f));
            Txt(controls, "SensitivityLabel", "Sensitivity", new Rect(123f, 1196f, 260f, 56f), fMedium, 40f, C.TextSoft, TextAlignmentOptions.Left);
            Slider sensitivity = RefSlider(controls, "SensitivitySlider", new Rect(388f, 1205f, 605f, 36f), 0.54f);
            Txt(controls, "VibrationLabel", "Vibration", new Rect(123f, 1296f, 500f, 56f), fMedium, 40f, C.TextSoft, TextAlignmentOptions.Left);
            SwitchToggle vibration = RefSwitch(controls, "VibrationToggle", new Rect(860f, 1290f, 133f, 68f), true);
            Txt(controls, "LeftHandedLabel", "Left-handed", new Rect(123f, 1396f, 600f, 56f), fMedium, 40f, C.TextSoft, TextAlignmentOptions.Left);
            SwitchToggle leftHanded = RefSwitch(controls, "LeftHandedToggle", new Rect(860f, 1390f, 133f, 68f), false);
            Txt(controls, "ContrastLabel", "High contrast", new Rect(123f, 1496f, 600f, 56f), fMedium, 40f, C.TextSoft, TextAlignmentOptions.Left);
            SwitchToggle contrast = RefSwitch(controls, "ContrastToggle", new Rect(860f, 1490f, 133f, 68f), false);'''),
('            Set(view, "vibrationToggle", vibration);\n',
 '            Set(view, "vibrationToggle", vibration);\n            Set(view, "leftHandedToggle", leftHanded);\n            Set(view, "contrastToggle", contrast);\n'),
('''            Txt(s.top, "Version", $"JUMPLINE GO  v{PlayerSettings.bundleVersion}", new Rect(63f, 1220f, 990f, 50f), fBold, 30f, C.TextDim, localize: false);''',
'''            Link(BoxButton(s.top, "CreditsButton", new Rect(313f, 1210f, 490f, 106f), "CREDITS"), MenuLink.LinkAction.Open, "credits");
            Txt(s.top, "Version", $"JUMPLINE GO  v{PlayerSettings.bundleVersion}", new Rect(63f, 1350f, 990f, 50f), fBold, 30f, C.TextDim, localize: false);'''),
('            BuildLegal(NewScreen(safe, "legal", false));\n',
 '            BuildLegal(NewScreen(safe, "legal", false));\n            BuildCredits(NewScreen(safe, "credits", false));\n'),
])

# ---- Gameplay: atma düğmesi aynalanır; kontrast modu
patch(B+r"\Editor\GameplaySceneBuilder.Boss.cs",[
('            var btnRoot = Add(bot, "ThrowButton", new Rect(826f, 1592f, 250f, 250f));\n',
 '            var btnRoot = Add(bot, "ThrowButton", new Rect(826f, 1592f, 250f, 250f));\n            btnRoot.go.AddComponent<HandednessMirror>();   // solak modu: sola\n'),
])
patch(B+r"\Editor\GameplaySceneBuilder.cs",[
('            BuildProgressTracker(runner, coins, canvas.GetComponent<LevelFlowController>());\n',
 '            BuildProgressTracker(runner, coins, canvas.GetComponent<LevelFlowController>());\n            Set(canvas.gameObject.AddComponent<Jumpline.UI.ContrastMode>(), "volume", Object.FindAnyObjectByType<UnityEngine.Rendering.Volume>());\n'),
])

# ---- Localization
f=B+r"\Core\Localization.cs"
s=open(f,encoding="utf-8").read()
add='''            { "Left-handed", "Solak modu" },
            { "High contrast", "Kontrastlı mod" },
            { "CREDITS", "JENERİK" },
            { "SOUNDS", "SESLER" },
            { "MUSIC", "MÜZİK" },
            { "3D AND TEXTURES", "3D VE DOKULAR" },
            { "A GAME BY", "BİR OYUNU" },
            { "Made with Unity and Blender. All other models, textures and UI were made for this game.", "Unity ve Blender ile yapıldı. Diğer tüm modeller, dokular ve arayüz bu oyun için üretildi." },
'''
s=s.replace('            { "Vibration", "Titreşim" },\n','            { "Vibration", "Titreşim" },\n'+add,1)
open(f,"w",encoding="utf-8").write(s)
print("ok")
