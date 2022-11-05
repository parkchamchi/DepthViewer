using UnityEngine;
using System;
using System.Threading.Tasks;

public class VRRecordBehavior : MonoBehaviour {
	public Camera mainCamera;

	public RenderTexture cubeMapRenderTextureLeft;
	public RenderTexture cubeMapRenderTextureRight;

	public RenderTexture equirectRenderTexture;

	public Task Capture(string outputpath, bool png=true) {
		/*
		png: save as PNG. TGA otherwise.

		300 frames, 4096x4096, written on disk:
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
		Destroy(tex);

		string path = outputpath;
		return Task.Run(() => System.IO.File.WriteAllBytes(path, bytes));
	}
}