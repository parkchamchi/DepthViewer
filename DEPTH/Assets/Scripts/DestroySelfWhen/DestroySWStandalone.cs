using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DestroySWStandalone : MonoBehaviour {
	void Start() {
	#if UNITY_STANDALONE
		Destroy(gameObject);
	#endif
	}
}
