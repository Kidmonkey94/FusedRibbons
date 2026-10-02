using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FusedRibbons
{
    /// <summary>
    /// Opens the real Work / Schedule / Assign tab. Hosting those windows ate their
    /// scrollbar and clipped the last column. On this game OpenTab is a MainButtonDef.
    /// </summary>
    public static class TabAccess
    {
        public static RibbonGroup HeldGroup;
        public static MainButtonDef HeldMember;

        public static MainButtonDef OpenDef()
        {
            var root = Find.MainTabsRoot;
            if (root == null)
                return null;
            var prop = AccessTools.Property(root.GetType(), "OpenTab");
            if (prop != null)
                return prop.GetValue(root) as MainButtonDef;
            return AccessTools.Field(root.GetType(), "OpenTab")?.GetValue(root) as MainButtonDef;
        }

        public static MainTabWindow FindMemberWindow(RibbonGroup group)
        {
            if (group == null || Find.WindowStack == null)
                return null;
            foreach (var window in Find.WindowStack.Windows)
            {
                if (window is MainTabWindow tab && tab.def != null && group.Contains(tab.def.defName))
                    return tab;
            }
            return null;
        }

        public static void Open(MainButtonDef def)
        {
            var root = Find.MainTabsRoot;
            if (root == null || def == null)
                return;
            HeldMember = def;
            var set = AccessTools.Method(root.GetType(), "SetCurrentTab");
            if (set != null)
            {
                Invoke(set, root, def);
                return;
            }
            var toggle = AccessTools.Method(root.GetType(), "ToggleTab");
            if (toggle != null)
                Invoke(toggle, root, def);
            else
                def.Worker?.Activate();
        }

        public static void Close(RibbonGroup group)
        {
            var open = OpenDef();
            var root = Find.MainTabsRoot;
            if (root != null && open != null && group != null && group.MemberDefNames.Contains(open.defName))
            {
                var toggle = AccessTools.Method(root.GetType(), "ToggleTab");
                if (toggle != null)
                    Invoke(toggle, root, open);
            }
            var window = FindMemberWindow(group);
            if (window != null)
                window.Close();
            HeldGroup = null;
            HeldMember = null;
            RibbonSelectorWindow.CloseOpen();
        }

        private static void Invoke(MethodInfo method, object root, MainButtonDef def)
        {
            if (method.GetParameters().Length == 1)
                method.Invoke(root, new object[] { def });
            else
                method.Invoke(root, new object[] { def, true });
        }
    }

    public class MainButtonWorker_RibbonGroup : MainButtonWorker
    {
        public override void Activate()
        {
            var group = RibbonGroups.ForButton(def);
            if (group == null)
                return;
            var members = group.Members;
            if (members.Count == 0)
                return;

            if (TabAccess.HeldGroup == group || TabAccess.FindMemberWindow(group) != null)
            {
                TabAccess.Close(group);
                return;
            }

            OpenGroup(group);
        }

        public static void OpenGroup(RibbonGroup group)
        {
            var members = group.Members;
            if (members.Count == 0)
                return;
            TabAccess.HeldGroup = group;
            TabAccess.Open(members[0]);
            if (RibbonSelectorWindow.Open == null)
                Find.WindowStack.Add(new RibbonSelectorWindow(group));
            else
                RibbonSelectorWindow.Open.SetGroup(group);
        }
    }

    /// <summary>
    /// Present so the bottom-bar button has a window to construct. It does not draw
    /// the table. The game was calling ToggleTab and adding null after this class
    /// was removed.
    /// </summary>
    public class MainTabWindow_RibbonHost : MainTabWindow
    {
        public override void DoWindowContents(Rect inRect)
        {
        }

        public override void PostOpen()
        {
            base.PostOpen();
            var group = RibbonGroups.ForButton(def);
            if (group != null)
                MainButtonWorker_RibbonGroup.OpenGroup(group);
            Close();
        }
    }

    /// <summary>
    /// Sits above the real tab. Clicks switch that tab; they do not draw it.
    /// </summary>
    public class RibbonSelectorWindow : Window
    {
        public static RibbonSelectorWindow Open;

        private RibbonGroup group;

        public RibbonSelectorWindow(RibbonGroup group)
        {
            this.group = group;
            Open = this;
            doCloseX = false;
            doCloseButton = false;
            closeOnClickedOutside = false;
            closeOnAccept = false;
            closeOnCancel = false;
            absorbInputAroundWindow = false;
            draggable = false;
            resizeable = false;
            focusWhenOpened = false;
            preventCameraMotion = false;
            layer = WindowLayer.Dialog;
        }

        protected override float Margin => 0f;

        public static void CloseOpen()
        {
            if (Open == null)
                return;
            Open.Close();
            Open = null;
        }

        public static bool MouseOver()
        {
            return Open != null && Event.current != null && Open.windowRect.Contains(Event.current.mousePosition);
        }

        public void SetGroup(RibbonGroup next)
        {
            group = next;
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();
            var tab = TabAccess.FindMemberWindow(group);
            if (tab == null)
            {
                var open = TabAccess.OpenDef();
                if (open == null || !group.Contains(open.defName))
                {
                    if (Open == this)
                        CloseOpen();
                    TabAccess.HeldGroup = null;
                }
                return;
            }
            if (tab.def != null && tab.def.defName == "Ideos" && tab.windowRect.y < 40f)
            {
                var rect = tab.windowRect;
                float shift = 40f - rect.y;
                rect.y = 40f;
                rect.height -= shift;
                tab.windowRect = rect;
            }
            float y = tab.windowRect.y - 36f;
            if (y < 4f)
                y = 4f;
            windowRect = new Rect(tab.windowRect.x, y, tab.windowRect.width, 32f);
        }

        public static void ClampTab(MainTabWindow tab)
        {
        }

        private static MainTabWindow widened;

        public static void FitWork(MainTabWindow tab)
        {
            if (tab?.def == null || tab.def.defName != "Work" || widened == tab)
                return;
            var rect = tab.windowRect;
            rect.x -= 32f;
            rect.width += 32f;
            if (rect.x < 0f)
            {
                rect.width += rect.x;
                rect.x = 0f;
            }
            tab.windowRect = rect;
            widened = tab;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Widgets.DrawBoxSolid(inRect, new Color(0.08f, 0.08f, 0.08f, 0.94f));
            var members = group.Members;
            if (members.Count == 0)
                return;
            float gap = 4f;
            float buttonW = (inRect.width - gap * (members.Count - 1)) / members.Count;
            float x = inRect.x;
            var active = TabAccess.OpenDef() ?? TabAccess.HeldMember;
            foreach (var member in members)
            {
                var button = new Rect(x, inRect.y, buttonW, inRect.height);
                if (GUI.Button(button, member.LabelCap) && member != active)
                {
                    if (member.tabWindowClass == null)
                        continue;
                    TabAccess.HeldGroup = group;
                    TabAccess.Open(member);
                }
                x += buttonW + gap;
            }
        }

        public override void PostClose()
        {
            if (Open == this)
                Open = null;
            base.PostClose();
        }
    }
}
