using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using UnityEngine;
using UnityEngine.UI;

public class MeshShaders {
	public string Name {get; private set;}
	public bool ShouldSetVertexColors {get; private set;}

	//The initial property values for materials
	public Dictionary<string, float> MaterialProperties {get; private set;} = null;

	//For point clouds
	public const float DefaultPointCloudSize = 0.05f;
	private static readonly Dictionary<string, float> _pointCloudMatProps = new Dictionary<string, float> {
		{"_PointSize", DefaultPointCloudSize}		
	};

	public Shader Shader {
		get {
			/*
				The target shader should be under "Always Included Shaders" list in ProjectSettings/Graphics
				https://docs.unity3d.com/ScriptReference/Shader.Find.html
			*/

			Shader targetshader = Shader.Find(this.Name);
			if (targetshader == null)
				Debug.LogError($"MeshShaders.Shader: couldn't find shader {this.Name}");
			
			return targetshader;
		}
	}

	public MeshShaders(string name, bool shouldSetVertexColors, Dictionary<string, float> materialProperties=null) {
		Name = name;
		ShouldSetVertexColors = shouldSetVertexColors;

		MaterialProperties = materialProperties;
	}

	public static MeshShaders GetStandard() =>
		new MeshShaders("Standard", false);

	public static MeshShaders GetPointCloudDisk() =>
		new MeshShaders("Point Cloud/Disk", true, _pointCloudMatProps);
		
	public static MeshShaders GetPointCloudPoint() =>
		new MeshShaders("Point Cloud/Point", true, _pointCloudMatProps);
}

public interface IDepthMesh {
	bool ShouldUpdateDepth {set;} //whether the depth has to be updated when the parameters (alpha, ...) is changed
	void SetScene(Depth depth, Texture texture=null, float ratio=0);

	void SetShader(MeshShaders shader);
	void SetMaterialFloat(string propertyname, float value); //wrapper for Material.SetFloat()

	void SetParam(string paramname, float value);
	void ToDefault();

	event System.Action<string, float> ParamChanged; //paramname, value

	/*
	Save current params as string, in format
		param1=value1
		param2=value2
		...

	For video depthfiles this format would not be quite memory efficient, but since it is compressed it wouldn't be much trouble
	*/
	string ExportParams();
	void ImportParams(string paramstr);
}

public class Wiggler {
	private float _intervalScale; //Relative
	
	private float _leftAngle;
	private float _rightAngle;
	private float _upAngle;
	private float _downAngle;

	public Wiggler(float intervalScale, float horAngle, float verAngle) {
		_intervalScale = intervalScale;
		_leftAngle = _rightAngle = horAngle;
		_upAngle = _downAngle = verAngle;
	}

	public Wiggler(float intervalScale, float leftAngle, float rightAngle, float upAngle, float downAngle) {
		_intervalScale = intervalScale;

		_leftAngle = leftAngle;
		_rightAngle = rightAngle;
		_upAngle = upAngle;
		_downAngle = downAngle;
	}

	public Quaternion GetRotation() {
		//There can be a better method, especially for four params

		float curangle = Time.time * _intervalScale % (2 * MathF.PI);

		float x_rot = MathF.Cos(curangle);
		x_rot *= (x_rot > 0) ? _downAngle : _upAngle;

		float y_rot = MathF.Sin(curangle);
		y_rot *= (y_rot > 0) ? _rightAngle : _leftAngle;

		return Quaternion.Euler(x_rot, y_rot, 0);
	}
}

public class MeshBehavior : MonoBehaviour, IDepthMesh {

	private Mesh _mesh;
	private Vector3[] _vertices;
	private Vector3[] _vertices_proj; //Projected vertices using (x, y) of _vertices
	private Vector2[] _uv;
	private	int[] _triangles;
	private Material _material;

	//private float[] _depths; //for UpdateDepth()
	private Depth _depth;

	private const float _width = 320; //canvas size
	private const float _height = 180;
	private const float _depthLength = 100; //i.e. 16:9:5

	private float _ratio = -1;

