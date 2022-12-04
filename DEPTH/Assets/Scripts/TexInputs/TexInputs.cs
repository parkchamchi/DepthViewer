using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface TexInputs : IDisposable {
	void UpdateTex();

	bool WaitingSequentialInput {get;}
	void SequentialInput(string filepath, FileTypes ftype);
}