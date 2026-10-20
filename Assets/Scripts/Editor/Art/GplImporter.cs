using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;
using BellwortBurrow.Data;

namespace BellwortBurrow.EditorTools
{
    /// <summary>
    /// Imports every .gpl palette as a Ramp Palette: one ramp per row of Columns colors, named by what its colors' names
    /// share. The characters palette drives player recoloring (sys-6), so saving the palette from Aseprite is all it
    /// takes for the game to see a changed or added ramp.
    /// </summary>
    [ScriptedImporter(1, "gpl")]
    public class GplImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var palette = ScriptableObject.CreateInstance<RampPalette>();
            palette.name = Path.GetFileNameWithoutExtension(ctx.assetPath);
            palette.SetRamps(RampPalette.Parse(File.ReadAllText(ctx.assetPath)));
            ctx.AddObjectToAsset("palette", palette);
            ctx.SetMainObject(palette);
        }
    }
}
