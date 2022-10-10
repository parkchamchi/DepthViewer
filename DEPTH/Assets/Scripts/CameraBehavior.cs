using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraBehavior : MonoBehaviour {
	private const float _defaultZ = -150;

	public void SetZ(float offset) {
		transform.position = new Vector3(0, 0, _defaultZ + offset);
	}
}
