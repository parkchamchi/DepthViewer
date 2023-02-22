using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RecenterCameraBehavior : MonoBehaviour {
	/* Sets TargetParent's transform as TargetCamera's */

	public GameObject TargetParent;
	public Camera TargetCamera;

	//private Vector3 _origPosition;
	private Quaternion _origRotation;

	void Start() {
		_origRotation = TargetParent.transform.rotation;
	}

	public void Reset() {
		TargetParent.transform.rotation = _origRotation;
	}

	public void Recenter() {
		/* Add offset of position & rotation */
		Reset();
		Quaternion offset = TargetCamera.transform.rotation;
		offset.eulerAngles = new Vector3(offset.eulerAngles.x, offset.eulerAngles.y, 0); //ignore z axis (yaw)
		TargetParent.transform.rotation *=  Quaternion.Inverse(offset);
	}
}
