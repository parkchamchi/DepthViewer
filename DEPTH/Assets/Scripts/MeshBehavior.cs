using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MeshBehavior : MonoBehaviour {

	public Slider MeshLocSlider;
	public Slider ScaleSlider;

	private Mesh _mesh;
	private Vector3[] _vertices;
	private Vector2[] _uv;
	private	int[] _triangles;
	private Material _material;

	private float[] _depths; //for UpdateDepth()

	private float _width = 320; //canvas size
	private float _height = 180;

	private int _x = 320; //pixels of the input
	private int _y = 180;

	private float _ratio = -1;

	private const float _defaultDepthMult = 50f;
	private float _depthMult;

	private float _rotateSpeed = 75f;

	private const float _defaultZ = 0f;

	private float _alpha = 1f;
	private float _beta = 0.5f;

	void Start() {
		_mesh = new Mesh();
		_mesh.MarkDynamic();
		_mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; //for >65k vertices
		GetComponent<MeshFilter>().mesh = _mesh;

		_material = GetComponent<MeshRenderer>().GetComponent<Renderer>().material;

		_depthMult = _defaultDepthMult;
	}

	void Update() {
		float vInput = Input.GetAxis("Vertical") * _rotateSpeed;
		float hInput = Input.GetAxis("Horizontal") * _rotateSpeed;

		transform.Rotate(Vector3.left * vInput * Time.deltaTime);
		transform.Rotate(Vector3.up * hInput * Time.deltaTime);
	}

	private void SetMeshSize(int x, int y, float ratio) {
		/*
			x, y: pixel count of DEPTH.
			ratio: (width/height) of IMAGE.
		*/

		_mesh.Clear();

		/*Size of the image on canvas: that is, image fit to (_width, _height), while retaining the ratio. Thus these would not exceed _width and _height.*/
		float imwidth = 0;
		float imheight = 0;

		if (ratio <= 0) {
			Debug.LogError($"Invalid ratio: {ratio}");
			ratio = (float) x/y;
		}

		//Check ratio
		if (ratio > (float) _width/_height) {
			//longer width
			imwidth = _width;
			imheight = imwidth / ratio;
		}
		else {
			//longer height
			imheight = _height;
			imwidth = imheight * ratio;
		}

		float x_start = -imwidth/2;
		float y_start = imheight/2;

		float x_gap = (float) imwidth / x;
		float y_gap = (float) imheight / y;			

		_vertices = new Vector3[x*y]; //<65k for uint16
		_uv = new Vector2[_vertices.Length];
		for (int i = 0; i < _vertices.Length; i++) {
			_vertices[i] = new Vector3(x_start + i%x * x_gap, y_start - i/x * y_gap, 0);
			_uv[i] = new Vector2((float) (i%x) / x, (float) (y - (i/x)) / y);
		}

		_triangles = new int[(y-1)*(x-1)*6];
		int offset = 0;
		for (int h = 0; h < y-1; h++) {
			for (int w = 0; w < x-1; w++) {
				offset = (h*(x-1) + w) * 6;

				_triangles[offset+0] = h*x + w;
				_triangles[offset+1] = h*x + (w+1);
				_triangles[offset+2] = (h+1)*x + (w+1);

				_triangles[offset+3] = h*x + w;
				_triangles[offset+4] = (h+1)*x + (w+1);
				_triangles[offset+5] = (h+1)*x + w;
			}
		}

		_mesh.vertices = _vertices;
		_mesh.uv = _uv;
		_mesh.triangles = _triangles;

		_x = x;
		_y = y;

		_ratio = ratio;
	}

	private void SetDepth(float[] depths) {
		if (depths.Length != _x*_y) {
			Debug.LogError("depths.Length " + depths.Length + " does not match _x*_y " + _x*_y + " .");
			return;
		}

		_depths = depths; //depth should not change elsewhere! (especially for images)

		UpdateDepth();
	}

	private void UpdateDepth() {
		/* Also called when a image is being shown and depthmult is updated */
		/*
		`depths` are normalized to [0, ..., 1]
		MiDaS returns inverse depth, so let k be
		1 / (a*x + b)
		where a > 0, b > 0.
		Now k's are [1/b, ... , 1/(a+b)]
		And normalize such that z be
		(k * (a + b) - 1) * b / a
		This gives us [1, ..., 0], where 1 is the closest.
		*/

		if (_vertices == null || _depths == null) return;

		for (int i = 0; i < _depths.Length; i++) { //_alpha and _beta is assured to be positive
			float z = (1 / (_alpha * _depths[i] + _beta)); //inverse
			z = (z * (_alpha + _beta) - 1) * _beta / _alpha; //normalize
			_vertices[i].z = z * _depthMult;
		}
		_mesh.vertices = _vertices;
	}

	public void SetScene(float[] depths, int x, int y, float ratio, Texture texture=null) {
		if (x*y != depths.Length) {
			Debug.LogError("x*y " + x*y + " does not match depths.Length " + depths.Length + " .");
			return;
		}

		if (x != _x || y != _y || ratio != _ratio)
			SetMeshSize(x, y, ratio:ratio);
		SetDepth(depths);

		if (texture)
			_material.mainTexture = texture;
	}

	/* 3 functions below are copy-pasted (for now) */

	public void SetDepthMult(float rat, bool shouldUpdate) {
		_depthMult = _defaultDepthMult * rat;

		if (shouldUpdate)
			UpdateDepth();
	}

	public void SetAlpha(float rat, bool shouldUpdate) {
		_alpha = rat;

		if (shouldUpdate)
			UpdateDepth();
	}

	public void SetBeta(float rat, bool shouldUpdate) {
		_beta = rat;

		if (shouldUpdate)
			UpdateDepth();
	}

	public void SetZ() {
		float offset = MeshLocSlider.value;
		transform.position = new Vector3(0, 0, _defaultZ - offset);
	}

	public void SetScale() {
		float scale = ScaleSlider.value;
		transform.localScale = new Vector3(scale, scale, transform.localScale.z);
	}

}