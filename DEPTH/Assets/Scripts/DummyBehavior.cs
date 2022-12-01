using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface CanRunCoroutine {
	Coroutine StartUnityCoroutine(IEnumerator routine);
}

public class DummyBehavior : MonoBehaviour, CanRunCoroutine {
	public Coroutine StartUnityCoroutine(IEnumerator routine) {
		return StartCoroutine(routine);
	}
}
