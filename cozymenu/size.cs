using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace CozyMenu
{
    class Size
    {
        const float Min = 0.2f;
        const float Max = 15f;
        static readonly float[] Presets = { 0.25f, 0.5f, 1f, 2f, 5f, 10f };
        static readonly string[] PresetLabels = { "0.25x", "0.5x", "1x", "2x", "5x", "10x" };

        static readonly BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        static readonly FieldInfo FSize = typeof(PlayerCharacterStatusEffects).GetField("<CharacterSizeMult>k__BackingField", Any);
        static readonly MethodInfo MRecalc = typeof(PlayerCharacterStatusEffects).GetMethod("RecalculateStatusEffects", Any);
        static readonly FieldInfo FAnimator = typeof(PlayerCharacterAnimation).GetField("mAnimator", Any);

        readonly ConfigEntry<bool> _on;
        readonly ConfigEntry<float> _size;
        readonly ConfigEntry<float> _anim;

        PlayerCharacterStatusEffects _sized;
        readonly Dictionary<Animator, float> _wrote = new Dictionary<Animator, float>();

        public Size(ConfigFile cfg, ManualLogSource log)
        {
            _on = cfg.Bind("size", "custom size", false, "uses the size below");
            _size = cfg.Bind("size", "size", 3f, new ConfigDescription("player size", new AcceptableValueRange<float>(Min, Max)));
            _anim = cfg.Bind("size", "keep animation speed", 1f, new ConfigDescription("scales walk animations with size", new AcceptableValueRange<float>(0f, 1f)));
            if (FSize == null) log.LogWarning("no CharacterSizeMult on PlayerCharacterStatusEffects");
        }

        public void Draw(Panel p)
        {
            _on.Value = p.Toggle("custom size", _on.Value);
            _size.Value = Round(p.Slider("size", _size.Value, Min, Max, v => v.ToString("0.0#") + "x", true));
            int active = -1;
            for (int i = 0; i < Presets.Length; i++)
                if (_on.Value && Mathf.Abs(_size.Value - Presets[i]) < 0.001f) active = i;
            int picked = p.Buttons(PresetLabels, active);
            if (picked >= 0) { _size.Value = Presets[picked]; _on.Value = true; }
            _anim.Value = p.Slider("keep animation speed", _anim.Value, 0f, 1f, v => (v * 100f).ToString("0") + "%");
        }

        static float Round(float v)
        {
            return v < 1f ? Mathf.Round(v * 100f) / 100f : Mathf.Round(v * 20f) / 20f;
        }

        public void Update()
        {
            var me = PlayerTracker.MyPlayer != null ? PlayerTracker.MyPlayer.SpawnedCharacter : null;
            var fx = me != null ? me.StatusEffects : null;
            if (_on.Value && fx != null && FSize != null)
            {
                if (_sized != fx) { Unsize(); _sized = fx; }
                float want = Mathf.Clamp(_size.Value, Min, Max);
                if ((float)FSize.GetValue(fx) != want) FSize.SetValue(fx, want);
            }
            else Unsize();
        }

        public void Unsize()
        {
            var fx = _sized;
            _sized = null;
            if (fx == null || MRecalc == null) return;
            try { MRecalc.Invoke(fx, null); } catch { }
        }

        public void AfterUpdates()
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
    }
}
