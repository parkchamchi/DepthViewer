using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DummyBehavior : MonoBehaviour {
	public Coroutine StartUnityCoroutine(IEnumerator routine) =>
		StartCoroutine(routine);
}