	private Quaternion _rotation = Quaternion.identity;
	private float _rotateSpeed = 75f;
	public bool MoveMeshByMouse {set; private get;} = true;
	private Vector3 _lastMousePos; //can't be null

	public Wiggler MeshWiggler {set; private get;} = null;

	private float _defaultZ;
	private float _defaultX;
	private float _defaultY;

	private bool _shouldUpdateDepth = false;
	public bool ShouldUpdateDepth {set {_shouldUpdateDepth = value;}} //Depth has to be updated when an image is being shown

	private MeshShaders _shader = MeshShaders.GetStandard();
	private RenderTexture _rt; //for resizing texture (for point clouds)

	private float _disappearRemainingSec = 0f; //for this seconds, make the mesh disappear (just set them aside)
	private const float _disappearMaxSec = 0.2f;

	public event System.Action<string, float> ParamChanged;

	//TODO: Generalize the properties
	//Now all properties calls UpdateDepth() (due to projecting)
	//Also emits ParamChanged

	public const float DefaultAlpha = 1f;
	private float _alpha = DefaultAlpha;
	public float Alpha {
		set {
			if (value <= 0) {
				//Debug.LogError($"Got negative alpha {value}.");
				//return;

				value = 0.00001f; //epsilon glitches
			}
			_alpha = value;
			if (_shouldUpdateDepth) UpdateDepth();

			ParamChanged?.Invoke("Alpha", value);
		}

		get {return _alpha;}
	}

	public const float DefaultBeta = 0.5f;
	private float _beta = DefaultBeta;
	public float Beta {
		set {
			if (value <= 0) {
				//Debug.LogError($"Got negative beta {value}.");
				//return;

				value = 0.00001f;
			}
			_beta = value;
			if (_shouldUpdateDepth) UpdateDepth();

			ParamChanged?.Invoke("Beta", value);
		}

		get {return _beta;}
	}

	private float _localScaleZ {get {return transform.localScale.z;}} //should be same for all axis
	private void LocalPositionUpdate() =>
		transform.localPosition = new Vector3(_defaultX + _meshHor * _width * _localScaleZ, _defaultY - _meshVer * _height * _localScaleZ, _defaultZ + _camDist);

	private const float _scalePerCamDist = (0.96f/150); //Scale (legacy) (shrinked a little (96%)) was 1 when CamDist (MeshLoc=0) was 150

	public const float DefaultCamDist = 10f;
	private float _camDist = DefaultCamDist;
	public float CamDist {
		set {
			_camDist = value;
			LocalPositionUpdate();

			float localScale = _scalePerCamDist * value * _scaleR;
			transform.localScale = Vector3.one * localScale;

			_disappearRemainingSec = _disappearMaxSec; //Make the mesh disappear for a moment

			if (_shouldUpdateDepth) UpdateDepth();
			ParamChanged?.Invoke("CamDist", value);
			ParamChanged?.Invoke("CamDistL", MathF.Log10(CamDist));
		}

		get {return _camDist;}
	}
	public float CamDistL {set {CamDist = MathF.Pow(10, value);} get {return MathF.Log10(CamDist);}} //Log10 version

	public const float DefaultScaleR = 1f; //Relative
	private float _scaleR = DefaultScaleR;
	public float ScaleR {
		set {
			_scaleR = value;
			float localScale = value * _scalePerCamDist * _camDist; //TODO: same as CamDist, can use a same subroutine
			transform.localScale = Vector3.one * localScale;

			if (_shouldUpdateDepth) UpdateDepth();
			ParamChanged?.Invoke("ScaleR", value);
		}

		get {return _scaleR;}
	}
	
	public const float DefaultDepthMultR = 1f;
	private float _depthMultR = DefaultDepthMultR;
	public float DepthMultR {
		set {
			_depthMultR = value;
			if (_shouldUpdateDepth) UpdateDepth();

			ParamChanged?.Invoke("DepthMultR", value);
			ParamChanged?.Invoke("DepthMultRL", (value == 0) ? float.NegativeInfinity : MathF.Log10(value));
		}

		get {return _depthMultR;}
	}
	public float DepthMultRL {set {DepthMultR = (value <= -1) ? 0 : MathF.Pow(10, value);} get {return MathF.Log10(DepthMultR);}} //Log10 version. If value <= -1, just set to 0.

