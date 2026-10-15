using System;
using UnityEngine;
using Verse;

namespace AutoEaves
{
    public class AutoEaveSettings : ModSettings
    {
        public bool onlyPlayerBuilt = false;
        public bool spawnCorners = true;
        public bool defaultEavesActive = true;
        public bool showTopologyOverlay = false; // The new setting for the heatmap

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref onlyPlayerBuilt, "onlyPlayerBuilt", false);
            Scribe_Values.Look(ref spawnCorners, "spawnCorners", true);
            Scribe_Values.Look(ref defaultEavesActive, "defaultEavesActive", true);
            Scribe_Values.Look(ref showTopologyOverlay, "showTopologyOverlay", false);

            // Sync the static helper used by the logic
            CompAutoEave.ShowDebugColors = showTopologyOverlay;
        }

        public void RestoreDefaults()
        {
            onlyPlayerBuilt = false;
            spawnCorners = true;
            defaultEavesActive = true;
            showTopologyOverlay = false;
            CompAutoEave.ShowDebugColors = showTopologyOverlay;
        }
    }

    public class AutoEaveMod : Mod
    {
        public static AutoEaveSettings settings;

        public AutoEaveMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<AutoEaveSettings>();
            // Sync on load
            CompAutoEave.ShowDebugColors = settings.showTopologyOverlay;
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);

            DrawResetButton(listingStandard, () =>
            {
                bool prevOverlay = settings.showTopologyOverlay;
                settings.RestoreDefaults();
                if (prevOverlay != settings.showTopologyOverlay && Current.ProgramState == ProgramState.Playing)
                    Find.CurrentMap?.mapDrawer.RegenerateEverythingNow();
            });

            // Toggle for the Heatmap/Topology
            bool previousOverlayState = settings.showTopologyOverlay;
            DrawCheckbox(listingStandard, "Show Topology Heatmap", ref settings.showTopologyOverlay,
                "Draws colored squares over walls to show how the mod identifies their shape (Blue = Corner, etc).",
                defaultValue: false);

            // If the user toggled the heatmap in settings, refresh the map immediately
            if (previousOverlayState != settings.showTopologyOverlay)
            {
                CompAutoEave.ShowDebugColors = settings.showTopologyOverlay;
                if (Current.ProgramState == ProgramState.Playing)
                {
                    Find.CurrentMap?.mapDrawer.RegenerateEverythingNow();
                }
            }

            listingStandard.Gap(4f);
            DrawCheckbox(listingStandard, "Eaves active by default", ref settings.defaultEavesActive,
                "If enabled, new walls will start with eaves turned ON.",
                defaultValue: true);
            DrawCheckbox(listingStandard, "Only apply to player-built walls", ref settings.onlyPlayerBuilt,
                "If enabled, eaves will only appear on walls that belong to your faction.",
                defaultValue: false);

            listingStandard.Gap(8f);
            if (listingStandard.ButtonText("Force Refresh Map Eaves"))
            {
                if (Current.ProgramState == ProgramState.Playing)
                    Find.CurrentMap.mapDrawer.RegenerateEverythingNow();
            }

            listingStandard.End();
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory() => "Auto Eaves";

        // ----- WD-style helpers (kept in SettingsMenu; no separate library file) -----

        private static void DrawResetButton(Listing_Standard l, Action onReset)
        {
            if (onReset == null) return;
            l.Gap(4f);
            Rect row = l.GetRect(30f);
            float resetW = Mathf.Min(250f, row.width);
            if (resetW < 120f) resetW = Mathf.Max(100f, row.width * 0.35f);
            Rect resetRect = new Rect(row.x, row.y, resetW, row.height);
            Color prev = GUI.color;
            GUI.color = new Color(1f, 0.5f, 0.5f);
            if (Widgets.ButtonText(resetRect, "Restore defaults")) onReset();
            GUI.color = prev;
            l.Gap(6f);
        }

        private static void DrawCheckbox(Listing_Standard l, string label, ref bool value, string tooltip = null, float? rowHeight = null, bool? defaultValue = null)
        {
            l.Gap(2f);
            float h = rowHeight ?? 24f;
            Rect r = l.GetRect(h);
            if (!tooltip.NullOrEmpty()) TooltipHandler.TipRegion(r, TooltipWithDefault(tooltip, defaultValue));
            Rect inner = h > 24f ? new Rect(r.x, r.y + (h - 24f) / 2f, r.width, 24f) : r;
            Widgets.CheckboxLabeled(inner, label, ref value);
        }

        private static string TooltipWithDefault(string tooltip, bool? defaultValue)
        {
            if (!defaultValue.HasValue) return tooltip;
            return AppendDefault(tooltip, defaultValue.Value ? "On" : "Off");
        }

        private static string AppendDefault(string tooltip, string def)
        {
            string line = $"Default: {def}";
            return tooltip.NullOrEmpty() ? line : tooltip + "\n" + line;
        }
    }
}
