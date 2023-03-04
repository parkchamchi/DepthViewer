using System;
using System.Linq;

using UnityEngine;

public enum DepthMapType {Inverse, Linear, Metric}

public class Depth {
	private float[] _value;
	public ReadOnlySpan<float> Value {get {return new ReadOnlySpan<float>(_value);}}
	
	public readonly int X;
	public readonly int Y;

	public DepthMapType Type;

	public float Min => _value.Min();
	public float Max => _value.Max();

	public Depth(float[] value, int x, int y, DepthMapType type=DepthMapType.Inverse) {
		if (value == null) {
			Debug.LogError("Depth(): got null value");
			return;
		}
		if (value.Length == 0) {
			Debug.LogError("Depth(): value.Length == 0");
			return;
		}
		if (x*y != value.Length) {
			Debug.LogError($"Depth(): ({x} * {y} == {x*y}) does not equal value.Length {value.Length}");
			return;
		}

		_value = (float[]) value.Clone();
		X = x;
		Y = y;
		Type = type;
	}

	public bool IsSameSize(Depth tocompare) {
		if (tocompare == null) {
			Debug.LogError("Depth.IsSameSize(): tocompare == null");
			return false;
		}

		return ((X == tocompare.X) && (Y == tocompare.Y));
	}

	public static bool IsValid(Depth depth) =>
		((depth != null) && (depth.Value != null));

	public Depth MetricToLinear() {
		if (Type != DepthMapType.Metric) {
			Debug.LogError("MetricToLinear(): depth map is not metric");
			return null;
		}

		float[] lin = new float[_value.Length];
		float min = Min, max = Max;

		//Normalize
		for (int i = 0; i < lin.Length; i++)
			lin[i] = (_value[i] - min) / (max - min);

		return new Depth(lin, X, Y, DepthMapType.Linear);
	}
}

public interface BaseDepthModel : IDisposable {
	string ModelType {get;}	
}

public interface DepthModel : BaseDepthModel {
	Depth Run(Texture inputTexture);
}

public interface AsyncDepthModel {
	bool IsAvailable {get;}
	bool IsWaiting {get;}

	delegate bool DepthReadyCallback(Depth depth);

	void Run(Texture tex, DepthReadyCallback callback);
}