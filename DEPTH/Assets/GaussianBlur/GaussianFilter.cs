using UnityEngine;

public enum DownSampleModes {Off, Half, Quarter}

public static class GaussianFilter {
	public static DownSampleModes DownSampleMode = DownSampleModes.Quarter;

	private static Shader _shader;
	private static Material _material;

	static GaussianFilter() {
		_shader = Shader.Find("Hidden/Gaussian Blur Filter");
		if (_shader == null)
			Debug.LogError("GaussianFilter: Couldn't find the shader.");
	}

	public static void Filter(RenderTexture source, RenderTexture destination, int iteration=4) {
		if (iteration < 0 || 8 < iteration)
			Debug.LogWarning($"GaussianFilter: Interation {iteration} is out of the normal range [0, 8]");

		if (_material == null) {
			_material = new Material(_shader);
			_material.hideFlags = HideFlags.HideAndDontSave;
		}

		RenderTexture rt1, rt2;

		if (DownSampleMode == DownSampleModes.Half)
		{
			rt1 = RenderTexture.GetTemporary(source.width / 2, source.height / 2);
			rt2 = RenderTexture.GetTemporary(source.width / 2, source.height / 2);
			Graphics.Blit(source, rt1);
		}
		else if (DownSampleMode == DownSampleModes.Quarter)
		{
			rt1 = RenderTexture.GetTemporary(source.width / 4, source.height / 4);
			rt2 = RenderTexture.GetTemporary(source.width / 4, source.height / 4);
			Graphics.Blit(source, rt1, _material, 0);
		}
		else
		{
			rt1 = RenderTexture.GetTemporary(source.width, source.height);
			rt2 = RenderTexture.GetTemporary(source.width, source.height);
			Graphics.Blit(source, rt1);
		}

		for (var i = 0; i < iteration; i++)
		{
			Graphics.Blit(rt1, rt2, _material, 1);
			Graphics.Blit(rt2, rt1, _material, 2);
		}

		Graphics.Blit(rt1, destination);

		RenderTexture.ReleaseTemporary(rt1);
		RenderTexture.ReleaseTemporary(rt2);
	}
}