	public const float DefaultMeshHor = 0f;
	private float _meshHor = DefaultMeshHor;
	public float MeshHor {
		set {
			_meshHor = value;
			LocalPositionUpdate();
			if (_shouldUpdateDepth) UpdateDepth();
			ParamChanged?.Invoke("MeshHor", value);
		}

		get {return _meshHor;}
	}

	public const float DefaultMeshVer = 0f;
	private float _meshVer = DefaultMeshVer;
	public float MeshVer {
		set {
			_meshVer = value;
			LocalPositionUpdate();
			if (_shouldUpdateDepth) UpdateDepth();
			ParamChanged?.Invoke("MeshVer", value);
		}

		get {return _meshVer;}
	}

	public const float DefaultProjRatio = 0.5f;
	private float _projRatio = DefaultProjRatio;
	public float ProjRatio {
		set {
			_projRatio = value;
			if (_shouldUpdateDepth) UpdateDepth();

			ParamChanged?.Invoke("ProjRatio", value);
		}

		get {return _projRatio;}
	}

	private float _threshold = 0f;
	public float Threshold {
		set {
			if (value < 0 || value > 1) {
				Debug.LogError($"Got invalid Threshold value {value}");
				return;
			}
			_threshold = value;

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
		Alpha = DefaultAlpha;
		Beta = DefaultBeta;
		ProjRatio = DefaultProjRatio;
		CamDist = DefaultCamDist;
		ScaleR = DefaultScaleR;
		DepthMultR = DefaultDepthMultR;
		
		MeshHor = DefaultMeshHor;
		MeshVer = DefaultMeshVer;

		_rotation = Quaternion.identity;
	}

	void Start() {
		_mesh = new Mesh();
		_mesh.MarkDynamic();
		_mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; //for >65k vertices
		GetComponent<MeshFilter>().mesh = _mesh;

		_material = GetComponent<MeshRenderer>().GetComponent<Renderer>().material;

		_defaultZ = transform.localPosition.z;
		_defaultX = transform.localPosition.x;
		_defaultY = transform.localPosition.y;

		ToDefault();
	}

	void Update() {
		if (_disappearRemainingSec > 0) {
			_disappearRemainingSec -= Time.deltaTime; //This can be a negative value, but since it's always assigned from const it wouldn't matter

			if (_disappearRemainingSec > 0)
				transform.localPosition = new Vector3(_defaultX, _defaultY + 500, _defaultZ - 500); //Just move it to the back of the camera
			else
				LocalPositionUpdate(); //Restore
		}

		if (MeshWiggler == null) {
			float vInput = Input.GetAxis("Vertical") * _rotateSpeed;
			float hInput = Input.GetAxis("Horizontal") * _rotateSpeed;

			//The target rotation
			if (vInput != 0 || hInput != 0) {
				_rotation *= Quaternion.Euler(Vector3.left * vInput * Time.deltaTime);
				_rotation *= Quaternion.Euler(Vector3.up * hInput * Time.deltaTime);
				transform.localRotation = _rotation;
			}

			//Moving by mouse
			if (MoveMeshByMouse) {
				//Get the diff
				Vector3 currentMousePos = Input.mousePosition;
				Vector3 diff = currentMousePos - _lastMousePos;
				_lastMousePos = currentMousePos;

				diff = new Vector3(-diff.y, diff.x) / 32;
				transform.Rotate(diff);
			}
		}
		else {
			//Predefined movement
			transform.localRotation = MeshWiggler.GetRotation();
		}
	}

	void FixedUpdate() {
		//Restore rotation
		if (MeshWiggler == null) {
			transform.localRotation = Quaternion.Slerp(transform.localRotation, _rotation, .05f);
		}
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

		float x_gap = (float) imwidth / (x-1);
		float y_gap = (float) imheight / (y-1);			

		_vertices = new Vector3[x*y]; //<65k for uint16
		_vertices_proj = new Vector3[_vertices.Length];
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

		_ratio = ratio;
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

		if (_vertices == null || _depth == null) return;

		//The vertex array to be applied to the mesh. if we don't project, (x, y) will not be changed and thus can be reused again.
		Vector3[] targetVertices = (_projRatio == 0) ? _vertices : _vertices_proj;

		//Micro-optimizations...
		bool isThresholdSet = (_threshold > 0);
		float scale = _localScaleZ;
		
		for (int i = 0; i < _depth.Value.Length; i++) { //_alpha and _beta are assured to be positive
			float z = _depth.Value[i];

			if (isThresholdSet && z < _threshold)
				z = _targetVal;

			z = (1 / (_alpha * z + _beta)); //inverse
			z = (z * (_alpha + _beta) - 1) * _beta / _alpha; //normalize
			_vertices[i].z = z * _depthLength * _depthMultR;

			if (_projRatio == 0) 
				continue; //Continue without projecting

			/*
			Project vertices on camera

			For vertex p, we want the distance between p and z-axis (i.e. how far will p be from the center on camera) to be linearly related to the distance between p and the camera.
			The difference between p.z and Camera.z is
				p.cam_z_dist := CamDist + p.z

			Let's fix the location of vertices whose z are 0.
			Let p' be the projection of p on plane z = CamDist (i.e. z_p = 0)
			Since p.x = p'.x and p.y = p'.y,
				tan (theta_p') = sqrt(p.x^2 + p.y^2) / CamDist
			
			Using the same angle, if we let p" be the projection of p' on the original xy-plane of p,
				tan (theta_p') = tan (theta_p") = p".r / p.cam_z_dist
				=> p".r = tan (theta_p') * p.cam_z_dist
			p".r being the distance between p" and z-axis.

			Thus the (x, y) of p" can be calculated from (p.x, p.y).normalized * p".r
			*/
			/*
			Use parameter ProjRatio [0, 1] to set the magnitude
			*/

			Vector3 p = _vertices[i];

			float prop = (p.z * scale * _projRatio + CamDist) / (CamDist); //multiply p.z by scale to get the absolute value
			float orig_rad = MathF.Sqrt(p.x*p.x + p.y*p.y);
			if (orig_rad == 0) orig_rad = 0.00001f; //Avoid divide-by-zero
			float new_rad = prop * orig_rad;

			Vector2 newXY = new Vector2(p.x, p.y) / orig_rad * new_rad;

			targetVertices[i] = new Vector3(newXY.x, newXY.y, p.z);
		}

		_mesh.vertices = targetVertices;
	}

	private void SetTexture(Texture texture) {
		if (texture == null) {
			Debug.LogError("SetTexture(): Got null texture");
			return;
		}

		_material.mainTexture = texture; //This won't be seen on point clouds (can be omitted)

		//Set vertex color instead of setting texture (for point clouds)
		if (_shader.ShouldSetVertexColors) {

			int x = _depth.X;
			int y = _depth.Y;

			//Prepare the RenderTexture
			if (_rt == null || _rt.width != x || _rt.height != y) {
				_rt?.Release();
				_rt = new RenderTexture(x, y, 16);
			}
	
			//Resize
			Graphics.Blit(texture, _rt);

			//Prepare the resized Texture2D
			Texture2D tex2d = new Texture2D(_rt.width, _rt.height);

			//Move the resized texture
			RenderTexture.active = _rt;
			tex2d.ReadPixels(new Rect(0, 0, _rt.width, _rt.height), 0, 0);
			RenderTexture.active = null;

			//Extract the vertex colors...
			Color[] colors = new Color[x*y];
			for (int i = 0; i < y; i++)
				for (int j = 0; j < x; j++)
					colors[(y-1 - i)*x + j] = tex2d.GetPixel(j, i); //Why is this flipped by y-axis?

			//Destroy the Texture2D
			Destroy(tex2d);

			_mesh.colors = colors;
		}
	}

	public void SetScene(Depth depth, Texture texture=null, float ratio=0) {
		if (depth == null) {
			Debug.LogError("SetScene(): depth == null");
			return;
		}

		if (ratio <= 0) {
			if (texture != null)
				ratio = (float) texture.width / texture.height;
			else
				ratio = (float) depth.X / depth.Y;
		}

		if (_depth == null || !depth.IsSameSize(_depth) || ratio != _ratio)
			SetMeshSize(depth.X, depth.Y, ratio:ratio);

		_depth = depth;
		UpdateDepth();

		if (texture != null)
			SetTexture(texture);
	}

	public void SetShader(MeshShaders shader) {
		Shader targetshader = Shader.Find(shader.Name);
		if (targetshader == null) {
			Debug.LogError($"Mesh.SetShader(): couldn't find shader {shader.Name}");
			return;
		}

		_shader = shader;
		_material.shader = targetshader;

		//Set the initial property values, if they exist
		if (_shader.MaterialProperties != null)
			foreach (KeyValuePair<string, float> item in _shader.MaterialProperties)
				_material.SetFloat(item.Key, item.Value);
	}

	public void SetMaterialFloat(string propertyname, float value) =>
		_material.SetFloat(propertyname, value);

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	// Params
	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public void SetParam(string paramname, float value) {
		var pinfo = this.GetType().GetProperty(paramname);
		if (pinfo == null) {
			Debug.LogWarning($"SetParam(): Got invalid paramname {paramname}");
			return;
		}

		pinfo.SetValue(this, value);
	}

	public float GetParam(string paramname) {
		var pinfo = this.GetType().GetProperty(paramname);
		if (pinfo == null) {
			Debug.LogWarning($"GetParam(): Got invalid paramname {paramname}");
			throw new System.ArgumentException();
		}

		return (float) pinfo.GetValue(this);
	}

	public string ExportParams() {
		string[] toexports = new string[] {
			"Alpha", "Beta", "ProjRatio", "CamDistL", "ScaleR", "DepthMultRL", "MeshHor", "MeshVer"
		};

		StringBuilder output = new StringBuilder();

		foreach (string paramname in toexports) {
			float value = GetParam(paramname);
			output.Append($"{paramname}={value}\n");
		}

		return output.ToString();
	}

	public void ImportParams(string paramstr) {
		foreach (string line in paramstr.Split('\n')) {
			int sep_i = line.IndexOf('=');
			if (sep_i < 0)
				continue;

			string key = line.Substring(0, sep_i).Trim();

			string valueStr = line.Substring(sep_i+1).Trim();
			float value = 0f;
			try {
				value = float.Parse(valueStr);
			}
			catch (System.FormatException) {
				Debug.LogWarning($"ImportParams(): Got invalid value {valueStr}");
				continue;
			}

			SetParam(key, value);
		}
	}

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	// Depths
	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	private Depth GetInverseDepth() {
		//This exports the un-postprocessed output

		if (_depth != null && _depth.Type != DepthMapType.Inverse)
			Debug.LogWarning("GetInverseDepth(): The depth is not inverse, this will return the linear one.");

		return _depth;
	}

	private Depth GetLinearDepth() {
		//This exports the processed output (by Alpha and Beta)
		if (_vertices == null || _depth == null)
			return null;

		float[] linear = new float[_vertices.Length];
		for (int i = 0; i < _vertices.Length; i++)
			linear[i] = _vertices[i].z / _depthLength / _depthMultR;

		return new Depth(linear, _depth.X, _depth.Y, type: DepthMapType.Linear);
	}

	public Depth GetDepth(DepthMapType type) {
		switch (type) {
		case DepthMapType.Inverse:
			return GetInverseDepth();
		case DepthMapType.Linear:
			return GetLinearDepth();
		default:
			Debug.LogError($"Got unknown DepthMapType: {type}");
			return null;
		}
	}

	public void GetTextureSize(out int w, out int h) {
		Texture tex = _material.mainTexture;

		if (tex == null) {
			w = h = 0;
			return;
		}

		w = tex.width;
		h = tex.height;

		return;
	}
}