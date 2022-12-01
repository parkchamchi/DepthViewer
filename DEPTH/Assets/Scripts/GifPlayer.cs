using System.Collections;
using System.Collections.Generic;
using System.IO;

using UnityEngine;

public class GIFPlayer : MonoBehaviour {
	public GIFPlayer(string filepath) {
		if (!File.Exists(filepath)) {
			Debug.LogError($"File does not exist: {filepath}");
			return;
		}

		byte[] bytes = File.ReadAllBytes(filepath);
		//...
	}
}