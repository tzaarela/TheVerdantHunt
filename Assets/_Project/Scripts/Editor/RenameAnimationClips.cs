using UnityEngine;
using UnityEditor;
using System.IO;

public class RenameAnimationClips : AssetPostprocessor
{
	[MenuItem("Tools/Rename Mixamo Animation Clips")]
	public static void RenameAllMixamoClips()
	{
		string[] guids = AssetDatabase.FindAssets("t:Model");
		int renamedCount = 0;

		foreach (string guid in guids)
		{
			string assetPath = AssetDatabase.GUIDToAssetPath(guid);

			if (!assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
				continue;

			ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
			if (importer == null)
				continue;

			ModelImporterClipAnimation[] clips = importer.clipAnimations;

			// If no custom clips defined yet, pull from the defaults
			if (clips.Length == 0)
				clips = importer.defaultClipAnimations;

			if (clips.Length == 0)
				continue;

			string fbxName = Path.GetFileNameWithoutExtension(assetPath);
			bool changed = false;

			for (int i = 0; i < clips.Length; i++)
			{
				if (clips[i].name == "mixamo.com" || clips[i].name != fbxName)
				{
					// Only rename clips that are still called "mixamo.com"
					if (clips[i].name == "mixamo.com")
					{
						clips[i].name = fbxName;
						changed = true;
					}
				}
			}

			if (changed)
			{
				importer.clipAnimations = clips;
				importer.SaveAndReimport();
				renamedCount++;
				Debug.Log($"Renamed clip in: {assetPath} → {fbxName}");
			}
		}

		Debug.Log($"Done! Renamed clips in {renamedCount} FBX file(s).");
	}
}
