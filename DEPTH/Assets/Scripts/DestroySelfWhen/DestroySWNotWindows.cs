using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DestroySWNotWindows : MonoBehaviour {
	void Start() {
	#if !UNITY_STANDALONE_WIN && !UNITY_EDITOR_WIN
		Destroy(gameObject);
	#endif
	}
}
