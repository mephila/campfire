using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace CozySize
{
    [BepInPlugin("mephila.cozysize", "cozysize", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        const float Min = 0.2f;
        const float Max = 15f;
        const float W = 280f;
        const float H = 196f;
        static readonly float[] Presets = { 0.25f, 0.5f, 1f, 2f, 5f, 10f };

        static readonly Color Back = new Color(0.10f, 0.11f, 0.13f, 0.96f);
        static readonly Color Bar = new Color(0.14f, 0.15f, 0.18f, 1f);
        static readonly Color Line = new Color(0.24f, 0.25f, 0.29f, 1f);
        static readonly Color Hover = new Color(0.30f, 0.31f, 0.36f, 1f);
        static readonly Color Accent = new Color(1f, 0.54f, 0.24f, 1f);
        static readonly Color Ink = new Color(0.92f, 0.92f, 0.94f, 1f);
        static readonly Color Dim = new Color(0.62f, 0.63f, 0.68f, 1f);

        static readonly BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        static readonly FieldInfo FSize = typeof(PlayerCharacterStatusEffects).GetField("<CharacterSizeMult>k__BackingField", Any);
        static readonly MethodInfo MRecalc = typeof(PlayerCharacterStatusEffects).GetMethod("RecalculateStatusEffects", Any);
        static readonly FieldInfo FAnimator = typeof(PlayerCharacterAnimation).GetField("mAnimator", Any);

        ConfigEntry<KeyCode> _key;
        ConfigEntry<bool> _on;
        ConfigEntry<float> _size;
        ConfigEntry<float> _anim;
        ConfigEntry<float> _x;
        ConfigEntry<float> _y;

        PlayerCharacterStatusEffects _sized;
        readonly Dictionary<Animator, float> _wrote = new Dictionary<Animator, float>();

        bool _open;
        bool _keyHeld;
        bool _locked;
        bool _forced;

        Vector2 _mouse;
        bool _down;
        bool _pressed;
        int _drag = -1;
        Vector2 _grab;

        Font _font;
        GUIStyle _left;
        GUIStyle _right;
        GUIStyle _center;
        float _styled = -1f;
        float _s = 1f;
        float _px;
        float _py;

        void Awake()
        {
            Config.SaveOnConfigSet = false;
            _key = Config.Bind("menu", "key", KeyCode.F6, "opens and closes the size window");
            _on = Config.Bind("size", "custom size", false, "uses the size below");
            _size = Config.Bind("size", "size", 3f, new ConfigDescription("player size", new AcceptableValueRange<float>(Min, Max)));
            _anim = Config.Bind("size", "keep animation speed", 1f, new ConfigDescription("scales walk animations with size", new AcceptableValueRange<float>(0f, 1f)));
            _x = Config.Bind("menu", "x", 40f, "window position");
            _y = Config.Bind("menu", "y", 200f, "window position");
            Config.Save();
            if (FSize == null) Logger.LogWarning("no CharacterSizeMult on PlayerCharacterStatusEffects");
            Logger.LogInfo("cozysize loaded, " + _key.Value + " opens it");
        }

        void Start()
        {
            StartCoroutine(AfterUpdates());
        }

        void Update()
        {
            try { ApplySize(); } catch (Exception e) { Logger.LogError(e); }
            if (_open) Hold();
            else Release();
        }

        IEnumerator AfterUpdates()
        {
            while (true)
            {
                yield return null;
                try { FixAnimations(); } catch { }
            }
        }

        void OnDestroy()
        {
            Release();
            Unsize();
        }

        static PlayerCharacter Local()
        {
            var p = PlayerTracker.MyPlayer;
            return p != null ? p.SpawnedCharacter : null;
        }

        void ApplySize()
        {
            var me = Local();
            var fx = me != null ? me.StatusEffects : null;
            if (_on.Value && fx != null && FSize != null)
            {
                if (_sized != fx) { Unsize(); _sized = fx; }
                float want = Mathf.Clamp(_size.Value, Min, Max);
                if ((float)FSize.GetValue(fx) != want) FSize.SetValue(fx, want);
            }
            else Unsize();
        }

        void Unsize()
        {
            var fx = _sized;
            _sized = null;
            if (fx == null || MRecalc == null) return;
            try { MRecalc.Invoke(fx, null); } catch { }
        }

        void FixAnimations()
        {
            float fix = _anim.Value;
            if (fix <= 0.001f || FAnimator == null) return;
            if (_wrote.Count > 64) _wrote.Clear();
            int hash = AnimationHashManager.MoveAnimSpeedMult;
            foreach (var p in PlayerTracker.Players)
            {
                if (p == null) continue;
                var c = p.SpawnedCharacter;
                if (c == null || c.Animation == null) continue;
                float s = c.CurrentModelScale;
                if (s <= 0.01f || Mathf.Abs(s - 1f) < 0.01f) continue;
                var a = FAnimator.GetValue(c.Animation) as Animator;
                if (a == null || !a.isActiveAndEnabled) continue;
                float v = a.GetFloat(hash);
                float last;
                if (_wrote.TryGetValue(a, out last) && Mathf.Approximately(last, v)) continue;
                float nv = v * Mathf.Pow(s, fix);
                a.SetFloat(hash, nv);
                _wrote[a] = nv;
            }
        }

        void Hold()
        {
            if (!_locked)
            {
                try { UIInputDisable.LockInput(); _locked = true; } catch { }
                try
                {
                    if (global::Singleton<InputManager>.InstanceFound())
                    {
                        global::Singleton<InputManager>.Instance.ForceCursorVisible(true);
                        _forced = true;
                    }
                }
                catch { }
            }
            if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
            if (!Cursor.visible) Cursor.visible = true;
        }

        void Release()
        {
            if (_locked) try { UIInputDisable.UnlockInput(); } catch { }
            _locked = false;
            if (_forced)
                try
                {
                    if (global::Singleton<InputManager>.InstanceFound())
                        global::Singleton<InputManager>.Instance.ForceCursorVisible(false);
                }
                catch { }
            _forced = false;
        }

        void OnGUI()
        {
            var e = Event.current;
            if (e == null) return;

            if (e.type == EventType.KeyDown && e.keyCode != KeyCode.None)
            {
                if (e.keyCode == _key.Value)
                {
                    if (!_keyHeld) Toggle();
                    _keyHeld = true;
                }
                else if (e.keyCode == KeyCode.Escape && _open) Toggle();
            }
            else if (e.type == EventType.KeyUp && e.keyCode == _key.Value) _keyHeld = false;

            if (e.isMouse || e.type == EventType.Repaint) _mouse = e.mousePosition;
            if (e.type == EventType.MouseDown && e.button == 0) { _down = true; _pressed = true; }
            if (e.type == EventType.MouseUp && e.button == 0) _down = false;

            if (e.type != EventType.Repaint) return;
            GUI.depth = -900;
            if (_open)
            {
                try { Draw(); } catch (Exception ex) { Logger.LogError(ex); _open = false; }
            }
            _pressed = false;
        }

        void Toggle()
        {
            _open = !_open;
            _drag = -1;
            _down = false;
            _pressed = false;
            if (!_open) Config.Save();
        }

        void Styles()
        {
            _s = Mathf.Max(1f, Screen.height / 1080f);
            if (Mathf.Approximately(_styled, _s) && _left != null) return;
            _styled = _s;
            if (_font == null)
            {
                try { _font = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Tahoma", "Arial", "DejaVu Sans", "Liberation Sans" }, 14); } catch { }
            }
            _left = new GUIStyle(GUI.skin.label);
            if (_font != null) _left.font = _font;
            _left.fontSize = Mathf.RoundToInt(13f * _s);
            _left.alignment = TextAnchor.MiddleLeft;
            _left.padding = new RectOffset(0, 0, 0, 0);
            _left.margin = new RectOffset(0, 0, 0, 0);
            _left.wordWrap = false;
            _left.clipping = TextClipping.Overflow;
            _left.normal.textColor = Ink;
            _right = new GUIStyle(_left) { alignment = TextAnchor.MiddleRight };
            _right.normal.textColor = Dim;
            _center = new GUIStyle(_left) { alignment = TextAnchor.MiddleCenter };
        }

        Rect R(float x, float y, float w, float h)
        {
            return new Rect(_px + x * _s, _py + y * _s, w * _s, h * _s);
        }

        static void Fill(Rect r, Color c)
        {
            var was = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = was;
        }

        bool Over(Rect r) { return r.Contains(_mouse); }

        void Draw()
        {
            Styles();
            float w = W * _s, h = H * _s;
            _px = Mathf.Clamp(_x.Value, 0f, Mathf.Max(0f, Screen.width - w));
            _py = Mathf.Clamp(_y.Value, 0f, Mathf.Max(0f, Screen.height - h));

            var title = R(0, 0, W, 30);
            if (_pressed && Over(title)) { _drag = 0; _grab = _mouse - new Vector2(_px, _py); }
            if (_drag == 0)
            {
                if (_down)
                {
                    _px = Mathf.Clamp(_mouse.x - _grab.x, 0f, Mathf.Max(0f, Screen.width - w));
                    _py = Mathf.Clamp(_mouse.y - _grab.y, 0f, Mathf.Max(0f, Screen.height - h));
                    _x.Value = _px;
                    _y.Value = _py;
                }
                else { _drag = -1; Config.Save(); }
            }

            Fill(new Rect(_px - 1, _py - 1, w + 2, h + 2), Line);
            Fill(new Rect(_px, _py, w, h), Back);
            Fill(title, Bar);
            Fill(R(0, 0, W, 2), Accent);
            GUI.Label(R(12, 2, W - 24, 28), "cozysize", _left);

            var row = R(12, 40, W - 24, 22);
            if (Over(row)) Fill(R(6, 40, W - 12, 22), Bar);
            var box = R(12, 43, 16, 16);
            Fill(box, Line);
            if (_on.Value) Fill(R(15, 46, 10, 10), Accent);
            GUI.Label(R(36, 40, W - 48, 22), "custom size", _left);
            if (_pressed && Over(row)) { _on.Value = !_on.Value; Config.Save(); }

            _size.Value = Slider(1, 72, "size", _size.Value, ToT(_size.Value), FromT, _size.Value.ToString("0.0#") + "x");

            float bw = (W - 24 - 5 * 4) / Presets.Length;
            for (int i = 0; i < Presets.Length; i++)
            {
                var b = R(12 + i * (bw + 4), 118, bw, 22);
                bool active = _on.Value && Mathf.Abs(_size.Value - Presets[i]) < 0.001f;
                Fill(b, active ? Accent : Over(b) ? Hover : Line);
                GUI.Label(b, Presets[i].ToString("0.##") + "x", _center);
                if (_pressed && Over(b)) { _size.Value = Presets[i]; _on.Value = true; Config.Save(); }
            }

            _anim.Value = Slider(2, 150, "keep animation speed", _anim.Value, _anim.Value, t => t, (_anim.Value * 100f).ToString("0") + "%");
        }

        static float ToT(float v) { return Mathf.Log(Mathf.Clamp(v, Min, Max) / Min) / Mathf.Log(Max / Min); }

        static float FromT(float t)
        {
            float v = Min * Mathf.Pow(Max / Min, Mathf.Clamp01(t));
            return v < 1f ? Mathf.Round(v * 100f) / 100f : Mathf.Round(v * 20f) / 20f;
        }

        float Slider(int id, float y, string label, float value, float t, Func<float, float> fromT, string shown)
        {
            GUI.Label(R(12, y, W - 24, 20), label, _left);
            GUI.Label(R(12, y, W - 24, 20), shown, _right);
            var track = R(12, y + 26, W - 24, 6);
            var hit = R(6, y + 18, W - 12, 22);
            if (_pressed && Over(hit)) _drag = id;
            if (_drag == id)
            {
                if (_down) t = Mathf.Clamp01((_mouse.x - track.x) / track.width);
                else { _drag = -1; Config.Save(); }
            }
            Fill(track, Line);
            Fill(new Rect(track.x, track.y, track.width * t, track.height), Accent);
            float kx = track.x + track.width * t;
            Fill(new Rect(kx - 5 * _s, track.y - 4 * _s, 10 * _s, 14 * _s), Ink);
            return _drag == id ? fromT(t) : value;
        }
    }
}
