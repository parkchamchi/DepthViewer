using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DestroySelfWhenStandalone : MonoBehaviour {
	void Start() {
	#if UNITY_STANDALONE
		Destroy(gameObject);
	#endif
	}
}
