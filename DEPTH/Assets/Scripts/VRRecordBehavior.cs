using UnityEngine;
using System;

public class VRRecordBehavior : MonoBehaviour {
	public Camera mainCamera;

	public RenderTexture cubeMapRenderTextureLeft;
	public RenderTexture cubeMapRenderTextureRight;

	public RenderTexture equirectRenderTexture;

	public void Capture(string outputpath, bool png=false) {
		/*
		png: save as PNG. TGA otherwise.

		300 frames, written on disk:
			TGA: 78s, 64MB per frame
			PNG: 140s, 307KB per frame
		*/

		mainCamera.stereoSeparation = 0.065f;

		mainCamera.RenderToCubemap(cubeMapRenderTextureLeft, 63, Camera.MonoOrStereoscopicEye.Left);
		mainCamera.RenderToCubemap(cubeMapRenderTextureRight, 63, Camera.MonoOrStereoscopicEye.Right);
		
		cubeMapRenderTextureLeft.ConvertToEquirect(equirectRenderTexture, Camera.MonoOrStereoscopicEye.Left);
		cubeMapRenderTextureRight.ConvertToEquirect(equirectRenderTexture, Camera.MonoOrStereoscopicEye.Right);

		Texture2D tex = new Texture2D(equirectRenderTexture.width, equirectRenderTexture.height);
		RenderTexture.active = equirectRenderTexture;
		tex.ReadPixels(new Rect(0, 0, equirectRenderTexture.width, equirectRenderTexture.height), 0, 0);
		RenderTexture.active = null;
		
		byte[] bytes = (png) ? tex.EncodeToPNG() : tex.EncodeToTGA();
		string path = outputpath;
		System.IO.File.WriteAllBytes(path, bytes);

		Destroy(tex);
	}
}