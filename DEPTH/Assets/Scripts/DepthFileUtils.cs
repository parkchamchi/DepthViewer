using System.Collections;
using System.Collections.Generic;
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;

using UnityEngine;

public static class DepthFileUtils {
	public const string Version = "v0.5.9-beta";
	
	public const string DepthExt = ".depthviewer";

	private static string _depthdir;
	public static string DepthDir {
		set {
			_depthdir = value;
			Utils.CreateDirectory(_depthdir);
		}
		get {
			return _depthdir;
		}
	}

	private static readonly string _defaultDepthDir;

	private static ZipArchive _archive;
	private static ZipArchiveMode _archiveMode;
	private static string _archive_path;

	private static long _framecount;
	private static long _count;
	private static bool _isFull;

	//Values are arbitrarily set relative numbers
	//So that the highest quality of depth file would be loaded
	public enum ModelTypes : int {
		MidasV21Small = 100,
		MiDasV21 = 200,
		MidasV3DptHybrid = 300,
		MidasV3DptLarge = 400,
	}

	public static void Dispose() {
		if (_archive != null)
			_archive.Dispose();
	}

	public static void Reopen() {
		/* This function is needed because ZipArchive.Length does not work if it was modified (why?) */

		if (_archive == null) return;

		_archive.Dispose();
		_archive = ZipFile.Open(_archive_path, _archiveMode);
	}

	public static void CreateDepthFile(long framecount, long startframe, string hashval, string orig_basename, int orig_width, int orig_height, int x, int y, int model_type_val, string model_type=null) {
		/*
		Args:
			hashval: hash value (see Utils)
			orig_filename: filename of original image (need not be the full path)
			orig_width, orig_height: size of ORIGINAL IMAGE INPUT.
			x, y: size of DEPTH.
			model_type: model used. (see DepthONNX.ModelType)
		*/

		//if (x*y*orig_width*orig_height == 0) return; //256*256*512*384 == 0...
		if (x == 0 || y == 0 || orig_width == 0 || orig_height == 0) return;


		orig_basename = Path.GetFileName(orig_basename);

		if (model_type == null) {
			if (Enum.IsDefined(typeof (ModelTypes), model_type_val))
				model_type = Enum.GetName(typeof (ModelTypes), model_type_val);
			else
				model_type = $"unknown_{model_type_val}";
		}

		string metadata = WriteMetadata(
			hashval: hashval,
			framecount: framecount.ToString(),
			startframe: startframe.ToString(),
			width: x.ToString(),
			height: y.ToString(),
			model_type: model_type,
			model_type_val: model_type_val.ToString(),
			original_name: orig_basename,
			original_width: orig_width.ToString(),
			original_height: orig_height.ToString(),
			timestamp: Utils.GetTimestamp(),
			program: "DepthViewer",
			version: Version
		);

		string output_filepath = GetDepthFileName(orig_basename, model_type_val, hashval);
		if (_archive != null) _archive.Dispose();

		_archiveMode = ZipArchiveMode.Update;
		_archive_path = output_filepath;
		_archive = ZipFile.Open(_archive_path, _archiveMode);

		UpdateDepthFileMetadata(metadata);

		_framecount = framecount;
		_count = 0;
		_isFull = false;
	}

	public static string GetDepthFileName(string orig_basename, int model_type_val, string hashval) {
		orig_basename = Path.GetFileName(orig_basename);

		string output_filepath = $"{orig_basename}.{model_type_val}.{hashval}{DepthExt}";
		if (output_filepath.Length > 250) //if it's too long, omit the orig basename
			output_filepath = $"{model_type_val}.{hashval}{DepthExt}";

		output_filepath = _depthdir + '/' + output_filepath;

		return output_filepath;
	}

	public static void UpdateDepthFile(float[] depths, long frame, int x, int y) {
		if (x*y == 0 || depths == null) return;

		if (frame >= _framecount) {
			Debug.LogWarning($"frame {frame} exceeds _framecount {_framecount}");
			return;
		}

		string filename = $"{frame}.pgm";

		//Write the new depth
		ZipArchiveEntry entry = _archive.GetEntry(filename); //is `get` necessary?
		if (entry == null) 
			entry = _archive.CreateEntry(filename);
		using (BinaryWriter bw = new BinaryWriter(entry.Open()))
			bw.Write(WritePGM(depths, x, y));

		//Increment _count
		_count++;
		if (_count >= _framecount) {
			/* Archive is full! */
			//Test
			IsFull();
			if (!_isFull)
				Debug.LogError("_count <= 0, but !_isfull");

			//Set the mode to readonly
			_archiveMode = ZipArchiveMode.Read;
			
			//Reopening will be done elsewhere (e.g. end of the video)
		}
	}

