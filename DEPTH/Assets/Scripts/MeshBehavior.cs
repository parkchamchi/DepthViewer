using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshBehavior : MonoBehaviour {
	private Mesh _mesh;
	private Vector3[] _vertices;
	private Vector2[] _uv;
	private	int[] _triangles;
	private Material _material;

	private DepthONNX _donnx;

	private float _width = 320; //canvas size
	private float _height = 180;

	private int _x = 320; //pixels of the input
	private int _y = 180;

	private float _depthMult = 50f;

	private float[][] _depths;
	private int _depth_idx;


	void Start() {
		_mesh = new Mesh();
		_mesh.MarkDynamic();
		_mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; //for >65k vertices
		GetComponent<MeshFilter>().mesh = _mesh;

		_material = GetComponent<MeshRenderer>().GetComponent<Renderer>().material;

		int x, y;
		double orig_ratio;
		string depth_path = @"";
		string texture_path = @"";

		_depths = Utils.ReadDepth(depth_path, out x, out y, out orig_ratio);
		Texture2D texture = Utils.LoadImage(texture_path);

		_depth_idx = 0;
		SetScene(_depths[_depth_idx++], x, y, orig_ratio, texture);
	}

	void Update() {
		if (_depths == null) return;

		if (_depth_idx == _depths.Length)
			_depth_idx = 0;

		SetDepth(_depths[_depth_idx++]);
	}

	void GetDepthONNX() {
		if (_donnx == null)
			_donnx = GameObject.Find("DepthONNX").GetComponent<DepthONNXBehavior>().GetDepthONNX();
	}

	private void SetMeshSize(int x, int y, double ratio=0) {
		/*
			x, y: pixel count of DEPTH.
			ratio: (width/height) of IMAGE.
		*/

		_mesh.Clear();

		/*Size of the image on canvas: that is, image fit to (_width, _height), while retaining the ratio. Thus these would not exceed _width and _height.*/
		float imwidth = 0;
		float imheight = 0;

		//If ratio is not given, use x/y.
		if (ratio == 0)
			ratio = (double) x/y;

		//Check ratio
		if (ratio > (double) _width/_height) {
			//longer width
			imwidth = _width;
			imheight = (imwidth/x) * y;
		}
		else {
			//longer height
			imheight = _height;
			imwidth = (imheight/y) * x;
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
	}

	private void SetDepth(float[] depths) {
		if (depths.Length != _x*_y) {
			Debug.LogError("depths.Length " + depths.Length + " does not match _x*_y " + _x*_y + " .");
			return;
		}

		for (int i = 0; i < depths.Length; i++)
			_vertices[i].z = -depths[i] * _depthMult;
		_mesh.vertices = _vertices;
	}

	public void SetScene(float[] depths, int x, int y, double ratio=0, Texture2D texture=null) {
		if (x*y != depths.Length) {
			Debug.LogError("x*y " + x*y + " does not match depths.Length " + depths.Length + " .");
			return;
		}

		if (x != _x || y != _y)
			SetMeshSize(x, y, ratio:ratio);
		SetDepth(depths);

		if (texture)
			_material.mainTexture = texture;
	}

	void Kkumteul() {
		for (int i = 0; i < _x*_y; i++) {
			_vertices[i].z += Random.Range(-0.1f, 0.1f);
		}
		_mesh.vertices = _vertices;
	}
}

//no stretching?