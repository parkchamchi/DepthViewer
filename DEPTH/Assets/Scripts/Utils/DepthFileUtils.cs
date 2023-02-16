using System.Collections;
using System.Collections.Generic;
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;

using UnityEngine;

public static class DepthFileUtils {
	public const string Version = "v0.8.5-beta";
	
	public const string DepthExt = ".depthviewer";

	private const string _metadataFilename = "METADATA.txt";
	private const string _paramsFilename = "PARAMS.txt";

	private static string _savedir;
	public static string SaveDir {
		set {
			if (!Directory.Exists(value)) {
				Debug.LogError("Invalid directory: " + value);
				return;
			}
			
			_savedir = value;
		}
		get {
			return _savedir;
		}
	}

	public static string DepthDir {
		get {
			string depthdir = $"{_savedir}/depths";
			Utils.CreateDirectory(depthdir);

			return depthdir;
		}
	}

	private static ZipArchive _archive;
	private static ZipArchiveMode _archiveMode;
	private static string _archive_path;

	private static long _framecount;
	private static long _count;
	private static bool _isFull;

	static DepthFileUtils() {
		_savedir = Application.persistentDataPath;
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

	public static void CreateDepthFile(long framecount, long startframe, string hashval, string orig_basename, int orig_width, int orig_height, float orig_fps, int x, int y, string model_type) {
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
			Debug.LogError("CreateDepthFile(): model_type == null!");
			model_type = "unknown";
		}

		string metadata = WriteMetadata(
			hashval: hashval,
			framecount: framecount.ToString(),
			startframe: startframe.ToString(),
			width: x.ToString(),
			height: y.ToString(),
			model_type: model_type,
			model_type_val: "0",
			original_name: orig_basename,
			original_width: orig_width.ToString(),
			original_height: orig_height.ToString(),
			original_framerate: orig_fps.ToString(),
			timestamp: Utils.GetTimestamp(),
			program: "DepthViewer",
			version: Version
		);

		string output_filepath = GetDepthFileName(orig_basename, hashval);
		if (_archive != null) _archive.Dispose();

		_archiveMode = ZipArchiveMode.Update;
		_archive_path = output_filepath;
		_archive = ZipFile.Open(_archive_path, _archiveMode);

		UpdateDepthFileMetadata(metadata);

		_framecount = framecount;
		_count = 0;
		_isFull = false;
	}

	public static string GetDepthFileName(string orig_basename, string hashval, int modelTypeVal=0) {
		//If there are several depthfiles with the same hashval, the one with the highest model_type_val is loaded. Since it's not used anymore just set it to 0.

		orig_basename = Path.GetFileName(orig_basename);
		
		string output_filepath = $"{orig_basename}.{modelTypeVal}.{hashval}{DepthExt}";
		if (output_filepath.Length > 250) //if it's too long, omit the orig basename
			output_filepath = $"{modelTypeVal}.{hashval}{DepthExt}";

		output_filepath = DepthDir + '/' + output_filepath;

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

		ZipArchiveEntry metadataEntry = _archive.GetEntry(_metadataFilename);
		if (metadataEntry == null)
			metadataEntry = _archive.CreateEntry(_metadataFilename);
		using (StreamWriter sw = new StreamWriter(metadataEntry.Open()))
			sw.Write(metadata);
	}

	public static string ProcessedDepthFileExists(string hashval) =>
		ProcessedDepthFileExists(hashval, out _);

