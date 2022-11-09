using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if !UNITY_ANDROID

public class AndroidVRBehavior : MonoBehaviour {
	
} //Destroyed elsewhere

#else

/*
Includes source from 
https://github.com/googlevr/cardboard-xr-plugin/blob/master/Samples%7E/hellocardboard-unity/Scripts/VrModeController.cs
(Apache-2.0, see ../Assets/README.txt)

Copyright 2020 Google LLC

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

   http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing,
software distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using UnityEngine.XR;
using UnityEngine.XR.Management;
using Google.XR.Cardboard;

public class AndroidVRBehavior : MonoBehaviour {
	private Camera _mainCamera;
	private float _defaultFOV; //fov of non-vr camera

	private bool _isVrModeEnabled {
        get {return XRGeneralSettings.Instance.Manager.isInitializationComplete;}
    }

	void Start() {
		_mainCamera = Camera.main;
		_defaultFOV = _mainCamera.fieldOfView;

		ExitVR();
	}

	void Update() {
		if (_isVrModeEnabled) {
            if (Api.IsCloseButtonPressed)
                ExitVR();

            if (Api.IsGearButtonPressed)
                Api.ScanDeviceParams();

            Api.UpdateScreenParams();
        }
	}

	public void EnterVR() {
        StartCoroutine(StartXR());
        if (Api.HasNewDeviceParams()) {
            Api.ReloadDeviceParams();
        }
    }

	public void ExitVR() =>
		StopXR();

	private IEnumerator StartXR() {
        Debug.Log("Initializing XR...");
        yield return XRGeneralSettings.Instance.Manager.InitializeLoader();

        if (XRGeneralSettings.Instance.Manager.activeLoader == null) {
            Debug.LogError("Initializing XR Failed.");
        }
        else {
            Debug.Log("XR initialized.");

            Debug.Log("Starting XR...");
            XRGeneralSettings.Instance.Manager.StartSubsystems();
            Debug.Log("XR started.");
        }
    }

	private void StopXR() {
		Debug.Log("Stopping XR...");
		XRGeneralSettings.Instance.Manager.StopSubsystems();
		Debug.Log("XR stopped.");

		Debug.Log("Deinitializing XR...");
		XRGeneralSettings.Instance.Manager.DeinitializeLoader();
		Debug.Log("XR deinitialized.");

		_mainCamera.ResetAspect();
		_mainCamera.fieldOfView = _defaultFOV;
	}
}

#endif