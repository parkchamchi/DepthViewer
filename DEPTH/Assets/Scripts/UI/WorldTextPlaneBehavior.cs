using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using TMPro;

public class WorldTextPlaneBehavior : MonoBehaviour {
	public TMP_Text WorldText;

	private IDepthMesh _dmesh;
	private Dictionary<string, float> _paramd;
	private string _str;

	private readonly string[] _paramnames = {"Alpha", "Beta", "ProjRatio", "CamDistL", "ScaleR", "DepthMultRL"};

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
	}

	void Update() {
		WorldText.text = _str;
	}

	private void getText() {
		string newstr = "";

		/*
		string statusText = "";
		TMP_Text statusTextObj = UITextSet.StatusText;
		if (statusTextObj != null)
			newstr += statusTextObj.text + "\n\n";
		*/

		foreach (string k in _paramnames)
			newstr += $"{k}: {_paramd[k]}\n";

		_str = newstr;
	}

	private void onParamChanged(string paramname, float val) {
		int index = Array.IndexOf(_paramnames, paramname);
		if (index < 0) return;

		_paramd[paramname] = val;

		getText();
	}
}
