using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Jatify.FpvDrone
{
    // In-game tuning panel (End): sliders for the values that decide how the drone feels, three presets,
    // and a reset. Changes apply at once and are saved to the config file. The drone holds still while
    // the panel is open.
    internal static class Settings
    {
        private static GameObject _go;
        private static readonly List<Action> _refresh = new List<Action>();
        internal static bool Open => _go != null && _go.activeSelf;

        internal static void Toggle()
        {
            if (_go == null) Build();
            _go.SetActive(!_go.activeSelf);
            foreach (var r in _refresh) r();
        }

        internal static void Close() { if (_go != null) _go.SetActive(false); }

        private static readonly Color Bg = new Color(0.07f, 0.065f, 0.06f, 0.94f), Line = new Color(0.85f, 0.7f, 0.43f, 0.45f),
            Gold = new Color(0.9f, 0.78f, 0.49f), Text = new Color(0.94f, 0.9f, 0.83f), Dim = new Color(0.66f, 0.61f, 0.53f);
        private static Font _font;

        private static void Build()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _go = new GameObject("FpvDroneSettings", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var c = _go.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 30001;
            var s = _go.GetComponent<CanvasScaler>(); s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; s.referenceResolution = new Vector2(1920, 1080); s.matchWidthOrHeight = 1f;
            UnityEngine.Object.DontDestroyOnLoad(_go);

            var panel = Box(_go.transform, "panel", Bg, new Vector2(620f, 0f));
            var prt = (RectTransform)panel.transform; prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
            var outline = panel.gameObject.AddComponent<Outline>(); outline.effectColor = Line; outline.effectDistance = new Vector2(1.5f, -1.5f);

            float y = -24f;
            Label(panel.transform, "Drone settings", 30, Gold, new Vector2(28f, y), TextAnchor.UpperLeft, 560f); y -= 46f;
            Label(panel.transform, "End: close   ·   changes apply now and are saved", 17, Dim, new Vector2(28f, y), TextAnchor.UpperLeft, 560f); y -= 40f;

            // presets
            float x = 28f;
            foreach (var (name, rate, expo, thrust) in new[] { ("Beginner", 300f, 0.6f, 3.0f), ("Normal", 480f, 0.5f, 3.6f), ("Racer", 670f, 0.45f, 4.5f) })
            {
                Button(panel.transform, name, new Vector2(x, y), 170f, () =>
                {
                    Plugin.MaxRate.Value = rate; Plugin.Expo.Value = expo; Plugin.ThrustG.Value = thrust;
                    foreach (var r in _refresh) r();
                });
                x += 184f;
            }
            y -= 64f;

            y = Row(panel.transform, "Rotation speed", "how fast it flips and turns at full stick (deg/s)", Plugin.MaxRate, 150f, 1000f, "0", y);
            y = Row(panel.transform, "Expo", "softer around the stick centre (0 = linear)", Plugin.Expo, 0f, 0.9f, "0.00", y);
            y = Row(panel.transform, "Angle mode tilt", "how far it leans in Angle mode (degrees)", Plugin.AngleMax, 15f, 70f, "0", y);
            y = Row(panel.transform, "Thrust", "power at full throttle (1 = just hovers)", Plugin.ThrustG, 1.5f, 8f, "0.0", y);
            y = Row(panel.transform, "Air drag", "higher = slower top speed", Plugin.Drag, 0.003f, 0.03f, "0.000", y);
            y = Row(panel.transform, "Stick deadzone", "ignore small stick movement", Plugin.Deadzone, 0f, 0.25f, "0.00", y);
            y = Row(panel.transform, "Camera uptilt", "camera tilted up from the frame (degrees)", Plugin.CamTilt, 0f, 50f, "0", y);
            y = Row(panel.transform, "Field of view", "", Plugin.Fov, 70f, 130f, "0", y);
            y -= 8f;
            Button(panel.transform, "Reset all to defaults", new Vector2(28f, y), 260f, () =>
            {
                foreach (var e in new ConfigEntryBase[] { Plugin.MaxRate, Plugin.Expo, Plugin.AngleMax, Plugin.ThrustG, Plugin.Drag, Plugin.Deadzone, Plugin.CamTilt, Plugin.Fov })
                    e.BoxedValue = e.DefaultValue;
                foreach (var r in _refresh) r();
            });
            y -= 70f;
            prt.sizeDelta = new Vector2(620f, -y);
            _go.SetActive(false);
        }

        private static float Row(Transform parent, string name, string hint, ConfigEntry<float> entry, float min, float max, string fmt, float y)
        {
            Label(parent, name, 21, Text, new Vector2(28f, y), TextAnchor.UpperLeft, 300f);
            var value = Label(parent, "", 21, Gold, new Vector2(470f, y), TextAnchor.UpperRight, 120f);
            if (hint.Length > 0) Label(parent, hint, 15, Dim, new Vector2(28f, y - 26f), TextAnchor.UpperLeft, 560f);
            var slider = Slider(parent, new Vector2(28f, y - (hint.Length > 0 ? 52f : 30f)), 562f, min, max);
            slider.onValueChanged.AddListener(v => { entry.Value = v; value.text = v.ToString(fmt); });
            _refresh.Add(() => { slider.SetValueWithoutNotify(entry.Value); value.text = entry.Value.ToString(fmt); });
            return y - (hint.Length > 0 ? 84f : 62f);
        }

        // ---- plain UGUI parts ---------------------------------------------------------------------

        private static Image Box(Transform parent, string name, Color color, Vector2 size)
        {
            var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(parent, false); img.color = color;
            ((RectTransform)img.transform).sizeDelta = size;
            return img;
        }

        private static RectTransform TopLeft(Component c, Vector2 pos, Vector2 size)
        {
            var r = (RectTransform)c.transform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = pos; r.sizeDelta = size; return r;
        }

        private static Text Label(Transform parent, string text, int size, Color color, Vector2 pos, TextAnchor align, float width)
        {
            var t = new GameObject("t", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            t.transform.SetParent(parent, false); t.font = _font; t.fontSize = size; t.color = color; t.alignment = align; t.text = text;
            t.raycastTarget = false;
            TopLeft(t, pos, new Vector2(width, size + 10f));
            return t;
        }

        private static void Button(Transform parent, string label, Vector2 pos, float width, Action click)
        {
            var img = Box(parent, "btn", new Color(0.24f, 0.2f, 0.16f, 1f), Vector2.zero);
            TopLeft(img, pos, new Vector2(width, 44f));
            img.gameObject.AddComponent<Outline>().effectColor = Line;
            var b = img.gameObject.AddComponent<UnityEngine.UI.Button>();
            var cols = b.colors; cols.highlightedColor = new Color(1.25f, 1.15f, 1f); cols.pressedColor = new Color(0.8f, 0.75f, 0.7f); b.colors = cols;
            b.onClick.AddListener(() => click());
            var t = Label(img.transform, label, 20, Text, Vector2.zero, TextAnchor.MiddleCenter, width);
            var tr = (RectTransform)t.transform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.pivot = new Vector2(0.5f, 0.5f); tr.offsetMin = tr.offsetMax = Vector2.zero;
        }

        private static UnityEngine.UI.Slider Slider(Transform parent, Vector2 pos, float width, float min, float max)
        {
            var root = new GameObject("slider", typeof(RectTransform)); root.transform.SetParent(parent, false);
            var rr = (RectTransform)root.transform; rr.anchorMin = rr.anchorMax = rr.pivot = new Vector2(0f, 1f); rr.anchoredPosition = pos; rr.sizeDelta = new Vector2(width, 24f);
            var track = Box(root.transform, "track", new Color(0f, 0f, 0f, 0.55f), Vector2.zero);
            var tr = (RectTransform)track.transform; tr.anchorMin = new Vector2(0f, 0.5f); tr.anchorMax = new Vector2(1f, 0.5f); tr.sizeDelta = new Vector2(0f, 8f);
            var area = new GameObject("fillArea", typeof(RectTransform)); area.transform.SetParent(root.transform, false);
            var ar = (RectTransform)area.transform; ar.anchorMin = new Vector2(0f, 0.5f); ar.anchorMax = new Vector2(1f, 0.5f); ar.sizeDelta = new Vector2(0f, 8f);
            var fill = Box(area.transform, "fill", Gold, Vector2.zero);
            var fr = (RectTransform)fill.transform; fr.anchorMin = Vector2.zero; fr.anchorMax = new Vector2(0f, 1f); fr.sizeDelta = Vector2.zero;
            var handleArea = new GameObject("handleArea", typeof(RectTransform)); handleArea.transform.SetParent(root.transform, false);
            var hr = (RectTransform)handleArea.transform; hr.anchorMin = Vector2.zero; hr.anchorMax = Vector2.one; hr.sizeDelta = new Vector2(-20f, 0f);
            var handle = Box(handleArea.transform, "handle", new Color(0.96f, 0.86f, 0.6f), new Vector2(20f, 0f));
            var hh = (RectTransform)handle.transform; hh.anchorMin = new Vector2(0f, 0f); hh.anchorMax = new Vector2(0f, 1f);
            var s = root.AddComponent<UnityEngine.UI.Slider>();
            s.fillRect = fr; s.handleRect = hh; s.targetGraphic = handle; s.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            s.minValue = min; s.maxValue = max;
            return s;
        }
    }

    // While the panel is open: free cursor, no player or camera input.
    [HarmonyPatch(typeof(GameCamera), nameof(GameCamera.UpdateMouseCapture))]
    internal static class SettingsCursor
    {
        private static bool Prefix()
        {
            if (!Settings.Open) return true;
            ZCursor.LockState = CursorLockMode.None;
            ZCursor.Show();
            return false;
        }
    }

    [HarmonyPatch(typeof(Menu), nameof(Menu.IsVisible))]
    internal static class SettingsIsAMenu
    {
        private static void Postfix(ref bool __result) { if (Settings.Open) __result = true; }
    }
}
