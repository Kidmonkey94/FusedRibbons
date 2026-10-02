using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace FusedRibbons
{
    /// <summary>
    /// Data for one parent tab. Add another of these in XML and a MainButtonDef
    /// that uses MainTabWindow_RibbonHost. No vanilla def is edited.
    /// </summary>
    public class RibbonGroupDef : Def
    {
        public string buttonDef;
        public List<string> members;
    }

    public class RibbonGroup
    {
        public string ButtonDefName;
        public List<string> MemberDefNames;

        public bool Contains(string defName)
        {
            if (defName == null)
                return false;
            if (MemberDefNames != null && MemberDefNames.Contains(defName))
                return true;
            return FusedSettings.MembersOf(ButtonDefName).Any(def => def.defName == defName);
        }

        public List<MainButtonDef> Members
        {
            get
            {
                var list = new List<MainButtonDef>();
                if (MemberDefNames != null)
                {
                    foreach (var name in MemberDefNames)
                    {
                        var def = DefDatabase<MainButtonDef>.GetNamedSilentFail(name);
                        if (def != null)
                            list.Add(def);
                    }
                }
                foreach (var extra in FusedSettings.MembersOf(ButtonDefName))
                {
                    if (!list.Any(member => member.defName == extra.defName))
                        list.Add(extra);
                }
                return list
                    .OrderBy(def => FusedSettings.Rank(ButtonDefName, def.defName))
                    .ThenBy(def => def.defName)
                    .ToList();
            }
        }
    }

    public static class RibbonGroups
    {
        public static IEnumerable<RibbonGroup> All
        {
            get
            {
                if (DefDatabase<RibbonGroupDef>.AllDefsListForReading.Count > 0)
                {
                    foreach (var def in DefDatabase<RibbonGroupDef>.AllDefsListForReading)
                    {
                        yield return new RibbonGroup
                        {
                            ButtonDefName = def.buttonDef,
                            MemberDefNames = def.members
                        };
                    }
                }
                else
                {
                    yield return new RibbonGroup
                    {
                        ButtonDefName = "FusedPawns",
                        MemberDefNames = new List<string> { "Work", "Schedule", "Assign", "Mechs" }
                    };
                    yield return new RibbonGroup
                    {
                        ButtonDefName = "FusedAnimals",
                        MemberDefNames = new List<string> { "Animals", "Wildlife" }
                    };
                }

                foreach (var bar in FusedSettings.CustomBars())
                {
                    yield return new RibbonGroup
                    {
                        ButtonDefName = bar,
                        MemberDefNames = new List<string>()
                    };
                }
            }
        }

        public static RibbonGroup ForButton(MainButtonDef button)
        {
            if (button == null)
                return null;
            return All.FirstOrDefault(group => group.ButtonDefName == button.defName);
        }

        public static RibbonGroup ForMember(MainButtonDef member)
        {
            if (member == null)
                return null;
            var xml = All.FirstOrDefault(group => group.MemberDefNames != null && group.MemberDefNames.Contains(member.defName));
            if (xml != null)
                return xml;
            return All.FirstOrDefault(group => FusedSettings.MembersOf(group.ButtonDefName).Any(def => def.defName == member.defName));
        }
    }
}
