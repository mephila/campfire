using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace CozyMenu
{
    [BepInPlugin("mephila.cozymenu", "cozymenu", "1.1.0")]
    public class Menu : BaseUnityPlugin
    {
        class Section
        {
            public string Title;
            public Action<Panel> Draw;
            public bool Folded;
        }

        readonly List<Section> _sections = new List<Section>();

        ConfigEntry<KeyCode> _key;
        ConfigEntry<float> _x;
        ConfigEntry<float> _y;

        Size _size;
        NameColour _colour;

        bool _open;
        bool _keyHeld;
        bool _locked;
        bool _forced;
        float _height = 120f;

        readonly Panel _panel = new Panel();

        void Awake()
        {
            Config.SaveOnConfigSet = false;
            _key = Config.Bind("menu", "key", KeyCode.F6, "opens and closes the menu");
            _x = Config.Bind("menu", "x", 40f, "window position");
            _y = Config.Bind("menu", "y", 200f, "window position");
            _size = new Size(Config, Logger);
            _colour = new NameColour(Config);
            Config.Save();
            Add("size", _size.Draw);
            Add("name colour", _colour.Draw);
            Logger.LogInfo("cozymenu loaded, " + _key.Value + " opens it");
        }

        void Add(string title, Action<Panel> draw)
        {
            _sections.Add(new Section { Title = title, Draw = draw });
        }

        void Start()
        {
            StartCoroutine(AfterUpdates());
        }

        IEnumerator AfterUpdates()
        {
            while (true)
            {
                yield return null;
                try { _size.AfterUpdates(); } catch { }
            }
        }

        void Update()
        {
            try { _size.Update(); } catch (Exception e) { Logger.LogError(e); }
            try { _colour.Update(); } catch (Exception e) { Logger.LogError(e); }
            if (_open) Hold();
            else Release();
        }

        void OnDestroy()
        {
            Release();
            _size.Unsize();
            _colour.PutBack();
            Config.Save();
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

        void Toggle()
        {
            _open = !_open;
            _panel.Reset();
            if (!_open) Config.Save();
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

            _panel.Feed(e);
            if (e.type != EventType.Repaint) return;

            if (_open)
            {
                GUI.depth = -900;
                try { Draw(); }
                catch (Exception ex) { Logger.LogError(ex); Toggle(); }
            }
            _panel.EndFrame();
        }

        void Draw()
        {
            var p = _panel;
            p.Begin();
            float s = p.Scale;
            float w = Panel.Width * s;
            float h = _height * s;

            float x = Mathf.Clamp(_x.Value, 0f, Mathf.Max(0f, Screen.width - w));
            float y = Mathf.Clamp(_y.Value, 0f, Mathf.Max(0f, Screen.height - h));
            if (p.DragWindow(new Rect(x, y, w, Panel.TitleHeight * s), ref x, ref y, w, h))
            {
                _x.Value = x;
                _y.Value = y;
            }

            p.Frame(new Rect(x, y, w, h), "cozymenu");
            p.At(x, y + (Panel.TitleHeight + 6f) * s);

            foreach (var sec in _sections)
            {
                if (p.Header(sec.Title, sec.Folded)) sec.Folded = !sec.Folded;
                if (sec.Folded) continue;
                try { sec.Draw(p); }
                catch (Exception ex) { Logger.LogError(sec.Title + ": " + ex); }
                p.Gap(6f);
            }

            _height = Mathf.Max(80f, p.Used + 6f);
        }
    }
}
