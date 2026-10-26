using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Jatify.FpvDrone
{
    // An FPV racing drone you launch from your character and fly with a gamepad (PS4 DualShock or any
    // pad), Mode 2: R2 throttle, left stick yaw, right stick pitch/roll. Acro (rate) mode by default,
    // Angle (self-levelling) mode on Triangle. Client-side camera only: nothing exists for other players.
    [BepInPlugin(Guid, "FpvDrone", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "jatify.fpvdrone";
        internal static ConfigEntry<KeyCode> Key, SettingsKey;
        internal static ConfigEntry<float> MaxRate, Expo, ThrustG, Drag, CamTilt, Fov, Range, BatteryMin, Deadzone, AngleMax;
        internal static ConfigEntry<bool> HideHud, InvertPitch;

        private void Awake()
        {
            Log.Bind(Logger);
            Key = Config.Bind("Controls", "LaunchKey", KeyCode.Home, "Keyboard key to launch / land the drone (Valheim uses every gamepad button, so launching is keyboard only).");
            SettingsKey = Config.Bind("Controls", "SettingsKey", KeyCode.End, "Opens the drone settings panel (sliders for sensitivity, thrust, camera).");
            Deadzone = Config.Bind("Controls", "Deadzone", 0.06f, "Stick deadzone.");
            InvertPitch = Config.Bind("Controls", "InvertPitch", false, "Pull back to dive instead of push forward.");
            MaxRate = Config.Bind("Rates", "MaxRate", 670f, "Acro: rotation rate at full stick, degrees per second.");
            Expo = Config.Bind("Rates", "Expo", 0.45f, "0 = linear sticks, 1 = very soft around the centre.");
            AngleMax = Config.Bind("Rates", "AngleModeMaxTilt", 45f, "Angle mode: tilt at full stick, degrees.");
            ThrustG = Config.Bind("Physics", "ThrustG", 4.0f, "Thrust at full throttle, in g (1 = just hovers).");
            Drag = Config.Bind("Physics", "Drag", 0.011f, "Air drag (higher = lower top speed).");
            CamTilt = Config.Bind("Camera", "UptiltDegrees", 25f, "Camera tilted up from the frame, like a real FPV quad.");
            Fov = Config.Bind("Camera", "Fov", 105f, "Field of view while flying.");
            HideHud = Config.Bind("Camera", "HideHud", true, "Hide the game HUD while flying.");
            Range = Config.Bind("Flight", "RangeMeters", 230f, "Signal range from your character (the world only streams around you).");
            BatteryMin = Config.Bind("Flight", "BatteryMinutes", 5f, "Flight time per launch.");
            var harmony = new Harmony(Guid);
            foreach (var t in typeof(Plugin).Assembly.GetTypes())
                try { harmony.CreateClassProcessor(t).Patch(); }
                catch (System.Exception e) { Log.Warn($"patch {t.Name} failed: {e.GetBaseException().Message}"); }
            Log.Info("ready: press " + Key.Value + " to fly");
        }

        private void Update() => Drone.Tick(Time.deltaTime);
    }

    internal static class Drone
    {
        internal static bool Flying;
        private static Vector3 _pos, _vel;
        private static Quaternion _rot;
        private static bool _angleMode, _crashed;
        private static float _battery, _crashTimer, _throttle, _savedFov = -1f;
        private static string _crashText;
        private const float Radius = 0.18f;
        private static int _mask;

        internal static Vector3 CamPos => _pos;
        internal static Quaternion CamRot => _rot * Quaternion.Euler(-Plugin.CamTilt.Value, 0f, 0f);

        internal static void Tick(float dt)
        {
            var p = Player.m_localPlayer;
            if (p == null) { if (Flying) Land(); return; }
            var pad = Gamepad.current;
            bool typing = Console.IsVisible() || (Chat.instance != null && Chat.instance.HasFocus());
            if (!typing && Input.GetKeyDown(Plugin.SettingsKey.Value)) Settings.Toggle();
            if (Settings.Open)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) Settings.Close();
                return;   // the drone holds still while you tune it
            }

            // launch / land: the keyboard key only (every gamepad button is bound by Valheim)
            bool toggle = Input.GetKeyDown(Plugin.Key.Value);
            if (toggle && !Console.IsVisible() && !(Chat.instance != null && Chat.instance.HasFocus())) { if (Flying) Land(); else Launch(p); }
            if (!Flying) return;

            if (Input.GetKeyDown(KeyCode.Escape)) { Land(); return; }
            if (pad != null && pad.buttonEast.wasPressedThisFrame) { Reset(p, "RESET"); return; }          // Circle
            if (pad != null && pad.buttonNorth.wasPressedThisFrame) _angleMode = !_angleMode;                // Triangle

            if (_crashed)
            {
                _crashTimer -= dt;
                if (_crashTimer <= 0f) Reset(p, null);
                Osd.Paint(_pos, _vel, p, _throttle, _battery, _angleMode, _crashText, pad != null);
                return;
            }

            // ---- sticks (Mode 2) ----
            float thr, yaw, pitch, roll;
            if (pad != null)
            {
                thr = pad.rightTrigger.ReadValue();
                yaw = Dz(pad.leftStick.x.ReadValue());
                pitch = Dz(pad.rightStick.y.ReadValue());
                roll = Dz(pad.rightStick.x.ReadValue());
            }
            else   // keyboard fallback: Space throttle, W/S pitch, A/D roll, Q/E yaw
            {
                thr = Input.GetKey(KeyCode.Space) ? 1f : Input.GetKey(KeyCode.LeftShift) ? 0.35f : 0f;
                pitch = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
                roll = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
                yaw = (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f);
            }
            if (Plugin.InvertPitch.Value) pitch = -pitch;
            _throttle = thr;

            // ---- integrate in small steps (frame drops must not tunnel through terrain) ----
            int steps = Mathf.Clamp(Mathf.CeilToInt(dt / 0.004f), 1, 20);
            float h = dt / steps;
            for (int i = 0; i < steps && !_crashed; i++) Step(h, thr, yaw, pitch, roll, p);

            _battery -= dt;
            if (_battery <= 0f) Crash("BATTERY EMPTY");
            if (Vector3.Distance(_pos, p.transform.position) > Plugin.Range.Value) Crash("SIGNAL LOST");
            Osd.Paint(_pos, _vel, p, thr, _battery, _angleMode, _crashed ? _crashText : null, pad != null);
        }

        private static void Step(float h, float thr, float yaw, float pitch, float roll, Player p)
        {
            if (_angleMode)
            {
                // self-levelling: sticks set a tilt; yaw stays a rate
                var fwd = Vector3.ProjectOnPlane(_rot * Vector3.forward, Vector3.up);
                if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.ProjectOnPlane(_rot * Vector3.up, Vector3.up) * -1f;
                var heading = Quaternion.LookRotation(fwd.normalized, Vector3.up) * Quaternion.Euler(0f, Rate(yaw) * h, 0f);
                var target = heading * Quaternion.Euler(-pitch * Plugin.AngleMax.Value, 0f, -roll * Plugin.AngleMax.Value);
                _rot = Quaternion.Slerp(_rot, target, 1f - Mathf.Exp(-10f * h));
            }
            else
            {
                // acro: sticks are rotation rates about the drone's own axes
                _rot = _rot * Quaternion.Euler(-Rate(pitch) * h, Rate(yaw) * h, -Rate(roll) * h);
            }
            _rot = Quaternion.Normalize(_rot);

            var acc = _rot * Vector3.up * (Plugin.ThrustG.Value * 9.81f * thr) + Vector3.down * 9.81f;
            acc -= _vel * _vel.magnitude * Plugin.Drag.Value;
            _vel += acc * h;
            var move = _vel * h;

            float dist = move.magnitude;
            if (dist > 1e-5f && Physics.SphereCast(_pos, Radius, move / dist, out var hit, dist, _mask, QueryTriggerInteraction.Ignore))
            {
                if (_vel.magnitude > 7f) { _pos += move.normalized * Mathf.Max(0f, hit.distance - 0.02f); Crash("CRASHED"); return; }
                // a soft touch: stop against the surface and slide along it
                _pos += move.normalized * Mathf.Max(0f, hit.distance - 0.02f) + hit.normal * 0.01f;
                _vel = Vector3.ProjectOnPlane(_vel, hit.normal) * 0.6f;
                return;
            }
            _pos += move;
            float water = ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : 30f;
            if (_pos.y < water - 0.1f) Crash("IN THE WATER");
        }

        private static float Dz(float v) { float d = Plugin.Deadzone.Value; return Mathf.Abs(v) < d ? 0f : Mathf.Sign(v) * (Mathf.Abs(v) - d) / (1f - d); }

        private static float Rate(float s)
        {
            float e = Mathf.Clamp01(Plugin.Expo.Value);
            return Plugin.MaxRate.Value * (e * s * s * s + (1f - e) * s);
        }

        private static void Launch(Player p)
        {
            if (_mask == 0) _mask = LayerMask.GetMask("Default", "static_solid", "terrain", "piece", "Default_small", "vehicle", "character", "character_net", "tree", "rock");
            Flying = true; _angleMode = false;
            if (Plugin.HideHud.Value && Hud.instance != null) Hud.instance.m_rootObject.SetActive(false);
            Reset(p, null);
            Osd.Show(true);
            Log.Info("drone launched");
        }

        internal static void Land()
        {
            Flying = false; _crashed = false;
            if (Hud.instance != null) Hud.instance.m_rootObject.SetActive(true);
            var cam = GameCamera.instance != null ? GameCamera.instance.GetComponent<Camera>() : null;
            if (cam != null && _savedFov > 0f) cam.fieldOfView = _savedFov;
            _savedFov = -1f;
            Osd.Show(false);
            Log.Info("drone landed");
        }

        // back next to the player, level, facing where the player faces
        private static void Reset(Player p, string why)
        {
            var f = Vector3.ProjectOnPlane(p.transform.forward, Vector3.up).normalized;
            _pos = p.transform.position + Vector3.up * 1.8f + f * 1.2f;
            _rot = Quaternion.LookRotation(f, Vector3.up);
            _vel = Vector3.zero; _crashed = false;
            _battery = Plugin.BatteryMin.Value * 60f;
            if (why != null) Log.Info(why);
        }

        private static void Crash(string why)
        {
            if (_crashed) return;
            _crashed = true; _crashText = why; _crashTimer = 1.6f; _vel = Vector3.zero;
        }

        internal static void ApplyCamera(Camera cam)
        {
            if (_savedFov < 0f) _savedFov = cam.fieldOfView;
            cam.transform.SetPositionAndRotation(CamPos, CamRot);
            cam.fieldOfView = Plugin.Fov.Value;
        }
    }

    // The camera follows the drone (after Valheim has placed it).
    [HarmonyPatch(typeof(GameCamera), "LateUpdate")]
    internal static class CameraFollowsDrone
    {
        private static void Postfix(GameCamera __instance)
        {
            if (!Drone.Flying) return;
            var cam = __instance.GetComponent<Camera>();
            if (cam != null) Drone.ApplyCamera(cam); else __instance.transform.SetPositionAndRotation(Drone.CamPos, Drone.CamRot);
        }
    }

    // While flying your character stands still and ignores input.
    [HarmonyPatch(typeof(Player), "TakeInput")]
    internal static class PlayerStill
    {
        private static void Postfix(ref bool __result) { if (Drone.Flying) __result = false; }
    }

    // ...and Valheim ignores the gamepad (so sticks and buttons don't open menus or attack).
    [HarmonyPatch]
    internal static class MuteValheimGamepad
    {
        private static IEnumerable<MethodBase> TargetMethods() =>
            AccessTools.GetDeclaredMethods(typeof(ZInput)).Where(m => m.IsStatic && m.ReturnType == typeof(bool) && !m.IsGenericMethod
                && (m.Name.StartsWith("GetButton") || m.Name.StartsWith("GetJoy")));

        private static bool Prefix(MethodBase __originalMethod, object[] __args, ref bool __result)
        {
            if (!Drone.Flying) return true;
            if (__args.Length > 0 && __args[0] is string s && s.Contains("Menu")) return true;   // Esc / menu still work
            __result = false;
            return false;
        }
    }

    [HarmonyPatch]
    internal static class MuteValheimSticks
    {
        private static IEnumerable<MethodBase> TargetMethods() =>
            AccessTools.GetDeclaredMethods(typeof(ZInput)).Where(m => m.IsStatic && m.ReturnType == typeof(float) && !m.IsGenericMethod && m.Name.StartsWith("GetJoy"));

        private static bool Prefix(ref float __result)
        {
            if (!Drone.Flying) return true;
            __result = 0f;
            return false;
        }
    }

    // On-screen display, FPV goggles style: speed, altitude, distance, throttle, battery, mode.
    internal static class Osd
    {
        private static GameObject _go;
        private static Text _left, _center, _right;
        private static Image _thrBar;

        internal static void Show(bool on)
        {
            if (on && _go == null) Build();
            if (_go != null) _go.SetActive(on);
        }

        private static void Build()
        {
            _go = new GameObject("FpvDroneOsd", typeof(Canvas), typeof(CanvasScaler));
            var c = _go.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 30000;
            var s = _go.GetComponent<CanvasScaler>(); s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; s.referenceResolution = new Vector2(1920, 1080); s.matchWidthOrHeight = 1f;
            Object.DontDestroyOnLoad(_go);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _left = MakeText(font, new Vector2(0f, 0f), new Vector2(28f, 26f), TextAnchor.LowerLeft, 26);
            _right = MakeText(font, new Vector2(1f, 0f), new Vector2(-28f, 26f), TextAnchor.LowerRight, 26);
            _center = MakeText(font, new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), TextAnchor.MiddleCenter, 44);
            // crosshair
            var x = new GameObject("cross", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            x.transform.SetParent(_go.transform, false); x.font = font; x.fontSize = 34; x.alignment = TextAnchor.MiddleCenter; x.text = "—+—"; x.color = new Color(1f, 1f, 1f, 0.75f);
            ((RectTransform)x.transform).sizeDelta = new Vector2(200f, 60f);
            // throttle bar (left edge)
            var bg = new GameObject("thrbg", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            bg.transform.SetParent(_go.transform, false); bg.color = new Color(0f, 0f, 0f, 0.45f);
            var br = (RectTransform)bg.transform; br.anchorMin = br.anchorMax = br.pivot = new Vector2(0f, 0.5f); br.anchoredPosition = new Vector2(30f, 0f); br.sizeDelta = new Vector2(14f, 260f);
            _thrBar = new GameObject("thr", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            _thrBar.transform.SetParent(bg.transform, false); _thrBar.color = new Color(1f, 1f, 1f, 0.85f);
            var tr = (RectTransform)_thrBar.transform; tr.anchorMin = new Vector2(0f, 0f); tr.anchorMax = new Vector2(1f, 0f); tr.pivot = new Vector2(0.5f, 0f); tr.anchoredPosition = Vector2.zero; tr.sizeDelta = new Vector2(0f, 0f);
        }

        private static Text MakeText(Font font, Vector2 anchor, Vector2 pos, TextAnchor align, int size)
        {
            var t = new GameObject("t", typeof(RectTransform), typeof(Text), typeof(Outline)).GetComponent<Text>();
            t.transform.SetParent(_go.transform, false); t.font = font; t.fontSize = size; t.alignment = align; t.color = Color.white;
            t.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.85f);
            var r = (RectTransform)t.transform; r.anchorMin = r.anchorMax = r.pivot = anchor; r.anchoredPosition = pos; r.sizeDelta = new Vector2(900f, 120f);
            return t;
        }

        internal static void Paint(Vector3 pos, Vector3 vel, Player p, float thr, float battery, bool angle, string alert, bool pad)
        {
            if (_go == null) return;
            float ground = ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(pos, out var gy) ? gy : pos.y;
            float dist = Vector3.Distance(pos, p.transform.position);
            int mins = Mathf.Max(0, (int)(battery / 60f)), secs = Mathf.Max(0, (int)(battery % 60f));
            _left.text = $"{vel.magnitude * 3.6f:0} km/h    ALT {pos.y - ground:0} m    DIST {dist:0} m";
            _right.text = $"{(angle ? "ANGLE" : "ACRO")}    THR {thr * 100f:0}%    BAT {mins}:{secs:00}" + (pad ? "" : "\nno gamepad: Space / Shift throttle, WASD, Q/E");
            _center.text = alert ?? (dist > Plugin.Range.Value * 0.85f ? "LOW SIGNAL" : battery < 30f ? "LOW BATTERY" : "");
            _center.color = alert != null ? new Color(1f, 0.35f, 0.3f) : new Color(1f, 0.85f, 0.3f);
            ((RectTransform)_thrBar.transform).sizeDelta = new Vector2(0f, 260f * thr);
        }
    }
}
