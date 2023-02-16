using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class WindowManager {

	private static GameObject _currentWindow;

	public static void SetCurrentWindow(GameObject window) {
		//If it's the same window, toggle it 
		if (_currentWindow != null && window == _currentWindow)
			_currentWindow.SetActive(!_currentWindow.activeSelf);
		else {
			_currentWindow?.SetActive(false); //Close the former one...
			_currentWindow = window;
			window.SetActive(true);
		}
	}
}
