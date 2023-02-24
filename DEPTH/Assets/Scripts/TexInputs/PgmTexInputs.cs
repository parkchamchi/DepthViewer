using System.Collections;
using System.Collections.Generic;
using System.IO;

using UnityEngine;

public class PgmTexInputs : TexInputs {
	/*
	Use a .pgm file as the depthmap and the subsequent image file as texture
	akin to .depthviewer
	*/

	private IDepthMesh _dmesh;
	private Depth _depth;

	/* Sequential image file input */
	public bool WaitingSequentialInput {get; private set;}
	public void SequentialInput(string filepath, FileTypes ftype) =>
		ImgTextureInput(filepath, ftype);

	public PgmTexInputs(string pgmpath, IDepthMesh dmesh) {
		_dmesh = dmesh;

		//Open the pgm
		if (!File.Exists(pgmpath)) {
			UITextSet.StatusText.text = "Couldn't find the PGM file";
			return;
		}
		byte[] pgm = File.ReadAllBytes(pgmpath);

		_depth = DepthFileUtils.ReadPGM(pgm);
		if (_depth == null) {
			UITextSet.StatusText.text = "Error reading the PGM file";
			return;
		}

		_dmesh.ShouldUpdateDepth = true;
		_dmesh.SetScene(_depth, StaticGOs.PlaceholderTexture, ratio:(float) _depth.X/_depth.Y); //Use ratio of the pgm rather than that of the placeholder

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

		_dmesh.SetScene(_depth, tex);
		UITextSet.StatusText.text = "INPUT READ.";
	}

	public void UpdateTex() {}
	public void Dispose() {}
}
