using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FusedRibbons
{
    public class FusedRibbonsMod : Mod
    {
        public static FusedRibbonsMod Instance;

        public FusedRibbonsMod(ModContentPack content) : base(content)
        {
            Instance = this;
            GetSettings<FusedSettings>();
            new Harmony("kidmonkey.fusedribbons").PatchAll();
        }

        public override string SettingsCategory()
        {
            return "Fused Ribbons";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            FusedSettings.DoWindowContents(inRect);
        }
    }

    /// <summary>
    /// Keep member buttons off the bar without writing buttonVisible or defaultHotKey.
    /// Remove the mod and those defs are unchanged.
    /// </summary>
    [HarmonyPatch(typeof(MainButtonWorker), "get_Visible")]
    public static class Patch_HideMemberButtons
    {
        public static bool Prepare()
        {
            return AccessTools.Property(typeof(MainButtonWorker), "Visible") != null;
        }

        public static void Postfix(MainButtonWorker __instance, ref bool __result)
        {
            if (FusedSettings.ForceVisible(__instance.def))
            {
                __result = true;
                return;
            }
            if (FusedSettings.IsBarDisabled(__instance.def) || FusedSettings.IsHidden(__instance.def))
                __result = false;
            else if (RibbonGroups.ForMember(__instance.def) != null && !FusedSettings.ParentDisabled(__instance.def))
                __result = false;
        }
    }

    /// <summary>
    /// The selector is drawn above the window. A click there is outside the tab rect,
    /// and the game would close the tab before the button can switch pages.
    /// </summary>
    [HarmonyPatch(typeof(MainTabsRoot), "EscapeCurrentTab")]
    public static class Patch_KeepSelectorClick
    {
        public static bool Prepare()
        {
            return AccessTools.Method(typeof(MainTabsRoot), "EscapeCurrentTab") != null;
        }

        public static bool Prefix()
        {
            return !RibbonSelectorWindow.MouseOver();
        }
    }

    // The bottom bar sorts by order while it draws. Set it here so a move
    // does not wait for a reload.
    [HarmonyPatch(typeof(MainButtonsRoot), "DoButtons")]
    public static class Patch_ApplyBarOrder
    {
        public static bool Prepare()
        {
            return AccessTools.Method(typeof(MainButtonsRoot), "DoButtons") != null;
        }

        public static void Prefix()
        {
            CustomBarMaker.ApplyOrder();
        }
    }

    // Work measures its columns from the window size. Inset before that measure,
    // or Research is already drawn into the clip and a later shrink cannot move it.
    [HarmonyPatch(typeof(MainTabWindow), "SetInitialSizeAndPosition")]
    public static class Patch_FitWorkTab
    {
        public static void Postfix(MainTabWindow __instance)
        {
            RibbonSelectorWindow.FitWork(__instance);
        }
    }

    // The column group clips the header before DoHeader runs. A wider rect still
    // ends on that clip. Remember the word and draw it after the groups close.
    [HarmonyPatch(typeof(PawnColumnWorker), "DoHeader")]
    public static class Patch_LastColumnHeader
    {
        public static bool Prepare()
        {
            return AccessTools.Method(typeof(PawnColumnWorker), "DoHeader") != null;
        }

        public static void Postfix(PawnColumnWorker __instance, Rect rect)
        {
            if (__instance.def == null || rect.xMax < UI.screenWidth - 70f)
                return;
            var font = Text.Font;
            Text.Font = GameFont.Tiny;
            string label = __instance.def.LabelCap;
            float need = Text.CalcSize(label).x + 30f;
            Text.Font = font;
            if (need <= rect.width + 1f)
                return;
            var screen = GUIUtility.GUIToScreenPoint(new Vector2(rect.xMax - need, rect.y));
            HeaderOverlay.Set(new Rect(screen.x, screen.y, need, rect.height), label);
        }
    }

    public static class HeaderOverlay
    {
        private static bool show;
        private static Rect rect;
        private static string label;

        public static void Set(Rect next, string text)
        {
            rect = next;
            label = text;
            show = true;
        }

        public static void Draw()
        {
            if (!show)
                return;
            show = false;
            var font = Text.Font;
            var anchor = Text.Anchor;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.DrawBoxSolid(rect, new Color(0.13f, 0.13f, 0.13f, 1f));
            Widgets.Label(rect, label);
            Text.Font = font;
            Text.Anchor = anchor;
        }
    }

    [HarmonyPatch(typeof(UIRoot_Play), "UIRootOnGUI")]
    public static class Patch_DrawHeaderOverlay
    {
        public static void Postfix()
        {
            HeaderOverlay.Draw();
        }
    }

    // The name column keeps a wide fixed size, so the last work header hits the clip.
    // Fit it to the longest current name. A longer name still widens it.
    [HarmonyPatch(typeof(PawnColumnWorker), "GetMinWidth")]
    public static class Patch_NameColumnWidth
    {
        public static bool Prepare()
        {
            return AccessTools.Method(typeof(PawnColumnWorker), "GetMinWidth") != null;
        }

        public static void Postfix(PawnColumnWorker __instance, PawnTable table, ref int __result)
        {
            if (!IsNameColumn(__instance) || table == null)
                return;
            __result = NameWidth(table);
        }

        public static bool IsNameColumn(PawnColumnWorker worker)
        {
            return worker.def != null && worker.def.defName == "Label";
        }

        public static int NameWidth(PawnTable table)
        {
            float widest = 72f;
            var font = Text.Font;
            Text.Font = GameFont.Small;
            var pawns = table.PawnsListForReading;
            if (pawns != null)
            {
                for (int i = 0; i < pawns.Count; i++)
                {
                    var pawn = pawns[i];
                    if (pawn == null)
                        continue;
                    float width = Text.CalcSize(pawn.LabelCap).x;
                    if (width > widest)
                        widest = width;
                }
            }
            Text.Font = font;
            return Mathf.CeilToInt(widest + 28f);
        }
    }

    [HarmonyPatch(typeof(PawnColumnWorker), "GetOptimalWidth")]
    public static class Patch_NameColumnOptimal
    {
        public static bool Prepare()
        {
            return AccessTools.Method(typeof(PawnColumnWorker), "GetOptimalWidth") != null;
        }

        public static void Postfix(PawnColumnWorker __instance, PawnTable table, ref int __result)
        {
            if (!Patch_NameColumnWidth.IsNameColumn(__instance) || table == null)
                return;
            __result = Patch_NameColumnWidth.NameWidth(table);
        }
    }
}
