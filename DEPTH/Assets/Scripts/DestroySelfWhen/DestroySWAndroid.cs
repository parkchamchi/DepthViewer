using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DestroySelfWhenAndroid : MonoBehaviour {
	void Start() {
	#if UNITY_ANDROID
		Destroy(gameObject);
	#endif
	}
}
