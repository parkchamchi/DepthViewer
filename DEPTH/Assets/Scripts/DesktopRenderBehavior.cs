using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DesktopRenderBehavior : MonoBehaviour {

	public GameObject MainButton;
	public GameObject MainPanel;

	private bool _supported;
	public bool Supported {get {return _supported;}}

	/* TMP */
	private Texture2D[] _placeholders;
	private int _placeholderIdx;

	void Start() {
		/* Only supports Windows */
	#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
		_supported = true;
	#else
		_supported = false;
	#endif

		MainButton.SetActive(_supported);
		MainPanel.SetActive(false);

		/* TMP */
		_placeholders = new Texture2D[2] {Utils.LoadImage("d:/saul.jpg"), Utils.LoadImage("d:/todd.jpg")};
		_placeholderIdx = 0;
	}

	public Texture2D Get(out int width, out int height) {
		_placeholderIdx = (++_placeholderIdx % _placeholders.Length);
		Texture2D texture =  _placeholders[_placeholderIdx];

		width = texture.width;
		height = texture.height;

		return texture;
	}

	public void TogglePanel() {
		MainPanel.SetActive(!MainPanel.activeSelf);
	}
}
