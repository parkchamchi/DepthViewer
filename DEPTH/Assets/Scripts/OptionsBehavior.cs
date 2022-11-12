using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OptionsBehavior : MonoBehaviour {
	public GameObject ScrollView;

	void Start() {
		ScrollView.SetActive(false);
	}

	public void TogglePanel() {
		ScrollView.SetActive(!ScrollView.activeSelf);
	}
}
