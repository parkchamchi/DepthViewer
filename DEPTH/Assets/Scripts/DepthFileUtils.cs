using System.Collections;
using System.Collections.Generic;
using System;
using System.IO;
using System.IO.Compression;
using System.Text;

using UnityEngine;



public static class DepthFileUtils {
	public const string Version = "PRE-RELEASE";
	public static readonly string DefaultDepthDir;
	public const string DepthExt = ".depthviewer";

	static DepthFileUtils() {
		DefaultDepthDir = Application.persistentDataPath + "/depths";
		if (!Directory.Exists(DefaultDepthDir))
			Directory.CreateDirectory(DefaultDepthDir);
	}

	public static void DumpDepthFile(float[][] depths_frames, long startframe, string hashval, string orig_basename, int orig_width, int orig_height, int x, int y, string model_type, string weight) {
		/*
		Args:
			hashval: hash value (see Utils)
			orig_basename: filename of original image (need not be the full path)
			orig_width, orig_height: size of ORIGINAL IMAGE INPUT.
			x, y: size of DEPTH.
			model_type: model used. (see DepthONNXBehavior.ModelType)
			weight: basename of the model (e.g. "MiDaS_model-small.onnx")
		*/

		orig_basename = Path.GetFileName(orig_basename);

		string metadata = WriteMetadata(
			hashval: hashval,
			framecount: depths_frames.Length.ToString(),
			startframe: startframe.ToString(),
			width: x.ToString(),
			height: y.ToString(),
			model_type: model_type,
			weight: weight,
			original_name: orig_basename,
			original_width: orig_width.ToString(),
			original_height: orig_height.ToString(),
			timestamp: DateTimeOffset.Now.ToUnixTimeSeconds().ToString(),
			version: Version
		);

		string output_filepath = $"{orig_basename}.model=`{model_type}`.{hashval}{DepthExt}";
		if (output_filepath.Length > 250) //if it's too long, omit the orig basename
			output_filepath = $"model=`{model_type}`.{hashval}{DepthExt}";
		output_filepath = DefaultDepthDir + '/' + output_filepath;

		UpdateDepthFile(output_filepath, depths_frames, x, y, metadata);
	}

	public static void UpdateDepthFile(string depthfilepath, float[][] depths_frames, int x, int y) {
		UpdateDepthFile(depthfilepath, depths_frames, x, y, null);
	}

	public static void UpdateDepthFile(string depthfilepath, float[][] depths_frames, int x, int y, string metadata) {
		using (ZipArchive archive = ZipFile.Open(depthfilepath, ZipArchiveMode.Update)) {
			//Write the metadata, if it's not null
			if (metadata != null) {
				ZipArchiveEntry metadataEntry = archive.CreateEntry("METADATA.txt");
				using (StreamWriter sw = new StreamWriter(metadataEntry.Open()))
					sw.Write(metadata);
			}

			//Write the new depths
			for (int i = 0; i < depths_frames.Length; i++) {
				if (depths_frames[i] == null) continue;

				ZipArchiveEntry entry = archive.GetEntry(string.Format("{0}.pgm", i));
				if (entry == null) 
					entry = archive.CreateEntry($"{i}.pgm");
				using (BinaryWriter bw = new BinaryWriter(entry.Open()))
					bw.Write(WritePGM(depths_frames[i], x, y));
			}
		}
	}

	public static List<string> ProcessedDepthFileExists(string hashval) {
		/* 
			Returns a list of paths of processed depth files.
		*/
		List<string> filelist = new List<string>();

		foreach (string filename in Directory.GetFiles(DefaultDepthDir))
			if (filename.EndsWith($"{hashval}{DepthExt}"))
				filelist.Add(filename);

		return filelist;
	}

	public static string WriteMetadata(string hashval, string framecount, string startframe, string width, string height, string model_type, string weight, 
		string original_name, string original_width, string original_height, string timestamp, string version) {
		/*
		A line per a field, delimited by the initial '='
		*/

		string metadata = "DEPTHVIEWER FILE\n"
			+ $"hashval={hashval}\n"
			+ $"framecount={framecount}\n"
			+ $"startframe={startframe}\n"
			+ $"width={width}\n"
			+ $"height={height}\n"
			+ $"model_type={model_type}\n"
			+ $"weight={weight}\n"
			+ $"original_name={original_name}\n"
			+ $"original_width={original_width}\n"
			+ $"original_height={original_height}\n"
			+ $"timestamp={timestamp}\n"
			+ $"version={version}\n";
		
		return metadata;
	}

