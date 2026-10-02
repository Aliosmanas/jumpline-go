using Jumpline.Player;
using Jumpline.Save;
using Jumpline.Shop;
using UnityEngine;

namespace Jumpline.UI
{
    /// <summary>
    /// Menüdeki 3B karakter vitrini: giyili kostümü uygular, kostüm değişince günceller
    /// ve çok yavaş bir şekilde sağa sola salınır.
    /// </summary>
    public class RunnerDisplay : MonoBehaviour
    {
        [SerializeField] private RunnerSkin runner;
        [SerializeField] private SkinLibrary library;
        [Tooltip("Salınım genliği (derece) ve süresi (saniye).")]
        [SerializeField] private float swayAngle = 8f;
        [SerializeField, Min(0.1f)] private float swayPeriod = 9f;

        private float baseYaw;

        private void Awake() => baseYaw = runner.transform.localEulerAngles.y;

        private void OnEnable()
        {
            ShopService.SkinEquipped += OnSkinEquipped;
            OnSkinEquipped(SaveManager.EquippedSkin);
        }

        private void OnDisable() => ShopService.SkinEquipped -= OnSkinEquipped;

        private void OnSkinEquipped(string skinId) => runner.Apply(library.Find(skinId));

        private void Update()
        {
            float yaw = baseYaw + Mathf.Sin(Time.unscaledTime / swayPeriod * Mathf.PI * 2f) * swayAngle;
            runner.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }
    }
}