	public static void UpdateDepthFileMetadata(string metadata) {
		if (metadata == null) return;

		ZipArchiveEntry metadataEntry = _archive.GetEntry("METADATA.txt");
		if (metadataEntry == null)
			metadataEntry = _archive.CreateEntry("METADATA.txt");
		using (StreamWriter sw = new StreamWriter(metadataEntry.Open()))
			sw.Write(metadata);
	}

	public static string ProcessedDepthFileExists(string hashval, out int maxModelTypeVal) {
		/* 
			Returns a list of paths of processed depth files.
			filename format: [basename].modelval.hash64len.ext
		*/
		maxModelTypeVal = -1;
		string finalFile = null;

		if (hashval == null) return null;

		foreach (string filename in Directory.GetFiles(_depthdir))
			if (filename.EndsWith($"{hashval}{DepthExt}")) {
				string[] tokens = Path.GetFileName(filename).Split('.');
				int modelTypeVal = int.Parse(tokens[tokens.Length-3]);
				
				if (modelTypeVal > maxModelTypeVal) {
					finalFile = filename;
					maxModelTypeVal = modelTypeVal;
				}
			}

		return finalFile;
	}

	public static string WriteMetadata(string hashval, string framecount, string startframe, string width, string height, string model_type, string model_type_val,
		string original_name, string original_width, string original_height, string timestamp, string program, string version) {
		/*
		A line per a field, delimited by the initial '='
		*/

		string metadata = "DEPTHVIEWER\n"
			+ $"hashval={hashval}\n"
			+ $"framecount={framecount}\n"
			+ $"startframe={startframe}\n"
			+ $"width={width}\n"
			+ $"height={height}\n"
			+ $"model_type={model_type}\n"
			+ $"model_type_val={model_type_val}\n"
			+ $"original_name={original_name}\n"
			+ $"original_width={original_width}\n"
			+ $"original_height={original_height}\n"
			+ $"timestamp={timestamp}\n"
			+ $"program={program}\n"
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

	public static bool ReadDepthFile(string path, out long framecount, out Dictionary<string, string> metadata, bool readOnlyMode=false) {
		/*
			out x, y: pixel count of DEPTH.
			out orig_ratio: ratio of ORIGINAL INPUT.

			returns: IsFull()
		*/

		framecount = -1;
		
		//x = y = 0;
		metadata = null;

		if (!path.EndsWith(DepthExt)) {
			Debug.LogError("File " + path + " is not a valid format.");
			//return;
		}
		if (!File.Exists(path)) {
			Debug.LogError("File " + path + " does not exist.");
			//return;
		}

		if (_archive != null) _archive.Dispose();
		_archive_path = path;

		//First, read as readonly
		_archive = ZipFile.Open(_archive_path, ZipArchiveMode.Read);

		//Read the metadata
		string metadataStr;
		ZipArchiveEntry metadataEntry = _archive.GetEntry("METADATA.txt");
		using (StreamReader br = new StreamReader(metadataEntry.Open()))
			metadataStr = br.ReadToEnd();
		metadata = ReadMetadata(metadataStr);
		
		//x = int.Parse(metadata["width"]);
		//y = int.Parse(metadata["height"]);

		framecount = _framecount = int.Parse(metadata["framecount"]);

		//Check if it is full. This sets _isFull, _count.
		IsFull();

		//If it is not, and readOnlyMode == true, reopen as update-mode
		if (!_isFull && !readOnlyMode) {
			_archiveMode = ZipArchiveMode.Update;
			Reopen();
		}
		else {
			_archiveMode = ZipArchiveMode.Read;
		}

		return _isFull;
	}

	public static bool IsFull() {
		/*
		Check if the number of .pgm files matches the framecount
		Note: Unity's VideoPlayer gives wrong framecount (larger than it  actually is) for some videos,
			so some depthfiles generated by DepthViewer will never be full.
		*/

		if (_archive == null) {
			Debug.LogError("Archive is null!");
			return false;
		}

		long count = 0;
		/*
		foreach (ZipArchiveEntry entry in _archive.Entries) {
			if (entry.Name.EndsWith(".pgm"))
				count++;
		*/

		for (int i = 0; i < _framecount; i++) {
			ZipArchiveEntry entry = _archive.GetEntry($"{i}.pgm");
			if (entry != null)
				count++;
		}

		_count = count;
		_isFull = (count >= _framecount);

		return _isFull;
	}

	public static float[] ReadFromArchive(long frame, out int x, out int y) {
		x = y = 0;

		if (_archive == null) {
			Debug.LogError("Archive is null!");
			return null;
		}

		//Read the frames
		ZipArchiveEntry entry = _archive.GetEntry($"{frame}.pgm");
		if (entry == null)
			return null;

		byte[] pgm = new byte[entry.Length];

		using (BinaryReader br = new BinaryReader(entry.Open()))
			pgm = br.ReadBytes(pgm.Length);
		
		float[] depths = ReadPGM(pgm, out x, out y);
		return depths;
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

	/*public static float[] ReadPGM(byte[] pgm) {
		return ReadPGM(pgm, out _, out _);
	}*/

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