using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace CozyUnlock
{
    [BepInPlugin("mephila.cozyunlock", "cozyunlock", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        ConfigEntry<bool> _fullGame;
        ConfigEntry<bool> _levels;
        ConfigEntry<bool> _cosmetics;
        ConfigEntry<bool> _money;
        ConfigEntry<int> _amount;

        static readonly BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        readonly Dictionary<Type, FieldInfo> _flags = new Dictionary<Type, FieldInfo>();
        float _next;
        int _opened, _unlocked, _owned;
        bool _moneyOk;

        void Awake()
        {
            _fullGame = Config.Bind("unlock", "full game items", true, "opens everything marked full game only: cosmetics, species, tools, furniture, attachments, recipes, ingredients and critters");
            _levels = Config.Bind("unlock", "skip level requirements", true, "unlocks items that normally need a higher level");
            _cosmetics = Config.Bind("unlock", "own all cosmetics", true, "owns every cosmetic without buying it");
            _money = Config.Bind("money", "keep money topped up", true, "tops money back up to the amount below");
            _amount = Config.Bind("money", "amount", 999999999, new ConfigDescription("money to keep", new AcceptableValueRange<int>(0, 999999999)));
            _self = this;
            try
            {
                var load = AccessTools.Method(typeof(CosmeticsHandler.SaveData), "Load");
                if (load == null) Logger.LogWarning("no CosmeticsHandler.SaveData.Load");
                else new Harmony("mephila.cozyunlock").Patch(load, prefix: new HarmonyMethod(typeof(Plugin), nameof(BeforeCosmeticsLoad)));
            }
            catch (Exception e) { Logger.LogError(e); }
            Logger.LogInfo("cozyunlock loaded");
        }

        static Plugin _self;

        static void BeforeCosmeticsLoad()
        {
            if (_self == null) return;
            _self.Logger.LogInfo("cosmetics save loading, unlocking first");
            try { _self.Apply(); }
            catch (Exception e) { _self.Logger.LogError(e); }
        }

        void Update()
        {
            float now = Time.realtimeSinceStartup;
            if (now < _next) return;
            _next = now + 0.5f;
            try { Apply(); }
            catch (Exception e)
            {
                Logger.LogError(e);
                _next = now + 10f;
            }
        }

        void Apply()
        {
            int opened = 0, unlocked = 0, owned = 0;

            foreach (var d in All<CosmeticDatabase, CosmeticData>())
            {
                if (_fullGame.Value) opened += Open(d);
                if (_levels.Value && !d.IsUnlockFlagSet) { d.UnlockMe(); unlocked++; }
                if (_cosmetics.Value && !d.IsOwned) { d.IsOwned = true; owned++; }
            }
            foreach (var d in All<ToolDatabase, ToolData>())
            {
                if (_fullGame.Value) opened += Open(d);
                if (_levels.Value && !d.IsUnlockFlagSet) { d.UnlockMe(); unlocked++; }
            }
            foreach (var d in All<RecipeDatabase, RecipeData>())
            {
                if (_fullGame.Value) opened += Open(d);
                if (_levels.Value && !d.IsUnlockFlagSet) { d.UnlockMe(false); unlocked++; }
            }
            foreach (var d in All<FurnitureDatabase, FurnitureData>())
            {
                if (_fullGame.Value) opened += Open(d);
                if (_levels.Value && !d.IsUnlockFlagSet) { d.UnlockMe(); unlocked++; }
            }
            foreach (var d in All<FurnitureAttachmentDatabase, FurnitureAttachmentData>())
            {
                if (_fullGame.Value) opened += Open(d);
                if (_levels.Value && !d.IsUnlockFlagSet) { d.UnlockMe(); unlocked++; }
            }
            foreach (var d in All<IngredientDatabase, IngredientData>())
            {
                if (_fullGame.Value) opened += Open(d);
                if (_levels.Value && !d.IsUnlockFlagSet) { d.UnlockMe(); unlocked++; }
            }
            foreach (var d in All<CritterDatabase, CritterData>())
                if (_fullGame.Value) opened += Open(d);

            if (opened > 0 || unlocked > 0 || owned > 0)
            {
                _opened += opened;
                _unlocked += unlocked;
                _owned += owned;
                Logger.LogInfo("opened " + opened + " full game items, unlocked " + unlocked + " level locked items, owned " + owned + " cosmetics (totals " + _opened + ", " + _unlocked + ", " + _owned + ")");
            }

            if (_money.Value) TopUp();
        }

        int Open(BaseData d)
        {
            var t = d.GetType();
            FieldInfo f;
            if (!_flags.TryGetValue(t, out f))
            {
                f = t.GetField("FullGameOnly", Any);
                _flags[t] = f;
                if (f == null) Logger.LogWarning("no FullGameOnly on " + t.Name + "");
            }
            if (f == null || !(bool)f.GetValue(d)) return 0;
            f.SetValue(d, false);
            return 1;
        }

        void TopUp()
        {
            if (!global::Singleton<PlayerInventory>.InstanceFound()) return;
            var inv = global::Singleton<PlayerInventory>.Instance;
            if (inv == null) return;
            int want = Mathf.Clamp(_amount.Value, 0, 999999999);
            int have = inv.Currency;
            if (have < want)
            {
                inv.AddCurrency(want - have);
                if (!_moneyOk) Logger.LogInfo("money topped up to " + inv.Currency);
                _moneyOk = true;
            }
        }

        static IEnumerable<E> All<D, E>() where D : BaseDataManager<D, E> where E : BaseData
        {
            var db = BaseDataManager<D, E>.Instance;
            if (db == null || db.DatabaseList == null) yield break;
            foreach (var e in db.DatabaseList)
                if (e != null) yield return e;
        }
    }
}
