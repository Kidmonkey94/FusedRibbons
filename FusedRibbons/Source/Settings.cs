using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FusedRibbons
{
    /// <summary>
    /// Player assignments for modded main buttons. XML still owns the vanilla groups.
    /// A button left on "Bar" is unchanged. Pawns or Animals hides it and adds it to that row.
    /// </summary>
    public class FusedSettings : ModSettings
    {
        public Dictionary<string, string> assigned = new Dictionary<string, string>();
        public List<string> customBars = new List<string>();
        public List<string> memberOrder = new List<string>();
        public List<string> barOrder = new List<string>();
        public bool showHidden;
        public List<string> forcedBar = new List<string>();
        public List<string> disabledBars = new List<string>();
        public HashSet<string> openHeaders = new HashSet<string>();
        private Vector2 scroll;

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref assigned, "assigned", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref customBars, "customBars", LookMode.Value);
            Scribe_Collections.Look(ref memberOrder, "memberOrder", LookMode.Value);
            Scribe_Collections.Look(ref barOrder, "barOrder", LookMode.Value);
            Scribe_Values.Look(ref showHidden, "showHidden");
            Scribe_Collections.Look(ref forcedBar, "forcedBar", LookMode.Value);
            Scribe_Collections.Look(ref disabledBars, "disabledBars", LookMode.Value);
            if (forcedBar == null)
                forcedBar = new List<string>();
            if (disabledBars == null)
                disabledBars = new List<string>();
            if (assigned == null)
                assigned = new Dictionary<string, string>();
            if (customBars == null)
                customBars = new List<string>();
            if (memberOrder == null)
                memberOrder = new List<string>();
            if (barOrder == null)
                barOrder = new List<string>();
            if (openHeaders == null)
                openHeaders = new HashSet<string>();
        }

        public static IEnumerable<string> CustomBars()
        {
            var settings = FusedRibbonsMod.Instance?.GetSettings<FusedSettings>();
            if (settings?.customBars == null)
                yield break;
            foreach (var name in settings.customBars)
                yield return name;
        }

        public static IEnumerable<MainButtonDef> MembersOf(string groupButton)
        {
            var settings = FusedRibbonsMod.Instance?.GetSettings<FusedSettings>();
            if (settings?.assigned == null)
                yield break;
            foreach (var pair in settings.assigned)
            {
                if (pair.Value != groupButton)
                    continue;
                var def = DefDatabase<MainButtonDef>.GetNamedSilentFail(pair.Key);
                if (def != null && def.tabWindowClass != null)
                    yield return def;
            }
        }

        public static void DoWindowContents(Rect inRect)
        {
            var settings = FusedRibbonsMod.Instance.GetSettings<FusedSettings>();
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width - 340f, 32f), "Open a ribbon to order its buttons. Assign orders and hides what is still on the bar.");
            bool show = settings.showHidden;
            Widgets.CheckboxLabeled(new Rect(inRect.xMax - 330f, inRect.y, 160f, 28f), "Show hidden", ref show);
            if (show != settings.showHidden)
            {
                settings.showHidden = show;
                settings.Write();
            }
            if (Widgets.ButtonText(new Rect(inRect.xMax - 160f, inRect.y, 160f, 28f), "Make bar"))
                Find.WindowStack.Add(new Dialog_NameBar(null));
            var view = new Rect(inRect.x, inRect.y + 40f, inRect.width, inRect.height - 40f);
            var groups = RibbonGroups.All.ToList();
            var others = Assignable().ToList();
            int lines = groups.Count + 2;
            if (settings.openHeaders.Contains("Custom"))
                lines += settings.customBars.Count;
            foreach (var group in groups)
            {
                if (settings.openHeaders.Contains(group.ButtonDefName))
                    lines += group.Members.Count;
            }
            if (settings.openHeaders.Contains("Assign"))
                lines += ScreenList(settings).Count;
            var inner = new Rect(0f, 0f, view.width - 16f, Mathf.Max(view.height, lines * 32f + 48f));
            settings.scroll = GUI.BeginScrollView(view, settings.scroll, inner);
            float y = 0f;
            bool customOpen = settings.openHeaders.Contains("Custom");
            if (Widgets.ButtonText(new Rect(0f, y, inner.width, 28f), (customOpen ? "v  " : ">  ") + "Custom bars"))
            {
                if (customOpen)
                    settings.openHeaders.Remove("Custom");
                else
                    settings.openHeaders.Add("Custom");
            }
            y += 32f;
            if (customOpen)
            {
                foreach (var bar in settings.customBars)
                {
                    var line = new Rect(16f, y, inner.width - 16f, 28f);
                    Widgets.Label(new Rect(line.x, line.y, line.width - 250f, line.height), GroupLabel(bar));
                    if (Widgets.ButtonText(new Rect(line.xMax - 244f, line.y, 28f, line.height), "^"))
                        MoveBar(settings, bar, -1);
                    if (Widgets.ButtonText(new Rect(line.xMax - 212f, line.y, 28f, line.height), "v"))
                        MoveBar(settings, bar, 1);
                    bool off = settings.disabledBars.Contains(bar);
                    if (Widgets.ButtonText(new Rect(line.xMax - 174f, line.y, 84f, line.height), off ? "Enable" : "Disable"))
                    {
                        if (off)
                            settings.disabledBars.Remove(bar);
                        else
                            settings.disabledBars.Add(bar);
                        settings.Write();
                    }
                    if (Widgets.ButtonText(new Rect(line.xMax - 84f, line.y, 84f, line.height), "Rename"))
                        Find.WindowStack.Add(new Dialog_NameBar(bar));
                    y += 32f;
                }
            }
            foreach (var group in groups)
            {
                bool open = settings.openHeaders.Contains(group.ButtonDefName);
                bool off = settings.disabledBars.Contains(group.ButtonDefName);
                if (Widgets.ButtonText(new Rect(0f, y, inner.width - 150f, 28f), (open ? "v  " : ">  ") + GroupLabel(group.ButtonDefName)))
                {
                    if (open)
                        settings.openHeaders.Remove(group.ButtonDefName);
                    else
                        settings.openHeaders.Add(group.ButtonDefName);
                }
                if (Widgets.ButtonText(new Rect(inner.width - 146f, y, 28f, 28f), "^"))
                    MoveBar(settings, group.ButtonDefName, -1);
                if (Widgets.ButtonText(new Rect(inner.width - 114f, y, 28f, 28f), "v"))
                    MoveBar(settings, group.ButtonDefName, 1);
                if (Widgets.ButtonText(new Rect(inner.width - 84f, y, 84f, 28f), off ? "Enable" : "Disable"))
                {
                    if (off)
                        settings.disabledBars.Remove(group.ButtonDefName);
                    else
                        settings.disabledBars.Add(group.ButtonDefName);
                    settings.Write();
                }
                y += 32f;
                if (!open)
                    continue;
                foreach (var member in group.Members)
                {
                    var line = new Rect(16f, y, inner.width - 16f, 28f);
                    Widgets.Label(new Rect(line.x, line.y, line.width - 150f, line.height), member.LabelCap);
                    int index = GroupIndex(settings, group.ButtonDefName, member.defName);
                    int count = GroupCount(settings, group.ButtonDefName);
                    if (Widgets.ButtonText(new Rect(line.xMax - 144f, line.y, 28f, line.height), "^") && index > 0)
                        Move(settings, group.ButtonDefName, member.defName, -1);
                    if (Widgets.ButtonText(new Rect(line.xMax - 112f, line.y, 28f, line.height), "v") && index < count - 1)
                        Move(settings, group.ButtonDefName, member.defName, 1);
                    bool xmlMember = group.MemberDefNames != null && group.MemberDefNames.Contains(member.defName);
                    if (!xmlMember && Widgets.ButtonText(new Rect(line.xMax - 78f, line.y, 78f, line.height), "Remove"))
                    {
                        settings.assigned[member.defName] = null;
                        settings.Write();
                    }
                    y += 32f;
                }
            }
            bool assignOpen = settings.openHeaders.Contains("Assign");
            if (Widgets.ButtonText(new Rect(0f, y, inner.width, 28f), (assignOpen ? "v  " : ">  ") + "Assign"))
            {
                if (assignOpen)
                    settings.openHeaders.Remove("Assign");
                else
                    settings.openHeaders.Add("Assign");
            }
            y += 32f;
            if (assignOpen)
            {
                var barButtons = ScreenList(settings);
                for (int i = 0; i < barButtons.Count; i++)
                {
                    var def = barButtons[i];
                    var line = new Rect(16f, y, inner.width - 16f, 28f);
                    Widgets.Label(new Rect(line.x, line.y, line.width - 180f, line.height), def.LabelCap);
                    bool shown = OnRibbon(settings, def);
                    Widgets.Checkbox(new Vector2(line.xMax - 176f, line.y + 2f), ref shown, 24f);
                    if (shown != OnRibbon(settings, def))
                    {
                        if (shown)
                        {
                            settings.assigned[def.defName] = null;
                            if (def.Worker == null || !def.Worker.Visible)
                                settings.forcedBar.Add(def.defName);
                        }
                        else
                        {
                            settings.assigned[def.defName] = "Hidden";
                            settings.forcedBar.Remove(def.defName);
                        }
                        settings.Write();
                    }
                    if (Widgets.ButtonText(new Rect(line.xMax - 144f, line.y, 28f, line.height), "^") && i > 0)
                        MoveBar(settings, def.defName, -1);
                    if (Widgets.ButtonText(new Rect(line.xMax - 112f, line.y, 28f, line.height), "v") && i < barButtons.Count - 1)
                        MoveBar(settings, def.defName, 1);
                    string group = null;
                    settings.assigned.TryGetValue(def.defName, out group);
                    if (Widgets.ButtonText(new Rect(line.xMax - 78f, line.y, 78f, line.height), GroupLabel(group)))
                        OpenGroupMenu(settings, def);
                    y += 32f;
                }
            }
            GUI.EndScrollView();
        }

        private struct RibbonRow
        {
            public string Group;
            public MainButtonDef Def;
        }

        public static int Rank(string group, string defName)
        {
            var settings = FusedRibbonsMod.Instance?.GetSettings<FusedSettings>();
            if (settings?.memberOrder == null)
                return 1000;
            int index = settings.memberOrder.IndexOf(group + "|" + defName);
            return index < 0 ? 1000 : index;
        }

        private static void Move(FusedSettings settings, string group, string defName, int dir)
        {
            EnsureOrder(settings, group);
            var keys = settings.memberOrder.Where(item => item.StartsWith(group + "|")).ToList();
            string key = group + "|" + defName;
            int index = keys.IndexOf(key);
            int next = index + dir;
            if (index < 0 || next < 0 || next >= keys.Count)
                return;
            int from = settings.memberOrder.IndexOf(keys[index]);
            int to = settings.memberOrder.IndexOf(keys[next]);
            string swap = settings.memberOrder[to];
            settings.memberOrder[to] = settings.memberOrder[from];
            settings.memberOrder[from] = swap;
            settings.Write();
        }

        private static void EnsureOrder(FusedSettings settings, string group)
        {
            var ribbon = RibbonGroups.All.FirstOrDefault(item => item.ButtonDefName == group);
            if (ribbon == null)
                return;
            foreach (var member in ribbon.Members)
            {
                string key = group + "|" + member.defName;
                if (!settings.memberOrder.Contains(key))
                    settings.memberOrder.Add(key);
            }
        }

        private static int GroupIndex(FusedSettings settings, string group, string defName)
        {
            EnsureOrder(settings, group);
            return settings.memberOrder.Where(item => item.StartsWith(group + "|")).ToList().IndexOf(group + "|" + defName);
        }

        private static int GroupCount(FusedSettings settings, string group)
        {
            EnsureOrder(settings, group);
            return settings.memberOrder.Count(item => item.StartsWith(group + "|"));
        }

        public static List<string> BarList(FusedSettings settings)
        {
            var bars = new List<string>();
            foreach (var def in OnBar(settings))
                bars.Add(def.defName);
            if (!bars.Contains("Architect"))
                bars.Insert(0, "Architect");
            var ordered = new List<string>();
            foreach (var name in settings.barOrder)
            {
                bool kept = bars.Contains(name) || settings.disabledBars.Contains(name) || name.StartsWith("Fused");
                if (kept && !ordered.Contains(name))
                    ordered.Add(name);
            }
            var core = new List<string>();
            var modded = new List<string>();
            foreach (var name in bars)
            {
                if (ordered.Contains(name))
                    continue;
                var def = DefDatabase<MainButtonDef>.GetNamedSilentFail(name);
                if (def?.modContentPack != null && def.modContentPack.IsCoreMod)
                    core.Add(name);
                else if (name.StartsWith("Fused"))
                    core.Add(name);
                else
                    modded.Add(name);
            }
            ordered.AddRange(core);
            ordered.AddRange(modded);
            return ordered;
        }

        private static List<MainButtonDef> OnBar(FusedSettings settings)
        {
            var grouped = new HashSet<string>();
            foreach (var group in RibbonGroups.All)
            {
                foreach (var member in group.Members)
                    grouped.Add(member.defName);
            }
            return DefDatabase<MainButtonDef>.AllDefsListForReading
                .Where(def => def.defName != null && def.Worker != null && def.Worker.Visible && !grouped.Contains(def.defName))
                .OrderBy(def => def.order)
                .ToList();
        }

        private static bool OnRibbon(FusedSettings settings, MainButtonDef def)
        {
            if (FusedSettings.IsHidden(def))
                return false;
            if (settings.forcedBar.Contains(def.defName))
                return true;
            return def.Worker != null && def.Worker.Visible;
        }

        private static List<MainButtonDef> ScreenList(FusedSettings settings)
        {
            var list = new List<MainButtonDef>();
            foreach (var name in BarList(settings))
            {
                var def = DefDatabase<MainButtonDef>.GetNamedSilentFail(name);
                if (def != null)
                    list.Add(def);
            }
            foreach (var def in Assignable())
            {
                if (!list.Any(item => item.defName == def.defName))
                    list.Add(def);
            }
            return list;
        }

        private static void MoveBar(FusedSettings settings, string defName, int dir)
        {
            var bars = ScreenList(settings).Select(def => def.defName).ToList();
            int index = bars.IndexOf(defName);
            int next = index + dir;
            if (index < 0 || next < 0 || next >= bars.Count)
                return;
            string swap = bars[index];
            bars[index] = bars[next];
            bars[next] = swap;
            settings.barOrder = bars;
            settings.Write();
            CustomBarMaker.ApplyOrder();
        }

        private static IEnumerable<RibbonRow> RibbonRows()
        {
            foreach (var group in RibbonGroups.All)
            {
                foreach (var member in group.Members)
                    yield return new RibbonRow { Group = group.ButtonDefName, Def = member };
            }
        }

        private static IEnumerable<MainButtonDef> Assignable()
        {
            var grouped = new HashSet<string>();
            foreach (var group in RibbonGroups.All)
            {
                grouped.Add(group.ButtonDefName);
                foreach (var member in group.Members)
                    grouped.Add(member.defName);
            }
            return DefDatabase<MainButtonDef>.AllDefsListForReading
                .Where(def => def.defName != null && !grouped.Contains(def.defName))
                .OrderBy(def => AssignRank(def))
                .ThenBy(def => def.LabelCap.RawText);
        }

        private static int AssignRank(MainButtonDef def)
        {
            var settings = FusedRibbonsMod.Instance?.GetSettings<FusedSettings>();
            if (settings?.barOrder != null)
            {
                int index = settings.barOrder.IndexOf(def.defName);
                if (index >= 0)
                    return index;
            }
            if (def.defName == "Architect")
                return 1000 + (int)def.order;
            if (def.modContentPack != null && def.modContentPack.IsCoreMod)
                return 1000 + (int)def.order;
            return 5000 + (int)def.order;
        }

        private static void OpenGroupMenu(FusedSettings settings, MainButtonDef def)
        {
            var menu = new List<FloatMenuOption>
            {
                new FloatMenuOption("Bar", () => Assign(settings, def, null)),
                new FloatMenuOption("Pawns", () => Assign(settings, def, "FusedPawns")),
                new FloatMenuOption("Animals", () => Assign(settings, def, "FusedAnimals"))
            };
            foreach (var bar in CustomBars())
            {
                string captured = bar;
                menu.Add(new FloatMenuOption(BarLabel(captured), () => Assign(settings, def, captured)));
            }
            Find.WindowStack.Add(new FloatMenu(menu));
        }

        private static void Assign(FusedSettings settings, MainButtonDef def, string group)
        {
            settings.assigned[def.defName] = group;
            settings.Write();
        }

        private static string GroupLabel(string group)
        {
            if (group == "FusedPawns")
                return "Pawns";
            if (group == "FusedAnimals")
                return "Animals";
            if (group == "Hidden")
                return "Hidden";
            if (group != null && group.StartsWith("FusedCustom_"))
                return BarLabel(group);
            return "Bar";
        }

        public static string BarLabel(string defName)
        {
            if (defName != null && defName.StartsWith("FusedCustom_"))
                return defName.Substring("FusedCustom_".Length);
            return defName;
        }

        public static bool IsBarDisabled(MainButtonDef def)
        {
            if (def == null)
                return false;
            var settings = FusedRibbonsMod.Instance?.GetSettings<FusedSettings>();
            return settings != null
                && !settings.showHidden
                && settings.disabledBars != null
                && settings.disabledBars.Contains(def.defName);
        }

        public static bool ParentDisabled(MainButtonDef def)
        {
            var group = RibbonGroups.ForMember(def);
            if (group == null)
                return false;
            var parent = DefDatabase<MainButtonDef>.GetNamedSilentFail(group.ButtonDefName);
            return IsBarDisabled(parent);
        }

        public static bool IsHidden(MainButtonDef def)
        {
            if (def == null)
                return false;
            var settings = FusedRibbonsMod.Instance?.GetSettings<FusedSettings>();
            return settings != null
                && !settings.showHidden
                && settings.assigned.TryGetValue(def.defName, out var group)
                && group == "Hidden";
        }

        public static bool ForceVisible(MainButtonDef def)
        {
            if (def == null || RibbonGroups.ForMember(def) != null)
                return false;
            var settings = FusedRibbonsMod.Instance?.GetSettings<FusedSettings>();
            if (settings == null)
                return false;
            if (settings.showHidden)
                return true;
            return settings.forcedBar != null && settings.forcedBar.Contains(def.defName);
        }
    }

    public class Dialog_NameBar : Window
    {
        private string name;
        private readonly string existing;

        public Dialog_NameBar(string existing)
        {
            this.existing = existing;
            name = existing == null ? "Custom" : FusedSettings.BarLabel(existing);
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
        }

        public override Vector2 InitialSize => new Vector2(360f, 150f);

        public override void DoWindowContents(Rect inRect)
        {
            Widgets.Label(new Rect(0f, 0f, inRect.width, 24f), existing == null ? "Name the new bar." : "Rename this bar.");
            name = Widgets.TextField(new Rect(0f, 30f, inRect.width, 28f), name);
            if (Widgets.ButtonText(new Rect(0f, 70f, 140f, 28f), existing == null ? "Make bar" : "Rename"))
            {
                var settings = FusedRibbonsMod.Instance.GetSettings<FusedSettings>();
                string clean = "";
                foreach (char c in name ?? "")
                {
                    if (char.IsLetterOrDigit(c))
                        clean += c;
                }
                if (clean.Length == 0)
                    clean = "Custom";
                string defName = "FusedCustom_" + clean;
                if (existing == null)
                {
                    if (!settings.customBars.Contains(defName))
                        settings.customBars.Add(defName);
                }
                else
                {
                    int index = settings.customBars.IndexOf(existing);
                    if (index >= 0)
                        settings.customBars[index] = defName;
                    var keys = settings.assigned.Keys.ToList();
                    foreach (var key in keys)
                    {
                        if (settings.assigned[key] == existing)
                            settings.assigned[key] = defName;
                    }
                }
                settings.Write();
                Messages.Message("Restart for the " + clean + " bar to appear.", MessageTypeDefOf.TaskCompletion, false);
                Close();
            }
        }
    }

    [StaticConstructorOnStartup]
    public static class CustomBarMaker
    {
        static CustomBarMaker()
        {
            LongEventHandler.ExecuteWhenFinished(Ensure);
        }

        public static void Ensure()
        {
            int order = 60;
            foreach (var defName in FusedSettings.CustomBars())
            {
                if (DefDatabase<MainButtonDef>.GetNamedSilentFail(defName) != null)
                    continue;
                var def = new MainButtonDef();
                def.defName = defName;
                def.label = FusedSettings.BarLabel(defName);
                def.description = "Custom ribbon.";
                def.workerClass = typeof(MainButtonWorker_RibbonGroup);
                def.tabWindowClass = typeof(MainTabWindow_RibbonHost);
                def.order = order++;
                def.closesWorldView = true;
                DefDatabase<MainButtonDef>.Add(def);
            }
            ApplyOrder();
        }

        public static void ApplyOrder()
        {
            var settings = FusedRibbonsMod.Instance?.GetSettings<FusedSettings>();
            if (settings == null)
                return;
            var bars = FusedSettings.BarList(settings);
            for (int i = 0; i < bars.Count; i++)
            {
                var def = DefDatabase<MainButtonDef>.GetNamedSilentFail(bars[i]);
                if (def != null)
                    def.order = 10 + i * 10;
            }
            SortLiveBar();
        }

        private static void SortLiveBar()
        {
            var listField = AccessTools.Field(typeof(DefDatabase<MainButtonDef>), "defsList");
            if (listField?.GetValue(null) is IList defs && defs.Count > 1)
            {
                var sorted = defs.Cast<MainButtonDef>().OrderBy(def => def.order).ToList();
                defs.Clear();
                foreach (var def in sorted)
                    defs.Add(def);
            }
            var root = Find.MainButtonsRoot;
            if (root == null)
                return;
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var field in root.GetType().GetFields(flags))
            {
                if (!(field.GetValue(root) is IList list) || list.Count == 0 || !(list[0] is MainButtonDef))
                    continue;
                var sorted = list.Cast<MainButtonDef>().OrderBy(def => def.order).ToList();
                list.Clear();
                foreach (var def in sorted)
                    list.Add(def);
            }
        }
    }
}
