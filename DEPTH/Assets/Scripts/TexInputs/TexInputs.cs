using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface TexInputs : IDisposable {
	void UpdateTex();

	SequentialInputBehav SeqInputBehav {get {return null;}}
	void SendMsg(string msg) {}
}

public interface SequentialInputBehav {
	bool WaitingSequentialInput {get;}
	void SequentialInput(string filepath, FileTypes ftype);
}