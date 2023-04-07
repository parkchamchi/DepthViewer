using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DirIterator : MonoBehaviour {
	public string[] DirNameList;
	public int DirIdx = -1; //Ideally this should be a property, but it writing the editor script is such boresome. I'll just check it on Update() and call the proper callback.
	private int _curIdx = -1;

	private MainBehavior _mainBehav;

	void Start() {
		_mainBehav = Utils.GetMainBehav();
		if (_mainBehav == null)
			Debug.LogError("DirIterator.Start(): Couldn't find the MainBehavior.");
	}

	void Update() {
		/* Get the key input */
		if (DirNameList != null && DirNameList.Length > 0) {
			if (Input.GetKeyDown(Keymapper.Inst.PrevDir)) {
				DirIdx--;
				if (DirIdx < 0) DirIdx += DirNameList.Length; //Wrap
			}
			else if (Input.GetKeyDown(Keymapper.Inst.NextDir)) {
				DirIdx++;
				DirIdx %= DirNameList.Length;
			}
			else if (Keymapper.Inst.DirRandomAccessKeys != null) {
				KeyCode[] keys = Keymapper.Inst.DirRandomAccessKeys;

				for (int i = 0; i < keys.Length; i++) {
					if (Input.GetKeyDown(keys[i])) 
						DirIdx = i;
				}
			}
		}

		/* Check if DirIdx changed */
		if (DirIdx == _curIdx) return;

		//DirIdx changed!
		_curIdx = DirIdx;

		if (_curIdx < 0) {
			Debug.Log("DirIterator: Resetting.");
			_mainBehav?.ClearBrowseDir();
			return;
		}

		if (_curIdx >= DirNameList.Length) {
			Debug.LogWarning($"#{_curIdx} is out of range of DirNameList[]: {DirNameList.Length}");
			return;
		}

		string path = DirNameList[_curIdx];
		Debug.Log($"Using dir #{_curIdx}: {path}");
		_mainBehav?.SetBrowseDirName(path);
		return;
	}
}