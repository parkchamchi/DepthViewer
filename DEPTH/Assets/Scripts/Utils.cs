using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.IO;
using System.IO.Compression;

public class Utils {

	public static Texture2D LoadImage(string path) {
		if (!File.Exists(path)) {
			Debug.LogError("File " + path + " does not exist.");
			return null;
		}
		byte[] byteArr = File.ReadAllBytes(path);

		Texture2D texture = new Texture2D(0, 0);
		bool isLoaded = texture.LoadImage(byteArr);

		if (isLoaded)
			return texture;
		else
			return null;
	}

	public static float[][] ReadDepth(string path, out int x, out int y, out double orig_ratio) {
		/*
			out x, y: pixel count of DEPTH.
			out orig_ratio: ratio of ORIGINAL INPUT.
		*/

		const string ext = ".zip"; //valid ext
		
		float[][] depths;
		orig_ratio = x = y = 0;

		if (!path.EndsWith(ext)) {
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
			using (StreamReader br = new StreamReader(metadataEntry.Open())) {
				metadataStr = br.ReadToEnd();
			}
			Dictionary<string, string> metadata = ReadMetadata(metadataStr);
			
			x = int.Parse(metadata["width"]);
			y = int.Parse(metadata["height"]);
			orig_ratio = int.Parse(metadata["original_width"]) / int.Parse(metadata["original_height"]);

			int framecount = int.Parse(metadata["framecount"]);
			depths = new float[framecount][];

			//Read the frames
			for (int i = 0; i < framecount; i++) {
				byte[] pgm;
				ZipArchiveEntry entry = archive.GetEntry(string.Format("{0}.pgm", i));
				pgm = new byte[entry.Length];

				using (BinaryReader br = new BinaryReader(entry.Open())) {
					pgm = br.ReadBytes(pgm.Length);
				}
				
				depths[i] = ReadPGM(pgm);
			}

			return depths;
		}
	}

	public static Dictionary<string, string> ReadMetadata(string metadataStr) {
		Dictionary<string, string> metadata = new Dictionary<string, string>();
		int idx = 0;

		//Read until '{' & skip it
		for (; metadataStr[idx] != '{'; idx++);
		idx++;

		while (idx < metadataStr.Length) {
			string key = "", value = "";

			//Skip whitespaces
			for (; IsSpace(metadataStr[idx]); idx++);

			//Add key
			while (metadataStr[idx] != ':') {
				if (metadataStr[idx] == '"') {
					idx++; //Skip '"'
					continue;
				}
				key += metadataStr[idx++];
			}
			idx++; //skip ':'

			//Skip whitespaces
			for (; IsSpace(metadataStr[idx]); idx++);

			//Add value
			//check if it's "string" format
			bool isString = false;
			if (metadataStr[idx] == '"') {
				isString = true;
				idx++;
			}

			//for string, the delimiter is '"'
			//for non-string (e.g. int) the delimiter is ',', '}', or any whitespace.
			while ((isString && (metadataStr[idx] != '"')) 
				|| (!isString && (metadataStr[idx] != ',' && metadataStr[idx] != '}' && !IsSpace(metadataStr[idx])))) {

				value += metadataStr[idx++];
			}

			//Add to the dictionary
			metadata.Add(key, value);

			if (isString)
				idx++; //skip '"'

			//Skip whitespaces
			for (; IsSpace(metadataStr[idx]); idx++);


			if (metadataStr[idx] == ',') { //!isSpace
				idx++;
				continue;
			}

			else if (metadataStr[idx] == '}') { //!isSpace
				break;
			}

			else {
				Debug.LogErrorFormat("Malformatted metadata. -- Got an unexpected character {0}.", metadataStr[idx]);
				return null;
			}
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