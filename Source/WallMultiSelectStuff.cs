using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AutoEaves
{
	/// <summary>
	/// Vanilla SelectAllMatching only matches ThingDef (not Stuff). With neverMultiSelect
	/// cleared on Wall, double-click would grab every wall material. Keep same Stuff only.
	/// </summary>
	[HarmonyPatch(typeof(Selector), "SelectAllMatchingObjectUnderMouseOnScreen")]
	internal static class Patch_WallMultiSelectSameStuff
	{
		private static Thing? referenceWall;

		static void Prefix(Selector __instance)
		{
			referenceWall = null;
			Thing? t = __instance.SingleSelectedThing;
			if (t != null && IsWallLike(t))
				referenceWall = t;
		}

		static void Postfix(Selector __instance)
		{
			Thing? reference = referenceWall;
			referenceWall = null;
			if (reference == null)
				return;

			ThingDef? wantStuff = reference.Stuff;
			List<object> selected = __instance.SelectedObjectsListForReading;
			for (int i = selected.Count - 1; i >= 0; i--)
			{
				if (selected[i] is not Thing other || other == reference)
					continue;
				if (!IsWallLike(other) || other.def != reference.def)
					continue;
				if (other.Stuff != wantStuff)
					__instance.Deselect(other);
			}
		}

		private static bool IsWallLike(Thing t)
		{
			return t.def != null && (t.def.IsWall || t.def.defName == "Wall");
		}
	}
}
