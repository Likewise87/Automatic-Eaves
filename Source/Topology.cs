using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace AutoEaves
{
    public static partial class EaveLogic
    {
        public enum Reach { Open, Terminal, Continuous }

        public static void Postfix(Thing __instance, SectionLayer layer)
        {
            if (!(__instance is Building building) || !building.Spawned) return;
            if (!building.def.IsWall && !building.def.IsDoor) return;

            // 1. SOLID BLOCK PREVENTION
            if (IsWallAt(building, Rot4.North.FacingCell) && IsWallAt(building, Rot4.East.FacingCell) &&
                IsWallAt(building, Rot4.South.FacingCell) && IsWallAt(building, Rot4.West.FacingCell) &&
                IsWallDiag(building, Rot4.North, Rot4.East) && IsWallDiag(building, Rot4.East, Rot4.South) &&
                IsWallDiag(building, Rot4.South, Rot4.West) && IsWallDiag(building, Rot4.West, Rot4.North)) return;

            var comp = building.GetComp<CompAutoEave>();
            if (comp == null || !comp.ShouldShowEaves) return;

            RoofGrid roofGrid = building.Map.roofGrid;
            Reach nR = GetReach(building, Rot4.North);
            Reach eR = GetReach(building, Rot4.East);
            Reach sR = GetReach(building, Rot4.South);
            Reach wR = GetReach(building, Rot4.West);

            bool nW = nR != Reach.Open; bool eW = eR != Reach.Open;
            bool sW = sR != Reach.Open; bool wW = wR != Reach.Open;

            int neighbors = (nW ? 1 : 0) + (eW ? 1 : 0) + (sW ? 1 : 0) + (wW ? 1 : 0);
            if (neighbors == 0) return;

            string label = IdentifyTopology(nR, eR, sR, wR, building, out bool isBC);

            // --- 2. STRAIGHT EAVES ---
            if (label == "End" || label == "SW" || label == "ABG" || label == "ASG" || label == "TC" || label == "SC")
            {
                for (int i = 0; i < 4; i++)
                {
                    Rot4 dir = new Rot4(i);
                    if (!IsWallAt(building, dir.FacingCell) && !roofGrid.Roofed(building.Position + dir.FacingCell))
                    {
                        if (label == "End")
                        {
                            Rot4 neighborDir = nW ? Rot4.North : eW ? Rot4.East : sW ? Rot4.South : Rot4.West;
                            if (dir == neighborDir.Opposite) continue;
                        }
                        Draw(building, layer, dir, EaveType.Straight, dir.FacingCell.ToVector3());
                    }
                }
            }

            // --- 3. CORNERS & POCKETS ---
            if (label == "BC" || label == "SC")
            {
                EaveType outerType = (label == "SC") ? EaveType.OuterCornerSmall : EaveType.OuterCorner;

                // SURGERY: We check all 4 diagonal slots. 
                // If two adjacent cardinal directions are walls, but their diagonal is empty ground, DRAW.

                // North + East (Top Right)
                if (nW && eW && !IsWallDiag(building, Rot4.North, Rot4.East))
                {
                    Draw(building, layer, Rot4.North, outerType, Vector3.zero);
                    HandlePocket(building, layer, Rot4.North, Rot4.East, roofGrid);
                }
                // East + South (Bottom Right)
                if (eW && sW && !IsWallDiag(building, Rot4.East, Rot4.South))
                {
                    Draw(building, layer, Rot4.East, outerType, Vector3.zero);
                    HandlePocket(building, layer, Rot4.East, Rot4.South, roofGrid);
                }
                // South + West (Bottom Left)
                if (sW && wW && !IsWallDiag(building, Rot4.South, Rot4.West))
                {
                    Draw(building, layer, Rot4.South, outerType, Vector3.zero);
                    HandlePocket(building, layer, Rot4.South, Rot4.West, roofGrid);
                }
                // West + North (Top Left)
                if (wW && nW && !IsWallDiag(building, Rot4.West, Rot4.North))
                {
                    Draw(building, layer, Rot4.West, outerType, Vector3.zero);
                    HandlePocket(building, layer, Rot4.West, Rot4.North, roofGrid);
                }
            }

            // T and X junctions only handle pockets, no outer corners
            if (label == "TC" || label == "XC")
            {
                CheckAndDrawPocket(building, layer, Rot4.North, Rot4.East, roofGrid);
                CheckAndDrawPocket(building, layer, Rot4.East, Rot4.South, roofGrid);
                CheckAndDrawPocket(building, layer, Rot4.South, Rot4.West, roofGrid);
                CheckAndDrawPocket(building, layer, Rot4.West, Rot4.North, roofGrid);
            }

            if (CompAutoEave.ShowDebugColors)
            {
                Color debugColor = GetColorForTopology(label);
                Vector3 overlayPos = building.TrueCenter();
                overlayPos.y = AltitudeLayer.MetaOverlays.AltitudeFor() + 0.15f;
                Printer_Plane.PrintPlane(layer, overlayPos, new Vector2(0.85f, 0.85f), MaterialPool.MatFrom(BaseContent.WhiteTex, ShaderDatabase.MetaOverlay, debugColor));
            }
        }

        public static void Draw(Building wall, SectionLayer layer, Rot4 rot, EaveType type, Vector3 offset)
        {
            string path = GetPath(wall, type);
            Graphic g = GetGraphic(path);
            if (g == null) return;

            Rot4 tRot;
            if (path.Contains("Eavecorrner") && (type == EaveType.OuterCorner || type == EaveType.OuterCornerSmall))
            {
                if (rot == Rot4.North) tRot = Rot4.South;
                else if (rot == Rot4.East) tRot = Rot4.West;
                else if (rot == Rot4.South) tRot = Rot4.North;
                else tRot = Rot4.East;
            }
            else
            {
                tRot = (type == EaveType.Straight) ? rot.Opposite : (type == EaveType.OuterCorner || type == EaveType.OuterCornerSmall) ?
                        (rot == Rot4.North ? Rot4.West : rot == Rot4.East ? Rot4.North : rot == Rot4.South ? Rot4.East : Rot4.South) : rot;
            }

            Vector3 pos = wall.TrueCenter() + offset;
            if (type == EaveType.OuterCornerSmall) pos.y = AltitudeLayer.Skyfaller.AltitudeFor();
            else if (type == EaveType.InnerCornerSmall) pos.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            else pos.y = AltitudeLayer.MoteOverheadLow.AltitudeFor();

            Vector2 size = (wall.def.IsDoor || wall.def.defName.Contains("Door")) ? new Vector2(3f, 1.5f) : new Vector2(3f, 3f);
            Printer_Plane.PrintPlane(layer, pos, size, MaterialPool.MatFrom((Texture2D)g.MatAt(tRot).mainTexture, g.MatAt(tRot).shader, wall.DrawColor), 0f);
        }

        private static string IdentifyTopology(Reach nR, Reach eR, Reach sR, Reach wR, Building building, out bool isBC)
        {
            isBC = false;
            bool nW = nR != Reach.Open; bool eW = eR != Reach.Open;
            bool sW = sR != Reach.Open; bool wW = wR != Reach.Open;
            int neighbors = (nW ? 1 : 0) + (eW ? 1 : 0) + (sW ? 1 : 0) + (wW ? 1 : 0);

            if (neighbors == 4) return "XC";
            if (neighbors == 3) return "TC";
            if (neighbors == 2)
            {
                if ((nW && sW) || (eW && wW))
                {
                    if (IsAdjacentToBigCorner(building, nW, eW, sW, wW)) return "ABG";
                    if (IsAdjacentToSmallCorner(building, nR, eR, sR, wR)) return "ASG";
                    return "SW";
                }
                isBC = (nW ? nR == Reach.Continuous : true) && (eW ? eR == Reach.Continuous : true) && (sW ? sR == Reach.Continuous : true) && (wW ? wR == Reach.Continuous : true);
                return isBC ? "BC" : "SC";
            }
            return neighbors == 1 ? "End" : "";
        }

        private static void HandlePocket(Building b, SectionLayer layer, Rot4 d1, Rot4 d2, RoofGrid grid)
        {
            IntVec3 pocketPos = b.Position + d1.FacingCell + d2.FacingCell;
            if (!grid.Roofed(pocketPos)) Draw(b, layer, d1, EaveType.InnerCornerSmall, (d1.FacingCell + d2.FacingCell).ToVector3());
        }

        private static void CheckAndDrawPocket(Building b, SectionLayer layer, Rot4 d1, Rot4 d2, RoofGrid grid)
        {
            if (IsWallAt(b, d1.FacingCell) && IsWallAt(b, d2.FacingCell) && !IsWallDiag(b, d1, d2)) HandlePocket(b, layer, d1, d2, grid);
        }

        private static Reach GetReach(Building b, Rot4 dir) { if (!IsWallAt(b, dir.FacingCell)) return Reach.Open; return IsWallAt(b, dir.FacingCell * 2) ? Reach.Continuous : Reach.Terminal; }
        private static bool IsAdjacentToBigCorner(Building b, bool nW, bool eW, bool sW, bool wW)
        {
            if (nW && sW) return IsWallDiag(b, Rot4.North, Rot4.East) || IsWallDiag(b, Rot4.North, Rot4.West) || IsWallDiag(b, Rot4.South, Rot4.East) || IsWallDiag(b, Rot4.South, Rot4.West);
            if (eW && wW) return IsWallDiag(b, Rot4.East, Rot4.North) || IsWallDiag(b, Rot4.East, Rot4.South) || IsWallDiag(b, Rot4.West, Rot4.North) || IsWallDiag(b, Rot4.West, Rot4.South);
            return false;
        }
        private static bool IsAdjacentToSmallCorner(Building b, Reach nR, Reach eR, Reach sR, Reach wR) => nR == Reach.Terminal || eR == Reach.Terminal || sR == Reach.Terminal || wR == Reach.Terminal;
        private static bool IsWallAt(Building b, IntVec3 offset) { IntVec3 pos = b.Position + offset; return pos.InBounds(b.Map) && pos.GetEdifice(b.Map) is Building o && (o.def.IsWall || o.def.IsDoor); }
        private static bool IsWallDiag(Building b, Rot4 d1, Rot4 d2) => IsWallAt(b, d1.FacingCell + d2.FacingCell);

        private static Color GetColorForTopology(string label)
        {
            switch (label)
            {
                case "BC": return new Color(0, 0.6f, 1, 0.5f);
                case "SC": return new Color(1, 0.9f, 0, 0.5f);
                case "SW": return new Color(1, 1, 1, 0.2f);
                case "TC": return new Color(0.8f, 0, 0.8f, 0.5f);
                case "XC": return new Color(1, 0, 0, 0.6f);
                case "ABG": return new Color(0, 1, 0.4f, 0.4f);
                case "ASG": return new Color(1, 0.5f, 0, 0.4f);
                case "End": return new Color(0.7f, 0.7f, 0.7f, 0.3f);
                default: return new Color(0.5f, 0.5f, 0.5f, 0.3f);
            }
        }
    }
}