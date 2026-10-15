using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using HarmonyLib;

namespace AutoEaves
{
    public enum EaveType { Straight, InnerCorner, OuterCorner, InnerCornerSmall, OuterCornerSmall }

    public class CompProperties_AutoEave : CompProperties
    {
        public CompProperties_AutoEave()
        {
            this.compClass = typeof(CompAutoEave);
        }
    }

    [StaticConstructorOnStartup]
    public static class AutoEaveStartup
    {
        static AutoEaveStartup()
        {
            try
            {
                var harmony = new Harmony("com.tsa.autoeaves");
                var original = AccessTools.Method(typeof(Thing), nameof(Thing.Print));
                var postfix = AccessTools.Method(typeof(EaveLogic), nameof(EaveLogic.Postfix));
                harmony.Patch(original, postfix: new HarmonyMethod(postfix));
                harmony.PatchAll();

                // C# Auto-sweep removed to support manual XML patching.
            }
            catch (Exception ex) { Log.Error("[AutoEaves] Startup failure: " + ex); }
        }
    }

    public class CompAutoEave : ThingComp
    {
        public bool eavesEnabled;
        public static bool ShowDebugColors = false;
        public bool ShouldShowEaves => eavesEnabled && (!AutoEaveMod.settings.onlyPlayerBuilt || (parent.Faction != null && parent.Faction.IsPlayer));

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad)
            {
                eavesEnabled = AutoEaveMod.settings.defaultEavesActive;
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref eavesEnabled, "eavesEnabled", AutoEaveMod.settings.defaultEavesActive);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (AutoEaveMod.settings.onlyPlayerBuilt && (parent.Faction == null || !parent.Faction.IsPlayer)) yield break;
            yield return new Command_Toggle
            {
                defaultLabel = "Auto Eaves",
                defaultDesc = "Toggle automatic eaves.",
                icon = ContentFinder<Texture2D>.Get("Things/Building/Eave_Icon"),
                isActive = () => eavesEnabled,
                toggleAction = () => {
                    eavesEnabled = !eavesEnabled;
                    if (parent.Spawned) parent.Map.mapDrawer.SectionAt(parent.Position).RegenerateAllLayers();
                }
            };
        }
    }

    public static partial class EaveLogic
    {
        private static readonly Dictionary<string, Graphic> graphicCache = new Dictionary<string, Graphic>();

        public static string GetPath(Building wall, EaveType type)
        {
            bool isStony = wall.Stuff?.stuffProps.categories.Contains(StuffCategoryDefOf.Stony) ?? false;
            bool isWoody = wall.Stuff?.stuffProps.categories.Contains(StuffCategoryDefOf.Woody) ?? false;

            if (!isStony && !isWoody)
            {
                switch (type)
                {
                    case EaveType.Straight: return "Things/Building/Eave/Eave";
                    case EaveType.OuterCorner: return "Things/Building/Eavecorrner/Eavecornerout";
                    case EaveType.InnerCorner: return "Things/Building/Eave/Eave-corner-inner";
                    case EaveType.OuterCornerSmall: return "Things/Building/Eave/Eave-corner-outersmall";
                    case EaveType.InnerCornerSmall: return "Things/Building/Eave/Eave-corner-innersmall";
                    default: return "Things/Building/Eave/Eave";
                }
            }

            string prefix = isStony ? "Things/Building/stonyEave/stonyEave" : "Things/Building/woodyEave/woodyEave";
            switch (type)
            {
                case EaveType.Straight: return prefix;
                case EaveType.OuterCorner: return prefix + "-outercorner";
                case EaveType.InnerCorner: return prefix + "-innercorner";
                case EaveType.OuterCornerSmall: return prefix + "-outercornersmall";
                case EaveType.InnerCornerSmall: return prefix + "-innercornersmall";
                default: return prefix;
            }
        }

        public static Graphic GetGraphic(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (!graphicCache.TryGetValue(path, out Graphic g))
            {
                g = GraphicDatabase.Get<Graphic_Multi>(path, ShaderDatabase.Cutout, Vector2.one, Color.white);
                if (g != null) graphicCache[path] = g;
            }
            return g;
        }
    }
}