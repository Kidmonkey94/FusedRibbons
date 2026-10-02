# Fused Ribbons

RimWorld mod. Fuses Work, Schedule, Assign, and Mechs into a Pawns ribbon, and Animals and Wildlife into an Animals ribbon. Other main buttons can be assigned to a row, hidden, or given a new bar from the mod settings.

The real tabs are opened, not copied. Hotkeys on those buttons are left alone.

Incompatible with Reorderer: both mods write the same bar order.

Requires [Harmony](https://steamcommunity.com/workshop/filedetails/?id=2009463077). 

## Known limits

The last vanilla work column clips its own header. That is the game's column, not this mod. Better Work Tab or Compact Work Tab (continued) draws that header in full.

Grouped Pawns Lists still has its Wildlife cog. Resizing that window to clear the ribbon hides the cog, so the window is left as that mod draws it. Collapse a few groups and the cog comes back.

## Build

From `Source`:

```powershell
dotnet build -c Release
