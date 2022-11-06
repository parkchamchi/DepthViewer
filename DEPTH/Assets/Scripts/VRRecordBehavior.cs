using UnityEngine;
using System;
using System.Threading.Tasks;

public class VRRecordBehavior : MonoBehaviour {
	public Camera mainCamera;

	public RenderTexture cubeMapRenderTextureLeft;
	public RenderTexture cubeMapRenderTextureRight;

	public RenderTexture equirectRenderTexture;

	public Task Capture(string outputpath, string format="jpg") {
		/*
		png: save as PNG. TGA otherwise.

		300 frames, 4096x4096, written on disk, sample video (as in depthpy/utils/make_sample_vid.py):
			TGA: 78s, 64MB per frame
			PNG: 140s, 307KB per frame

		464 frames, 2048x2048, written on disk, actual video:
			JPG: 27s, 75KB per frame
			PNG: 64s, 320KB per frame
			TGA: 27s, 16MB per frame
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
		
		//UnityException: EncodeToPNG can only be called from the main thread. :(
		byte[] bytes = null;
		switch (format) {
		case "tga":
			bytes = tex.EncodeToTGA();
			break;
		case "jpg":
			bytes = tex.EncodeToJPG();
			break;
		/*default: //can't fall through?
			Debug.Log($"Got invalid format {format}. Falling back to PNG.");
			goto case "png";*/
		case "png":
			bytes = tex.EncodeToPNG();
			break;
		default:
			Debug.Log($"Got invalid format {format}. Falling back to JPG.");
			bytes = tex.EncodeToJPG();
			break;
		}

		Destroy(tex);

		return Task.Run(() => System.IO.File.WriteAllBytes(outputpath, bytes));
	}
}