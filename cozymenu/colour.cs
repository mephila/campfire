using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx.Configuration;
using UnityEngine;

namespace CozyMenu
{
    class NameColour
    {
        const int MaxChars = 32;
        const int TagChars = 6;
        const float Interval = 0.25f;
        const float MovingInterval = 0.1f;
        const float SpawnHold = 8f;
        const float JoinHold = 4f;
        const float WaitCap = 90f;
        const string Close = "</color>";

        const int Off = 0, One = 1, Fade = 2, Gradient = 3, Rainbow = 4, Letters = 5;
        static readonly string[] Modes = { "off", "one colour", "two colour fade", "gradient", "rainbow", "rainbow letters" };

        readonly ConfigEntry<int> _mode;
        readonly ConfigEntry<Color> _a;
        readonly ConfigEntry<Color> _b;
        readonly ConfigEntry<bool> _move;
        readonly ConfigEntry<float> _speed;
        readonly ConfigEntry<bool> _chat;
        readonly ConfigEntry<bool> _plainJoin;

        PlayerObject _seen;
        string _original;
        string _applied = "";
        float _next;
        float _spawnAt;
        float _holdUntil;
        float _nextScan;
        readonly HashSet<PlayerObject> _known = new HashSet<PlayerObject>();
        readonly Dictionary<PlayerObject, float> _arriving = new Dictionary<PlayerObject, float>();

        public NameColour(ConfigFile cfg)
        {
            _mode = cfg.Bind("name colour", "mode", 0, new ConfigDescription("0 off, 1 one colour, 2 two colour fade, 3 gradient, 4 rainbow, 5 rainbow letters", new AcceptableValueRange<int>(0, 5)));
            _a = cfg.Bind("name colour", "colour", (Color)new Color32(255, 143, 203, 255), "the one colour, and the first colour of fade and gradient");
            _b = cfg.Bind("name colour", "second colour", (Color)new Color32(111, 182, 255, 255), "second colour of fade and gradient");
            _move = cfg.Bind("name colour", "moving gradient", false, "the gradient slides along the name");
            _speed = cfg.Bind("name colour", "speed", 0.5f, new ConfigDescription("speed of the moving modes", new AcceptableValueRange<float>(0.05f, 2f)));
            _chat = cfg.Bind("name colour", "chat follows colour", false, "chat messages use the name colour too");
            _plainJoin = cfg.Bind("name colour", "plain name on joins", true, "keeps the name uncoloured while joining and while other players join");
        }

        bool Moving
        {
            get
            {
                int m = _mode.Value;
                return m == Fade || m == Rainbow || m == Letters || (m == Gradient && _move.Value);
            }
        }

        public void Draw(Panel p)
        {
            _mode.Value = p.Choice(Modes, _mode.Value);
            int m = _mode.Value;
            if (m == One) _a.Value = p.Colour("colour", _a.Value);
            if (m == Fade || m == Gradient)
            {
                _a.Value = p.Colour("first colour", _a.Value);
                _b.Value = p.Colour("second colour", _b.Value);
            }
            if (m == Gradient) _move.Value = p.Toggle("moving gradient", _move.Value);
            if (Moving) _speed.Value = p.Slider("speed", _speed.Value, 0.05f, 2f, v => v.ToString("0.00") + "x");
            if (m == One || m == Fade || m == Rainbow) _chat.Value = p.Toggle("chat follows colour", _chat.Value);
            if (m != Off) _plainJoin.Value = p.Toggle("plain name on joins", _plainJoin.Value);
        }

        static string Live(PlayerObject p)
        {
            try { return p.PlayerName.ToString(); } catch { return null; }
        }

        static string Strip(string s)
        {
            return string.IsNullOrEmpty(s) ? "" : Regex.Replace(s, "<[^>]*>", "");
        }

        public void PutBack()
        {
            var p = _seen;
            string orig = _original;
            _original = null;
            _applied = "";
            if (p == null || orig == null) return;
            try { if (p.Object != null && p.HasStateAuthority && Live(p) != orig) p.PlayerName = orig; } catch { }
        }

        public void Update()
        {
            if (_mode.Value <= Off)
            {
                if (_original != null) PutBack();
                return;
            }

            var p = PlayerTracker.MyPlayer;
            if (p == null || p.Object == null) { _seen = null; _original = null; return; }
            if (!p.HasStateAuthority) return;

            float now = Time.realtimeSinceStartup;
            if (!ReferenceEquals(p, _seen) || _original == null)
            {
                _seen = p;
                _original = Strip(Live(p) ?? "");
                _applied = "";
                _next = 0f;
                _spawnAt = now;
                _holdUntil = 0f;
                _nextScan = 0f;
                _known.Clear();
                _arriving.Clear();
            }

            bool hold = Holding(p, now);
            if (now < _next) return;

            string plain = _original.Length > MaxChars ? _original.Substring(0, MaxChars) : _original;
            if (plain.Length == 0) return;
            string want = hold ? plain : Colourise(plain);
            if (want == _applied && want == Live(p)) return;

            _next = now + (Moving ? MovingInterval : Interval);
            p.PlayerName = want;
            _applied = want;
        }

