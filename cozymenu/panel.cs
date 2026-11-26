using System;
using UnityEngine;

namespace CozyMenu
{
    public class Panel
    {
        public const float Width = 280f;
        public const float TitleHeight = 30f;
        const float Pad = 12f;

        static readonly Color Back = new Color(0.10f, 0.11f, 0.13f, 0.96f);
        static readonly Color Bar = new Color(0.14f, 0.15f, 0.18f, 1f);
        static readonly Color Line = new Color(0.24f, 0.25f, 0.29f, 1f);
        static readonly Color Hover = new Color(0.30f, 0.31f, 0.36f, 1f);
        static readonly Color Accent = new Color(1f, 0.54f, 0.24f, 1f);
        static readonly Color Ink = new Color(0.92f, 0.92f, 0.94f, 1f);
        static readonly Color Dim = new Color(0.62f, 0.63f, 0.68f, 1f);

        public float Scale { get; private set; } = 1f;
        public float Used { get { return (_cy - _wy) / Scale; } }

        Vector2 _mouse;
        bool _down;
        bool _pressed;
        int _drag = -1;
        int _id;
        Vector2 _grab;

        float _wx, _wy, _cy;

        Font _font;
        GUIStyle _left, _right, _center, _dim;
        float _styled = -1f;

        internal void Feed(Event e)
        {
            if (e.isMouse || e.type == EventType.Repaint) _mouse = e.mousePosition;
            if (e.type == EventType.MouseDown && e.button == 0) { _down = true; _pressed = true; }
            if (e.type == EventType.MouseUp && e.button == 0) _down = false;
        }

        internal void EndFrame() { _pressed = false; }

        internal void Reset()
        {
            _drag = -1;
            _down = false;
            _pressed = false;
        }

