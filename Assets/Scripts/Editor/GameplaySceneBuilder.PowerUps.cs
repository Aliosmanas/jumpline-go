using Jumpline.Level;
using Jumpline.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using static Jumpline.EditorTools.UIKit;

namespace Jumpline.EditorTools
{
    /// <summary>
    /// Güçler (aşama 05): koşucuya PowerUps (süreler CharacterStats'tan), yolda güç kutuları (PowerUpField: ikon + renkli
    /// parıltı, kameraya bakar), sol altta etkin güç süreleri (PowerUpHud), mıknatıs/2x altın için CoinField bağlantısı.
    /// </summary>
    public static partial class GameplaySceneBuilder
    {
        private const string CharacterStatsPath = "Assets/Settings/CharacterStats.asset";

        private static readonly (PowerUpType type, string icon, string glow, float weight)[] PowerUpDefs =
        {
            (PowerUpType.Magnet, "magnet", "#FF5A6E", 1.0f),
            (PowerUpType.Shield, "shield", "#55CFFF", 0.9f),
            (PowerUpType.DoubleGold, "double_gold", "#FFD54A", 0.9f),
            (PowerUpType.SpeedBoost, "speed_boost", "#9BFF6A", 0.7f),
        };

        private static void BuildPowerUps(Transform runner, CoinField coins)
        {
            var player = runner.GetComponent<PlayerController>();
            var pu = runner.gameObject.AddComponent<PowerUps>();
            Set(pu, "stats", AssetDatabase.LoadAssetAtPath<CharacterStats>(CharacterStatsPath));
            var shatter = MakeBurst(runner, "ShatterFx", Hex("#FFF1C2"), Hex("#C9B79A"), 4f, 11f, 1.4f);
            shatter.transform.SetParent(null, true);
            var sr = shatter.GetComponent<ParticleSystemRenderer>();
            sr.sharedMaterial = SplashMaterial();
            sr.renderMode = ParticleSystemRenderMode.Billboard;
            var smain = shatter.main;
            smain.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.8f);
            smain.startColor = new ParticleSystem.MinMaxGradient(new Color(0.95f, 0.88f, 0.72f, 0.9f), new Color(0.65f, 0.58f, 0.5f, 0.75f));
            Set(pu, "shatterFx", shatter);
            Set(player, "powerUps", pu);
            if (coins != null) Set(coins, "powerUps", pu);

            // yol üstü kutular
            var fieldGo = new GameObject("PowerUpField");
            var field = fieldGo.AddComponent<PowerUpField>();
            Set(field, "player", player);
            Set(field, "powerUps", pu);
            var so = new SerializedObject(field);
            var kinds = so.FindProperty("kinds");
            kinds.arraySize = PowerUpDefs.Length;
            for (int i = 0; i < PowerUpDefs.Length; i++)
            {
                var d = PowerUpDefs[i];
                var holder = new GameObject("Tpl_" + d.type);
                holder.transform.SetParent(fieldGo.transform, false);
                var glow = new GameObject("Glow").AddComponent<SpriteRenderer>();
                glow.transform.SetParent(holder.transform, false);
                glow.sprite = softCircle;
                Color gc = Hex(d.glow);
                glow.color = new Color(gc.r, gc.g, gc.b, 0.75f);
                glow.transform.localPosition = new Vector3(0f, 0f, 0.05f);
                ScaleSprite(glow, 2.0f);
                var ring = new GameObject("Ring").AddComponent<SpriteRenderer>();
                ring.transform.SetParent(holder.transform, false);
                ring.sprite = ringGlow != null ? ringGlow : circle;
                ring.color = Color.Lerp(gc, Color.white, 0.4f);
                ring.transform.localPosition = new Vector3(0f, 0f, 0.02f);
                ScaleSprite(ring, 1.5f);
                var icon = new GameObject("Icon").AddComponent<SpriteRenderer>();
                icon.transform.SetParent(holder.transform, false);
                icon.sprite = Icons.TryGetValue(d.icon, out var spr) ? spr : null;
                ScaleSprite(icon, 1.15f);
                foreach (var r in holder.GetComponentsInChildren<SpriteRenderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
                holder.SetActive(false);
                var e = kinds.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("type").enumValueIndex = (int)d.type;
                e.FindPropertyRelative("template").objectReferenceValue = holder;
                e.FindPropertyRelative("weight").floatValue = d.weight;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            Set(field, "pickupFx", MakeBurst(fieldGo.transform, "PowerUpPickupFx", Hex("#FFFFFF"), Hex("#FFE08A"), 2f, 6f, 0.2f));

            BuildPowerUpHud(pu);
        }

        private static void ScaleSprite(SpriteRenderer r, float worldSize)
        {
            if (r.sprite == null) return;
            float s = Mathf.Max(r.sprite.bounds.size.x, r.sprite.bounds.size.y);
            r.transform.localScale = Vector3.one * (worldSize / Mathf.Max(0.001f, s));
        }

        /// <summary>Sol alt: etkin güçler (ikon + kalan süre çubuğu), yeni güç en alta, eskiler yukarı kayar.</summary>
        private static void BuildPowerUpHud(PowerUps pu)
        {
            var root = NewUI("PowerUpHud", hudBot.rt);
            var rect = new Rect(36f, 1712f, 300f, 100f);
            Place(root, hudBot, rect);
            var hud = root.gameObject.AddComponent<Jumpline.UI.PowerUpHud>();
            var slots = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object>>();
            foreach (var d in PowerUpDefs)
            {
                var slotRt = NewUI("Slot_" + d.type, root);
                slotRt.anchorMin = slotRt.anchorMax = slotRt.pivot = new Vector2(0.5f, 0.5f);
                slotRt.sizeDelta = rect.size;
                slotRt.anchoredPosition = Vector2.zero;
                var slot = new Box(slotRt, rect);
                var group = slotRt.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = group.interactable = false;
                Panel(slot, "Back", rect, Hex("#1B2236", 0.88f), Hex("#2A3556", 0.88f), 50f, Hex(d.glow));
                Icon(slot, d.icon, new Rect(rect.x + 8f, rect.y + 8f, 84f, 84f), Color.white);
                Panel(slot, "Track", new Rect(rect.x + 104f, rect.y + 40f, 176f, 22f), C.NavyDark, C.NavyDark, 11f, Hex("#5D6A91"));
                var fill = Add(slot, "Fill", new Rect(rect.x + 107f, rect.y + 43f, 170f, 16f));
                var img = fill.go.AddComponent<Image>();
                Rounded(img, 8f);
                img.color = Hex(d.glow);
                img.raycastTarget = false;
                PivotLeft(fill);
                slots.Add(new System.Collections.Generic.Dictionary<string, object>
                {
                    { "type", (int)d.type }, { "group", group }, { "fill", fill.rt }, { "fullWidth", 170f },
                });
            }
            Set(hud, "powerUps", pu);
            SetStructArray(hud, "slots", slots);
        }
    }
}