	public static string ProcessedDepthFileExists(string hashval, out int maxModelTypeVal) {
		/* 
			Returns a list of paths of processed depth files.
			filename format: [basename].modelval.hash64len.ext
		*/
		maxModelTypeVal = -1;
		string finalFile = null;

		if (hashval == null) return null;

		foreach (string filename in Directory.GetFiles(DepthDir))
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
		string original_name, string original_width, string original_height, string original_framerate, string timestamp, string program, string version) {
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
			+ $"original_framerate={original_framerate}\n"
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

	public static bool ReadDepthFile(string path, out long framecount, out string modelType, out Dictionary<string, string> metadata, out Dictionary<long, string> paramsDict, bool readOnlyMode=false) {
		/*
			out x, y: pixel count of DEPTH.
			out orig_ratio: ratio of ORIGINAL INPUT.

			returns: IsFull()
		*/

		framecount = -1;
		
		metadata = null;
		paramsDict = null;

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
		ZipArchiveEntry metadataEntry = _archive.GetEntry(_metadataFilename);
		using (StreamReader br = new StreamReader(metadataEntry.Open()))
			metadataStr = br.ReadToEnd();
		metadata = ReadMetadata(metadataStr);

		framecount = _framecount = int.Parse(metadata["framecount"]);
		modelType = metadata["model_type"];

		//Read the params, if it exists
		string paramsStr;
		ZipArchiveEntry paramsEntry = _archive.GetEntry(_paramsFilename);
		if (paramsEntry != null) {
			using (StreamReader br = new StreamReader(paramsEntry.Open()))
				paramsStr = br.ReadToEnd();

			try {
				paramsDict = ReadParamsStr(paramsStr);
			}
			catch (Exception exc) {
				Debug.LogWarning($"Falied to read the params: {exc}");
			}
		}

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

	private static Dictionary<string, string> ReadMetadata(string metadataStr) {
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

	private static Dictionary<long, string> ReadParamsStr(string paramsStr) {
		/*
		!PARAMSVERSION=1

		!FRAME=0
		...=...
		...=...
		!ENDFRAME

		...
		*/

		Dictionary<long, string> paramsDict = new Dictionary<long, string>();
		StringBuilder frameParams = null;
		bool in_substr = false;

		long framenum = -1;

		foreach (string line in paramsStr.Split('\n')) {
			if (line == "")	continue;

			//Lines not starting with '!'
			if (!line.StartsWith('!')) {
				if (in_substr) {
					//part of params -- append
					frameParams.Append(line);
					frameParams.Append('\n');
				}
				else {
					Debug.LogWarning($"Got illegal params string: line without '!': {line}");
				}

				continue;
			}

			//Lines staring with '!'
			string key, value;

			int sep_i = line.IndexOf('=');
			if (sep_i < 0) {
				//if the delimiter is not in the line, key is the whole line
				key = line.Substring(1);
				value = null;
			}
			else {
				key = line.Substring(1, sep_i-1).Trim();
				value = line.Substring(sep_i + 1).Trim();
			}

			switch (key) {
			case "PARAMSVERSION":
				if (value != "1")
					Debug.LogWarning($"Higher params version: {value}");
				break;

			case "FRAME":
				//Start of the frame
				in_substr = true;

				framenum = long.Parse(value);
				frameParams = new StringBuilder();

				break;
			
			case "ENDFRAME":
				in_substr = false;
				paramsDict[framenum] = frameParams.ToString();

				break;

			default:
				Debug.LogWarning($"Unknown params statement {key}");
				break;
			}
		}

		return paramsDict;
	}

	private static string WriteParamsStr(Dictionary<long, string> paramsDict) {
		if (paramsDict == null) return null;

		StringBuilder output = new StringBuilder();
		output.Append("!PARAMSVERSION=1\n\n");

		foreach (var item in paramsDict) {
			output.Append($"!FRAME={item.Key}\n");
			output.Append(item.Value);
			output.Append("\n!ENDFRAME\n\n");
		}

		return output.ToString();
	}

	public static void WriteParams(Dictionary<long, string> paramsDict) {
		//Write to depthfile

		if (_archive == null) {
			Debug.LogError("WriteParams() called when _archive == null");
			return;
		}
		if (paramsDict == null) {
			Debug.LogError("WriteParams() called when paramsDict == null");
			return;
		}

		string paramsStr = WriteParamsStr(paramsDict);

		/*
		Save the original archive mode, which can be `Read`...
		Albeit this is not needed since this method is called in ImgVidDepthTexInputs.Dispose()
			and the _archive is disposed right after this
		*/
		ZipArchiveMode origMode = _archiveMode;
		_archiveMode = ZipArchiveMode.Update;
		if (origMode != _archiveMode) {
			try {
				Reopen();
			}
			catch (System.IO.IOException) {
				Debug.LogError("Couldn't save params: failed to reopen the depthfile as Write mode (is it opened by other software?)");
				return;
			}
		}
		
		ZipArchiveEntry paramsEntry = _archive.GetEntry(_paramsFilename);

		if (paramsDict.Count > 0) {
			//Write the params

			if (paramsEntry == null)
				paramsEntry = _archive.CreateEntry(_paramsFilename);
			using (StreamWriter sw = new StreamWriter(paramsEntry.Open()))
				sw.Write(paramsStr);
		}
		else {
			//Nothing to write -- just delete it (if it exists)
			if (paramsEntry != null)
				paramsEntry.Delete();
		}

		_archiveMode = origMode;
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