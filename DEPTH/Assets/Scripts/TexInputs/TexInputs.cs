using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface TexInputs : IDisposable {
	void UpdateTex();

	bool WaitingSequentialInput {get {return false;}}
	void SequentialInput(string filepath, FileTypes ftype) {Debug.LogError("SequentialInput(): Not implemented.");}

	void SendMsg(string msg) {}
}