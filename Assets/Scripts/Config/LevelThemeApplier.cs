using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class LevelThemeApplier : MonoBehaviour
{
    [Header("Scene refs")]
    [SerializeField] Camera mainCam;
    [SerializeField] Light sunLight;

    [Header("Renderers to tint (no materials!)")]
    [SerializeField] Renderer groundRenderer;
    [SerializeField] Renderer[] fenceRenderers;

    // ---- Shader property ids (class-scope!) ----
    static readonly int PropBase = Shader.PropertyToID("_BaseColor");
    static readonly int PropStd = Shader.PropertyToID("_Color");
    static readonly int PropEmiss = Shader.PropertyToID("_EmissionColor");

    MaterialPropertyBlock _mpb;

    struct Theme
    {
        public Color sky, sunColor, ambientColor;
        public float sunIntensity;
        public Color ground, fence;
        public bool useFog;
        public Color fogColor;
        public float fogDensity;
    }

    // ---------- BALANCED THEMES ----------
    Theme Day() => new Theme
    {
        sky = new Color(0.60f, 0.75f, 0.90f),
        sunColor = new Color(1.00f, 0.95f, 0.88f),
        sunIntensity = 1.0f,
        ambientColor = new Color(0.75f, 0.82f, 0.90f),
        ground = new Color(0.70f, 0.83f, 0.65f),
        fence = new Color(0.45f, 0.28f, 0.17f),
        useFog = false,
        fogColor = new Color(0.70f, 0.80f, 0.90f),
        fogDensity = 0f,
    };

    Theme Sunset() => new Theme
    {
        sky = new Color(0.88f, 0.57f, 0.45f),
        sunColor = new Color(1.00f, 0.80f, 0.55f),
        sunIntensity = 0.9f,
        ambientColor = new Color(0.82f, 0.63f, 0.55f),
        ground = new Color(0.60f, 0.74f, 0.58f),
        fence = new Color(0.37f, 0.23f, 0.14f),
        useFog = true,
        fogColor = new Color(0.92f, 0.66f, 0.50f),
        fogDensity = 0.0035f,
    };

    Theme Night() => new Theme
    {
        sky = new Color(0.12f, 0.16f, 0.25f),
        sunColor = new Color(0.70f, 0.85f, 1.00f),
        sunIntensity = 0.55f,
        ambientColor = new Color(0.22f, 0.28f, 0.38f),
        ground = new Color(0.35f, 0.45f, 0.40f),
        fence = new Color(0.24f, 0.20f, 0.16f),
        useFog = true,
        fogColor = new Color(0.12f, 0.16f, 0.24f),
        fogDensity = 0.006f,
    };

    void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        // auto-find fences if none assigned
        if ((fenceRenderers == null || fenceRenderers.Length == 0))
            fenceRenderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    void Start()
    {
        // Prefer GameSettings, fallback to PlayerPrefs
        int levelId = (GameSettings.Level != null) ? GameSettings.LevelIndex
                                                   : PlayerPrefs.GetInt("LEVEL_ID", 0);

        Theme t = (levelId == 2) ? Night()
                 : (levelId == 1) ? Sunset()
                 : Day();

        ApplyTheme(t);
        Debug.Log($"[Theme] Applied {(levelId == 0 ? "Day" : levelId == 1 ? "Sunset" : "Night")} (id {levelId})");
    }

    // ---- helpers (class-scope) ----
    static Color WithA(Color c, float a) => new Color(c.r, c.g, c.b, a);

    static Color EnsureContrast(Color obj, Color background, float minDelta = 0.22f, float minV = 0.40f)
    {
        Color.RGBToHSV(obj, out var h1, out var s1, out var v1);
        Color.RGBToHSV(background, out var _, out var __, out var v2);

        if (Mathf.Abs(v1 - v2) < minDelta) v1 = Mathf.Clamp01(v2 + (v2 < 0.5f ? +minDelta : -minDelta));
        v1 = Mathf.Max(v1, minV);
        return Color.HSVToRGB(h1, s1, v1);
    }

    bool MatHas(Renderer r, int prop) => r && r.sharedMaterial && r.sharedMaterial.HasProperty(prop);

    void ApplyTheme(Theme t)
    {
        // Camera & light (scene-local)
        if (mainCam)
        {
            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = t.sky;
        }
        if (sunLight)
        {
            sunLight.color = t.sunColor;
            sunLight.intensity = t.sunIntensity;
        }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = t.ambientColor;

        RenderSettings.fog = t.useFog;
        if (t.useFog)
        {
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = t.fogColor;
            RenderSettings.fogDensity = t.fogDensity;
        }

        // Ground (MPB, full alpha)
        if (groundRenderer)
        {
            _mpb.Clear();
            var gCol = WithA(t.ground, 1f);
            if (MatHas(groundRenderer, PropBase)) _mpb.SetColor(PropBase, gCol);
            else if (MatHas(groundRenderer, PropStd)) _mpb.SetColor(PropStd, gCol);
            else Debug.LogWarning("[Theme] Ground material has no _BaseColor/_Color, skipping tint.");
            groundRenderer.SetPropertyBlock(_mpb);
        }

        // Fences (contrast + emission + robust fallback)
        if (fenceRenderers != null)
        {
            foreach (var r in fenceRenderers)
            {
                if (!r) continue;
                var sm = r.sharedMaterial;
                if (!sm) continue;

                var fenceCol = EnsureContrast(WithA(t.fence, 1f), t.ground);
                bool wroteMPB = false;

                _mpb.Clear();
                if (MatHas(r, PropBase)) { _mpb.SetColor(PropBase, fenceCol); wroteMPB = true; }
                else if (MatHas(r, PropStd)) { _mpb.SetColor(PropStd, fenceCol); wroteMPB = true; }

                // add a gentle emission in dark scenes (if shader supports it)
                if (sm.HasProperty(PropEmiss) && sunLight && sunLight.intensity < 0.8f)
                {
                    _mpb.SetColor(PropEmiss, fenceCol * 0.15f);
                }

                if (wroteMPB)
                {
                    r.SetPropertyBlock(_mpb);
                    Debug.Log($"[Theme] Fence '{r.name}' tinted via MPB.");
                }
                else
                {
                    // LAST RESORT: instantiate a unique material and set its .color
                    // (does not modify the original asset)
                    var instanced = r.material; // creates a copy
                    if (instanced.HasProperty(PropBase)) instanced.SetColor(PropBase, fenceCol);
                    else if (instanced.HasProperty(PropStd)) instanced.SetColor(PropStd, fenceCol);
                    else instanced.color = fenceCol;
                    Debug.LogWarning($"[Theme] Fence '{r.name}' had no color prop; applied instanced material color.");
                }
            }
        }
    }
}
