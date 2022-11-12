using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DestroySWWebGL : MonoBehaviour {
	void Start() {
	#if UNITY_WEBGL
		Destroy(gameObject);
	#endif
	}
}