        bool Holding(PlayerObject me, float now)
        {
            if (!_plainJoin.Value) { _arriving.Clear(); return false; }
            if (me.SpawnedCharacter == null) _spawnAt = now;

            if (now >= _nextScan)
            {
                _nextScan = now + 0.5f;
                _known.RemoveWhere(x => x == null);
                foreach (var o in PlayerTracker.Players)
                {
                    if (o == null || ReferenceEquals(o, me)) continue;
                    if (_known.Add(o)) _arriving[o] = now;
                }
            }

            if (_arriving.Count > 0)
            {
                var done = new List<PlayerObject>();
                foreach (var kv in _arriving)
                {
                    var o = kv.Key;
                    if (o == null || now - kv.Value > WaitCap) { done.Add(o); continue; }
                    if (o.SpawnedCharacter != null) { _holdUntil = Mathf.Max(_holdUntil, now + JoinHold); done.Add(o); }
                }
                foreach (var o in done) _arriving.Remove(o);
            }

            return _arriving.Count > 0 || now < _holdUntil || now < _spawnAt + SpawnHold;
        }

        float Hue { get { return Time.realtimeSinceStartup * _speed.Value; } }

        static string Hex(Color c)
        {
            const string d = "0123456789abcdef";
            return "<#" + d[Mathf.Clamp(Mathf.RoundToInt(c.r * 15f), 0, 15)] + d[Mathf.Clamp(Mathf.RoundToInt(c.g * 15f), 0, 15)] + d[Mathf.Clamp(Mathf.RoundToInt(c.b * 15f), 0, 15)] + ">";
        }

        string Colourise(string plain)
        {
            switch (_mode.Value)
            {
                case One: return Wrap(plain, _a.Value);
                case Fade: return Wrap(plain, Color.Lerp(_a.Value, _b.Value, Mathf.PingPong(Hue * 2f, 1f)));
                case Gradient: return Graded(plain);
                case Rainbow: return Wrap(plain, Color.HSVToRGB(Mathf.Repeat(Hue, 1f), 0.6f, 1f));
                case Letters: return Rainbowed(plain);
                default: return plain;
            }
        }

        string Wrap(string plain, Color c)
        {
            string tag = Hex(c);
            if (_chat.Value)
            {
                int space = MaxChars - tag.Length * 2;
                return tag + tag + (plain.Length > space ? plain.Substring(0, space) : plain);
            }
            if (plain.Length + tag.Length + Close.Length <= MaxChars) return tag + plain + Close;
            int room = MaxChars - tag.Length;
            return tag + (plain.Length > room ? plain.Substring(0, room) : plain);
        }

        string Graded(string plain)
        {
            int n = plain.Length;
            int k = Mathf.Min(n, (MaxChars - n) / TagChars);
            if (k < 1)
            {
                int room = MaxChars - TagChars;
                return Hex(_a.Value) + (n > room ? plain.Substring(0, room) : plain);
            }
            var sb = new StringBuilder();
            float shift = _move.Value ? Hue : 0f;
            for (int i = 0; i < k; i++)
            {
                int from = i * n / k;
                int to = (i + 1) * n / k;
                float t = k == 1 ? 0f : i / (k - 1f);
                if (_move.Value) t = Mathf.PingPong(t + shift * 2f, 1f);
                sb.Append(Hex(Color.Lerp(_a.Value, _b.Value, t))).Append(plain, from, to - from);
            }
            return sb.ToString();
        }

        string Rainbowed(string plain)
        {
            var sb = new StringBuilder();
            float off = Hue;
            int i = 0;
            for (; i < plain.Length; i++)
            {
                char ch = plain[i];
                if (char.IsWhiteSpace(ch))
                {
                    if (sb.Length + 1 > MaxChars) break;
                    sb.Append(ch);
                    continue;
                }
                var c = Color.HSVToRGB(Mathf.Repeat(off + i * 0.09f, 1f), 0.6f, 1f);
                if (sb.Length + TagChars + 1 > MaxChars) break;
                sb.Append(Hex(c)).Append(ch);
            }
            for (; i < plain.Length && sb.Length < MaxChars; i++) sb.Append(plain[i]);
            return sb.ToString();
        }
    }
}