	public static byte[] WritePGM(float[] depths, int x, int y) {
		byte[] header = Encoding.ASCII.GetBytes($"P5\n{x} {y} 255\n");

		byte[] content = new byte[depths.Length];
		for (int i = 0; i < content.Length; i++)
			content[i] = (byte) (depths[i] * 255);

		byte[] pgm = new byte[header.Length + content.Length];
		header.CopyTo(pgm, 0);
		content.CopyTo(pgm, header.Length);

		return pgm;
	}

	public static float[][] ReadDepthFile(string path, out int x, out int y, out Dictionary<string, string> metadata) {
		/*
			out x, y: pixel count of DEPTH.
			out orig_ratio: ratio of ORIGINAL INPUT.
		*/
		
		float[][] depths;
		x = y = 0;
		metadata = null;

		if (!path.EndsWith(DepthExt)) {
			Debug.LogError("File " + path + " is not a valid format.");
			return null;
		}

		if (!File.Exists(path)) {
			Debug.LogError("File " + path + " does not exist.");
			return null;
		}

		using (ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Read)) {
			//Read the metadata
			string metadataStr;
			ZipArchiveEntry metadataEntry = archive.GetEntry("METADATA.txt");
			using (StreamReader br = new StreamReader(metadataEntry.Open()))
				metadataStr = br.ReadToEnd();
			metadata = ReadMetadata(metadataStr);
			
			x = int.Parse(metadata["width"]);
			y = int.Parse(metadata["height"]);

			int framecount = int.Parse(metadata["framecount"]);
			depths = new float[framecount][];

			//Read the frames
			for (int i = 0; i < framecount; i++) {
				byte[] pgm;
				ZipArchiveEntry entry = archive.GetEntry(string.Format("{0}.pgm", i));
				if (entry == null) continue;

				pgm = new byte[entry.Length];

				using (BinaryReader br = new BinaryReader(entry.Open()))
					pgm = br.ReadBytes(pgm.Length);
				
				depths[i] = ReadPGM(pgm);
			}

			return depths;
		}
	}

	public static Dictionary<string, string> ReadMetadata(string metadataStr) {
		Dictionary<string, string> metadata = new Dictionary<string, string>();

		foreach (string line in metadataStr.Split('\n')) {
			//Skip non-value lines
			int delimIdx = line.IndexOf('=');
			if (delimIdx < 0)
				continue;

			string key = line.Substring(0, delimIdx);
			string value = line.Substring(delimIdx+1, line.Length-delimIdx-1);

			//Add to the dictionary
			metadata.Add(key.Trim(), value.Trim());
		}

		return metadata;
	}

	public static float[] ReadPGM(byte[] pgm) {
		return ReadPGM(pgm, out _, out _);
	}

	public static float[] ReadPGM(byte[] pgm, out int x, out int y) {
		int idx = 0;
		int width = 0, height = 0, maxval = 0;
		x = y = 0;
		
		//"P5"
		if (pgm[0] != 'P' || pgm[1] != '5') {
			Debug.LogError("Invalid PGM Header detected.");
			return null;
		}
		idx = 2;

		//Skip whitespaces
		for (; IsSpace(pgm[idx]); idx++);

		//width
		for (; !IsSpace(pgm[idx]); idx++) {
			width *= 10;
			width += pgm[idx] - '0';
		}

		//Skip whitespaces
		for (; IsSpace(pgm[idx]); idx++);

		//height
		for (; !IsSpace(pgm[idx]); idx++) {
			height *= 10;
			height += pgm[idx] - '0';
		}

		//Skip whitespaces
		for (; IsSpace(pgm[idx]); idx++);

		//Maxval
		for (; !IsSpace(pgm[idx]); idx++) {
			maxval *= 10;
			maxval += pgm[idx] - '0';
		}
		
		//Skip a single whitespace
		if (!IsSpace(pgm[idx++])) {
			Debug.LogError("Illegal PGM format: there is no whitespace next to the maxval.");
		}

		//The rest is the sequence of uint8 bytes
		if (width*height != pgm.Length-idx)
			Debug.LogWarningFormat("PGM: {0}x{1} = {2} does not match remaining {3} bytes", width, height, width*height, pgm.Length-idx);

		float[] depths = new float[width*height];
		for (int j = 0; j < depths.Length; j++)
			depths[j] = (float) pgm[idx++] / maxval;

		x = width;
		y = height;
		return depths;
	}

	public static bool IsSpace(char c) {
		const string whitespaces = " \t\n\r";
		return whitespaces.IndexOf(c) != -1;
	}

	public static bool IsSpace(byte c) {
		return IsSpace((char) c);
	}

	public static bool IsSpace(int c) {
		return IsSpace((char) c);
	}

}