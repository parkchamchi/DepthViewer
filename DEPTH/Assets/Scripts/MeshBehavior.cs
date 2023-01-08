using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public interface IDepthMesh {
	bool ShouldUpdateDepth {set;} //whether the depth has to be updated when the parameters (alpha, ...) is changed
	void SetScene(float[] depths, int x, int y, float ratio, Texture texture=null);

	void SetParam(string paramname, float value);
	void ToDefault();
}

public class MeshBehavior : MonoBehaviour, IDepthMesh {

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

	private float _rotateSpeed = 75f;

	private float _defaultZ;
	private float _defaultX;
	private float _defaultY;

	private bool _shouldUpdateDepth = false;
	public bool ShouldUpdateDepth {set {_shouldUpdateDepth = value;}} //Depth has to be updated when an image is being shown

	public const float DefaultAlpha = 1f;
	private float _alpha = DefaultAlpha;
	public float Alpha {
		set {
			if (value <= 0) {
				//Debug.LogError($"Got negative alpha {value}.");
				return;
			}
			_alpha = value;
			if (_shouldUpdateDepth) UpdateDepth();
		}
	}

	public const float DefaultBeta = 0.5f;
	private float _beta = DefaultBeta;
	public float Beta {
		set {
			if (value <= 0) {
				//Debug.LogError($"Got negative beta {value}.");
				return;
			}
			_beta = value;
			if (_shouldUpdateDepth) UpdateDepth();
		}
	}

	public const float DefaultDepthMult = 50f;
	private float _depthMult = DefaultDepthMult;
	public float DepthMult {
		set {
			_depthMult = value;
			if (_shouldUpdateDepth) UpdateDepth();
		}
	}

	public const float DefaultMeshLoc = 0f;
	public float MeshLoc {
		set {transform.position = new Vector3(transform.position.x, transform.position.y, _defaultZ - value);}
	}

	public const float DefaultMeshX = 0f;
	public float MeshX {
		set {transform.position = new Vector3(_defaultX - value, transform.position.y, transform.position.z);}
	}

	public const float DefaultMeshY = 0f;
	public float MeshY {
		set {transform.position = new Vector3(transform.position.x, _defaultY - value, transform.position.z);}
	}

	public const float DefaultScale = 1f;
	public float Scale {
		set {
			transform.localScale = new Vector3(value, value, transform.localScale.z);
		}
	}

	private bool _isThresholdSet = false;
	private float _threshold = 0f;
	public float Threshold {
		set {
			if (value < 0 || value > 1) {
				Debug.LogError($"Got invalid Threshold value {value}");
				return;
			}
			_threshold = value;
			_isThresholdSet = (_threshold != 0) ? true : false;

			if (_shouldUpdateDepth) UpdateDepth();
		}
	}

	private float _targetVal = 0f;
	public float TargetVal {
		get {return _targetVal;}
		set {
			if (value < 0 || value > 1) {
				Debug.LogError($"Got invalid TargetVal value {value}");
				return;
			}
			_targetVal = value;

			if (_shouldUpdateDepth) UpdateDepth();
		}
	}

	public void ToDefault() {
		//Does not reset MeshX, MeshY

		Alpha = DefaultAlpha;
		Beta = DefaultBeta;
		DepthMult = DefaultDepthMult;
		MeshLoc = DefaultMeshLoc;
		Scale = DefaultScale;

		ResetRotation();
	}

	void Start() {
		_mesh = new Mesh();
		_mesh.MarkDynamic();
		_mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; //for >65k vertices
		GetComponent<MeshFilter>().mesh = _mesh;

		_material = GetComponent<MeshRenderer>().GetComponent<Renderer>().material;

		_defaultZ = transform.position.z;
		_defaultX = transform.position.x;
		_defaultY = transform.position.y;
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
		`_depths` are normalized to [0, ..., 1]
		MiDaS returns inverse depth, so let k be
		1 / (a*x + b)
		where a > 0, b > 0.
		Now k's are [1/b, ... , 1/(a+b)]
		Normalize such that z be
		(k * (a + b) - 1) * b / a
		This gives us [1, ..., 0], where 0 is the closest.
		*/
		/*
		If the Threshold [0, ..., 1] is set (that is, nonzero), set all values below it as the TargetVal.
		*/

		if (_vertices == null || _depths == null) return;

		for (int i = 0; i < _depths.Length; i++) { //_alpha and _beta are assured to be positive
			float z = _depths[i];
			if (_isThresholdSet && (z < _threshold))
				z = _targetVal;

			z = (1 / (_alpha * z + _beta)); //inverse
			z = (z * (_alpha + _beta) - 1) * _beta / _alpha; //normalize
			_vertices[i].z = z * _depthMult;
		}
		_mesh.vertices = _vertices;
	}

	public void SetScene(float[] depths, int x, int y, float ratio, Texture texture=null) {
		if (depths == null) {
			Debug.LogError("SetScene(): depths == null");
			return;
		}
		if (x*y != depths.Length) {
			Debug.LogError("x*y " + x*y + " does not match depths.Length " + depths.Length + " .");
			return;
		}
		if (depths.Length == 0) {
			Debug.LogError("depth.Length == 0");
			return;
		}

		if (x != _x || y != _y || ratio != _ratio)
			SetMeshSize(x, y, ratio:ratio);
		SetDepth(depths);

		if (texture)
			_material.mainTexture = texture;
	}

	public void ResetRotation() =>
		transform.rotation = Quaternion.identity;

	public void SetParam(string paramname, float value) {
		var pinfo = this.GetType().GetProperty(paramname);
		if (pinfo == null) {
			Debug.LogError($"SetParam(): Got invalid paramname {paramname}");
			return;
		}

		pinfo.SetValue(this, value);
	}
}