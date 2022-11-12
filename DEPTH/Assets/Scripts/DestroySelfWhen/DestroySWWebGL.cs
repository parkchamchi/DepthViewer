using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DestroySelfWhenWebGL : MonoBehaviour {
	void Start() {
	#if UNITY_WEBGL
		Destroy(gameObject);
	#endif
	}
}