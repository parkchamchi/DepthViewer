using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using TMPro;

using IngameDebugConsole;

public class WorldTextPlaneBehavior : MonoBehaviour {
	public TMP_Text WorldText;

	private IDepthMesh _dmesh;
	private Dictionary<string, float> _paramd;
	private string _str;

	private readonly string[] _paramnames = {"Alpha", "Beta", "ProjRatio", "CamDistL", "ScaleR", "DepthMultRL", "MeshOffZ"};
	private int _paramIdx = 0;

	private const float _paramInterval = 0.01f;

	void Start() {
		_str = "......";
		_dmesh = Utils.GetDepthMesh();

		_paramd = new Dictionary<string, float>();
		foreach (string k in _paramnames)
			_paramd.Add(k, 0);

		foreach (string k in _paramnames) //seperate loop
			_paramd[k] = _dmesh.GetParam(k);

		_dmesh.ParamChanged += onParamChanged;
		getText();

		DebugLogConsole.AddCommandInstance("worldtextplane.deactivate", "Deactivate the screen on the left", "Deactivate", this);
	}

	void Update() {
		int origParamIdx = _paramIdx;
		float delta = 0f;

		if (Input.GetKeyDown(Keymapper.Inst.ParamPrev))
			_paramIdx--;
		else if (Input.GetKeyDown(Keymapper.Inst.ParamNext))
			_paramIdx++;
		else if (Input.GetKey(Keymapper.Inst.ParamLower))
			delta = -_paramInterval;
		else if (Input.GetKey(Keymapper.Inst.ParamIncrease))
			delta = +_paramInterval;

		//Keep the index in bound
		int len = _paramnames.Length;
		_paramIdx = ((_paramIdx % len) + len) % len; 

		if (delta != 0f) {
			string paramname = _paramnames[_paramIdx];
			float val  = _paramd[paramname] + delta;

			//Exceptional case: log
			if (paramname == "DepthMultRL" && val == float.NegativeInfinity && delta > 0)
				val = -1 + 0.00001f; //significantly small value

			_dmesh.SetParam(paramname, val);
		}

		//Update the text
		if (origParamIdx != _paramIdx || delta != 0f)
			getText();

		WorldText.text = _str;
	}

	public void Deactivate() {
		Debug.Log("Deactivating the worldtextplane");
		this.gameObject.SetActive(false);
	}

	//method names below have cause issues ig

	private void getText() {
		string newstr = "";

		/*
		string statusText = "";
		TMP_Text statusTextObj = UITextSet.StatusText;
		if (statusTextObj != null)
			newstr += statusTextObj.text + "\n\n";
		*/

		string curParam = _paramnames[_paramIdx];
		foreach (string k in _paramnames) {
			if (k == curParam) //Show the current param
				newstr += "> ";
			newstr += $"{k}: {_paramd[k].ToString("0.##")}\n";
		}

		_str = newstr;
	}

	private void onParamChanged(string paramname, float val) {
		int index = Array.IndexOf(_paramnames, paramname);
		if (index < 0) return;

		_paramd[paramname] = val;

		getText();
	}
}
