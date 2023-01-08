using System.IO;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using IngameDebugConsole;

public static class ParamUtils {
	public static string ParamDir {get {return $"{DepthFileUtils.SaveDir}/params";}}
	public static string DefaultParamPath {get {return $"{ParamDir}/defaultparams.txt";}}

	public static IDepthMesh Dmesh {private get; set;}

	static ParamUtils() {
		Utils.CreateDirectory(ParamDir);
	}

	private static string NormalizeFilename(string basename) {
		if (basename == null)
			return DefaultParamPath;
		else
			return $"{ParamDir}/{basename}.txt";
	}

	[ConsoleMethod("params_export", "Export current parameters for the mesh (under the output dir)")]
	public static void ExportParams(string filename=null) {
		Debug.Log("Exporting parameters...");

		string paramstr = Dmesh.ExportParams();
		File.WriteAllText(NormalizeFilename(filename), paramstr);
	}

	[ConsoleMethod("params_import", "Import the parameters for the mesh (from the output dir)")]
	public static void ImportParams(string filename=null) {
		Debug.Log("Importing parameters...");

		if (filename == null) filename = DefaultParamPath;

		string paramstr = File.ReadAllText(NormalizeFilename(filename));
		Dmesh.ImportParams(paramstr);
	}
}
