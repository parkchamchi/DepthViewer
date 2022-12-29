using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface CanRunCoroutine {
	Coroutine StartCoroutine(IEnumerator routine);
	void StopCoroutine(IEnumerator routine);
}

public class DummyBehavior : MonoBehaviour, CanRunCoroutine {
	
}
