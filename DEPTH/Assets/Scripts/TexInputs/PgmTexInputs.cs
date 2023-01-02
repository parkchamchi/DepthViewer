using System.Collections;
using System.Collections.Generic;
using System.IO;

using UnityEngine;

public class PgmTexInputs : TexInputs {
	/*
	Use a .pgm file as the depthmap and the subsequent image file as texture
	akin to .depthviewer
	*/

	public bool WaitingSequentialInput = false;

	private IDepthMesh _dmesh;

	private float[] _depths;
	private int _x, _y;

	/* Sequential image file input */
	public class SequentialImgInputBehav : SequentialInputBehav {
		private PgmTexInputs _outer;
		public SequentialImgInputBehav(PgmTexInputs outer) {_outer = outer;}

		public bool WaitingSequentialInput {get {return _outer.WaitingSequentialInput;}}
		public void SequentialInput(string filepath, FileTypes ftype) {_outer.ImgTextureInput(filepath, ftype);}
	} 
	private SequentialImgInputBehav _seqbehav;
	public SequentialInputBehav SeqInputBehav {get {return _seqbehav;}}

	public PgmTexInputs(string pgmpath, IDepthMesh dmesh) {
		_dmesh = dmesh;

		_seqbehav = new SequentialImgInputBehav(this);

		//Open the pgm
		if (!File.Exists(pgmpath)) {
			UITextSet.StatusText.text = "Couldn't find the PGM file";
			return;
		}
		byte[] pgm = File.ReadAllBytes(pgmpath);

		_depths = DepthFileUtils.ReadPGM(pgm, out _x, out _y);
		if (_depths == null) {
			UITextSet.StatusText.text = "Error reading the PGM file";
			return;
		}

		_dmesh.ShouldUpdateDepth = true;
		_dmesh.SetScene(_depths, _x, _y, (float) _x/_y, StaticGOs.PlaceholderTexture);

		WaitingSequentialInput = true;
		UITextSet.StatusText.text = "INPUT AN IMAGE FILE";
	}

	public void ImgTextureInput(string filepath, FileTypes ftype) {
		WaitingSequentialInput = false;

		if (ftype != FileTypes.Img) {
			UITextSet.StatusText.text = $"Got non-image input: {ftype}";
			return;
		}

		Texture2D tex = Utils.LoadImage(filepath);
		if (tex == null) {
			UITextSet.StatusText.text = $"Couldn't read the image.";
			return;
		}

		_dmesh.SetScene(_depths, _x, _y, (float) tex.width/tex.height, tex);
		UITextSet.StatusText.text = "INPUT READ.";
	}

	public void UpdateTex() {}
	public void Dispose() {}
}
