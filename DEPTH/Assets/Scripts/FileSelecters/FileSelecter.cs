using UnityEngine;

public delegate void OnPathSelected(string path);

public interface FileSelecter {
	void SelectFile(OnPathSelected callback);
	void SelectDir(OnPathSelected callback) {Debug.LogError("Not implemented.");}
}