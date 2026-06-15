using UnityEngine;

namespace RoadPlay
{
    /// <summary>
    /// Holds the current on-screen feedback state (banner text + full-screen
    /// flash). The bootstrap's OnGUI reads these and draws them, so all IMGUI
    /// lives in one place.
    /// </summary>
    public class FeedbackManager : MonoBehaviour
    {
        string banner = "";
        float bannerTimer;
        Color bannerColor;

        float flashTimer;
        float flashDuration;
        Color flashColor;

        float nearMissCooldown;

        public void Violation(string msg)
        {
            banner = msg; bannerTimer = 2.2f; bannerColor = new Color(0.85f, 0.1f, 0.1f);
            Flash(new Color(1f, 0f, 0f), 0.6f);
        }

        public void Good(string msg)
        {
            banner = msg; bannerTimer = 1.6f; bannerColor = new Color(0.12f, 0.6f, 0.22f);
        }

        public void NearMiss()
        {
            if (nearMissCooldown > 0f) return;
            nearMissCooldown = 2f;
            banner = "WATCH OUT!"; bannerTimer = 1.8f; bannerColor = new Color(0.9f, 0.5f, 0.05f);
            Flash(new Color(1f, 0.5f, 0f), 0.5f);
        }

        void Flash(Color c, float dur)
        {
            flashColor = c; flashTimer = dur; flashDuration = dur;
        }

        void Update()
        {
            if (bannerTimer > 0f) bannerTimer -= Time.deltaTime;
            if (flashTimer > 0f) flashTimer -= Time.deltaTime;
            if (nearMissCooldown > 0f) nearMissCooldown -= Time.deltaTime;
        }

        public bool ShowBanner => bannerTimer > 0f;
        public string Banner => banner;
        public Color BannerColor => bannerColor;

        public bool ShowFlash => flashTimer > 0f;
        public Color FlashColor
        {
            get
            {
                float a = 0.4f * Mathf.Clamp01(flashTimer / Mathf.Max(0.0001f, flashDuration));
                return new Color(flashColor.r, flashColor.g, flashColor.b, a);
            }
        }
    }
}
