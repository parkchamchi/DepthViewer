using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

public class MeshSlidersBehavior : MonoBehaviour {
	public Slider DepthMultSlider;
	public Slider AlphaSlider;
	public Slider BetaSlider;
	public Slider MeshLocSlider;
	public Slider ScaleSlider;

	private MeshBehavior _meshBehav;

	void Start() {
		_meshBehav = GameObject.Find("DepthPlane").GetComponent<MeshBehavior>();
	}

	public void SetDepthMult() =>
		_meshBehav.DepthMult = DepthMultSlider.value;

	public void SetAlpha() =>
		_meshBehav.Alpha = AlphaSlider.value;

	public void SetBeta() =>
		_meshBehav.Beta = BetaSlider.value;

	public void SetMeshLoc() =>
		_meshBehav.MeshLoc = MeshLocSlider.value;

	public void SetScale() =>
		_meshBehav.Scale = ScaleSlider.value;

	public void ToDefault() {
		//Will not reset MeshX, MeshY

		DepthMultSlider.value = MeshBehavior.DefaultDepthMult;
		AlphaSlider.value = MeshBehavior.DefaultAlpha;
		BetaSlider.value = MeshBehavior.DefaultBeta;
		MeshLocSlider.value = MeshBehavior.DefaultMeshLoc;
		ScaleSlider.value = MeshBehavior.DefaultScale;

		_meshBehav.ResetRotation();
	}
}
