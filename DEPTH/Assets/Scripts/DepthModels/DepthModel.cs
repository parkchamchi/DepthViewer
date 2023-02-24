using System;

using UnityEngine;

public enum DepthMapType {Inverse, Linear}

public class Depth {
	private float[] _value;
	public ReadOnlySpan<float> Value {get {return new ReadOnlySpan<float>(_value);}}
	
	public readonly int X;
	public readonly int Y;

	public readonly DepthMapType Type;

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
}

public interface BaseDepthModel : IDisposable {
	string ModelType {get;}	
}

public interface DepthModel : BaseDepthModel {
	Depth Run(Texture inputTexture);
}

public interface AsyncDepthModel : BaseDepthModel {
	bool IsAvailable {get;}
	bool IsWaiting {get;}

	delegate bool DepthReadyCallback(Depth depth);

	void Run(Texture tex, DepthReadyCallback callback);
}