        internal void Begin()
        {
            _id = 0;
            Scale = Mathf.Max(1f, Screen.height / 1080f);
            if (Mathf.Approximately(_styled, Scale) && _left != null) return;
            _styled = Scale;
            if (_font == null)
            {
                try { _font = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Tahoma", "Arial", "DejaVu Sans", "Liberation Sans" }, 14); } catch { }
            }
            _left = new GUIStyle(GUI.skin.label);
            if (_font != null) _left.font = _font;
            _left.fontSize = Mathf.RoundToInt(13f * Scale);
            _left.alignment = TextAnchor.MiddleLeft;
            _left.padding = new RectOffset(0, 0, 0, 0);
            _left.margin = new RectOffset(0, 0, 0, 0);
            _left.wordWrap = false;
            _left.clipping = TextClipping.Overflow;
            _left.normal.textColor = Ink;
            _right = new GUIStyle(_left) { alignment = TextAnchor.MiddleRight };
            _right.normal.textColor = Dim;
            _center = new GUIStyle(_left) { alignment = TextAnchor.MiddleCenter };
            _dim = new GUIStyle(_left);
            _dim.normal.textColor = Dim;
        }

        internal bool DragWindow(Rect title, ref float x, ref float y, float w, float h)
        {
            if (_pressed && title.Contains(_mouse)) { _drag = -2; _grab = _mouse - new Vector2(x, y); }
            if (_drag != -2) return false;
            if (!_down) { _drag = -1; return false; }
            x = Mathf.Clamp(_mouse.x - _grab.x, 0f, Mathf.Max(0f, Screen.width - w));
            y = Mathf.Clamp(_mouse.y - _grab.y, 0f, Mathf.Max(0f, Screen.height - h));
            return true;
        }

        internal void Frame(Rect r, string title)
        {
            Fill(new Rect(r.x - 1, r.y - 1, r.width + 2, r.height + 2), Line);
            Fill(r, Back);
            Fill(new Rect(r.x, r.y, r.width, TitleHeight * Scale), Bar);
            Fill(new Rect(r.x, r.y, r.width, 2 * Scale), Accent);
            GUI.Label(new Rect(r.x + Pad * Scale, r.y + 2 * Scale, r.width - Pad * 2 * Scale, (TitleHeight - 2) * Scale), title, _left);
            _wx = r.x;
            _wy = r.y;
        }

        internal void At(float x, float y)
        {
            _wx = x;
            _cy = y;
        }

        Rect Row(float h)
        {
            var r = new Rect(_wx + Pad * Scale, _cy, (Width - Pad * 2) * Scale, h * Scale);
            _cy += h * Scale;
            return r;
        }

        Rect Wide(Rect r)
        {
            float g = 6f * Scale;
            return new Rect(r.x - g, r.y, r.width + g * 2, r.height);
        }

        static void Fill(Rect r, Color c)
        {
            var was = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = was;
        }

        bool Over(Rect r) { return r.Contains(_mouse); }

        bool Clicked(Rect r) { return _pressed && _drag == -1 && Over(r); }

        public void Gap(float h)
        {
            _cy += h * Scale;
        }

        public void Note(string text)
        {
            GUI.Label(Row(20), text, _dim);
        }

        internal bool Header(string title, bool folded)
        {
            var r = Row(24);
            Fill(new Rect(r.x, r.y + r.height - 1 * Scale, r.width, 1 * Scale), Line);
            GUI.Label(r, title, _left);
            GUI.Label(r, folded ? "+" : "-", _right);
            Gap(4f);
            return Clicked(Wide(r));
        }

        public bool Toggle(string label, bool value)
        {
            var r = Row(24);
            var hit = Wide(r);
            if (Over(hit)) Fill(hit, Bar);
            float b = 16f * Scale;
            var box = new Rect(r.x, r.y + (r.height - b) / 2f, b, b);
            Fill(box, Line);
            if (value) Fill(new Rect(box.x + 3 * Scale, box.y + 3 * Scale, b - 6 * Scale, b - 6 * Scale), Accent);
            GUI.Label(new Rect(r.x + b + 8 * Scale, r.y, r.width - b - 8 * Scale, r.height), label, _left);
            return Clicked(hit) ? !value : value;
        }

        public bool Button(string label)
        {
            var r = Row(24);
            r.height -= 2 * Scale;
            Fill(r, Over(r) ? Hover : Line);
            GUI.Label(r, label, _center);
            Gap(4f);
            return Clicked(r);
        }

        public int Buttons(string[] labels, int active)
        {
            int clicked = -1;
            float g = 4f * Scale;
            float full = (Width - Pad * 2) * Scale;
            var need = new float[labels.Length];
            for (int i = 0; i < labels.Length; i++)
                need[i] = _center.CalcSize(new GUIContent(labels[i])).x + 18f * Scale;
            int start = 0;
            while (start < labels.Length)
            {
                int end = start;
                float used = 0f;
                while (end < labels.Length)
                {
                    float next = used + (end > start ? g : 0f) + need[end];
                    if (end > start && next > full) break;
                    used = next;
                    end++;
                }
                var r = Row(24);
                r.height -= 2 * Scale;
                int n = end - start;
                float extra = Mathf.Max(0f, (r.width - used) / n);
                float bx = r.x;
                for (int i = start; i < end; i++)
                {
                    float bw = i == end - 1 ? r.xMax - bx : need[i] + extra;
                    var b = new Rect(bx, r.y, bw, r.height);
                    bx += bw + g;
                    Fill(b, i == active ? Accent : Over(b) ? Hover : Line);
                    GUI.Label(b, labels[i], _center);
                    if (Clicked(b)) clicked = i;
                }
                Gap(4f);
                start = end;
            }
            Gap(2f);
            return clicked;
        }

        public int Choice(string[] labels, int index)
        {
            int c = Buttons(labels, index);
            return c >= 0 ? c : index;
        }

        public float Slider(string label, float value, float min, float max, Func<float, string> show, bool log = false)
        {
            int id = ++_id;
            var top = Row(20);
            GUI.Label(top, label, _left);
            GUI.Label(top, show != null ? show(value) : value.ToString("0.##"), _right);
            var r = Row(18);
            var track = new Rect(r.x, r.y + 5 * Scale, r.width, 6 * Scale);
            float t = ToT(value, min, max, log);
            if (Clicked(Wide(r))) _drag = id;
            if (_drag == id)
            {
                if (_down)
                {
                    t = Mathf.Clamp01((_mouse.x - track.x) / track.width);
                    value = FromT(t, min, max, log);
                }
                else _drag = -1;
            }
            Fill(track, Line);
            Fill(new Rect(track.x, track.y, track.width * t, track.height), Accent);
            Knob(track, t);
            Gap(4f);
            return value;
        }

        public Color Colour(string label, Color c)
        {
            var top = Row(22);
            GUI.Label(top, label, _left);
            float sw = 34f * Scale;
            var swatch = new Rect(top.xMax - sw, top.y + 3 * Scale, sw, top.height - 6 * Scale);
            Fill(swatch, Line);
            Fill(new Rect(swatch.x + 1, swatch.y + 1, swatch.width - 2, swatch.height - 2), c);

            int id = ++_id;
            float h, s, v;
            Vector3 kept;
            Color was;
            if (_hsv.TryGetValue(id, out kept) && _rgb.TryGetValue(id, out was) && Same(was, c)) { h = kept.x; s = kept.y; v = kept.z; }
            else Color.RGBToHSV(c, out h, out s, out v);
            h = Bar01("h", h, x => Color.HSVToRGB(x, 1f, 1f));
            s = Bar01("s", s, x => Color.HSVToRGB(h, x, Mathf.Max(v, 0.25f)));
            v = Bar01("v", v, x => Color.HSVToRGB(h, s, x));
            Gap(4f);
            var outc = Color.HSVToRGB(h, s, v);
            _hsv[id] = new Vector3(h, s, v);
            _rgb[id] = outc;
            return outc;
        }

        readonly System.Collections.Generic.Dictionary<int, Vector3> _hsv = new System.Collections.Generic.Dictionary<int, Vector3>();
        readonly System.Collections.Generic.Dictionary<int, Color> _rgb = new System.Collections.Generic.Dictionary<int, Color>();

        static bool Same(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.002f && Mathf.Abs(a.g - b.g) < 0.002f && Mathf.Abs(a.b - b.b) < 0.002f;
        }

        float Bar01(string tag, float t, Func<float, Color> tint)
        {
            int id = ++_id;
            var r = Row(16);
            float lw = 16f * Scale;
            GUI.Label(new Rect(r.x, r.y, lw, r.height), tag, _dim);
            var track = new Rect(r.x + lw, r.y + 4 * Scale, r.width - lw, 8 * Scale);
            if (Clicked(Wide(r))) _drag = id;
            if (_drag == id)
            {
                if (_down) t = Mathf.Clamp01((_mouse.x - track.x) / track.width);
                else _drag = -1;
            }
            const int Steps = 24;
            float sw = track.width / Steps;
            for (int i = 0; i < Steps; i++)
                Fill(new Rect(track.x + i * sw, track.y, sw + 0.5f, track.height), tint((i + 0.5f) / Steps));
            Knob(track, t);
            return t;
        }

        void Knob(Rect track, float t)
        {
            float kx = track.x + track.width * t;
            Fill(new Rect(kx - 5 * Scale, track.y - 4 * Scale, 10 * Scale, track.height + 8 * Scale), Ink);
        }

        static float ToT(float v, float min, float max, bool log)
        {
            v = Mathf.Clamp(v, min, max);
            if (log && min > 0f) return Mathf.Log(v / min) / Mathf.Log(max / min);
            return Mathf.InverseLerp(min, max, v);
        }

        static float FromT(float t, float min, float max, bool log)
        {
            if (log && min > 0f) return min * Mathf.Pow(max / min, Mathf.Clamp01(t));
            return Mathf.Lerp(min, max, t);
        }
    }
}
