using UnityEngine;
using System;
using System.Threading.Tasks;

public class VRRecordBehavior : MonoBehaviour {
	public Camera mainCamera;

	public RenderTexture cubemapRTLeft2048;
	public RenderTexture cubemapRTRight2048;
	public RenderTexture equirectRT2048;

	public RenderTexture cubemapRTLeft4096;
	public RenderTexture cubemapRTRight4096;
	public RenderTexture equirectRT4096;

	private RenderTexture _cmRTL;
	private RenderTexture _cmRTR;
	private RenderTexture _equiRT;

	private int _size;
	public int Size {
		set {
			switch (value) {
			case 2048:
				_cmRTL = cubemapRTLeft2048;
				_cmRTR = cubemapRTRight2048;
				_equiRT = equirectRT2048;
				break;
			case 4096:
				_cmRTL = cubemapRTLeft4096;
				_cmRTR = cubemapRTRight4096;
				_equiRT = equirectRT4096;
				break;
			default: //can't fall through
				Debug.LogError($"Unsupported size: {value}. Falling back to 2048.");
				_cmRTL = cubemapRTLeft2048;
				_cmRTR = cubemapRTRight2048;
				_equiRT = equirectRT2048;
				break;
			}
		}
	}

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

		mainCamera.RenderToCubemap(_cmRTL, 63, Camera.MonoOrStereoscopicEye.Left);
		mainCamera.RenderToCubemap(_cmRTR, 63, Camera.MonoOrStereoscopicEye.Right);
		
		_cmRTL.ConvertToEquirect(_equiRT, Camera.MonoOrStereoscopicEye.Left);
		_cmRTR.ConvertToEquirect(_equiRT, Camera.MonoOrStereoscopicEye.Right);

		Texture2D tex = new Texture2D(_equiRT.width, _equiRT.height);
		RenderTexture.active = _equiRT;
		tex.ReadPixels(new Rect(0, 0, _equiRT.width, _equiRT.height), 0, 0);
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