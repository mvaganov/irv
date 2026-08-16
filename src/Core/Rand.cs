
namespace src.Core;
public class Rand {
	private static Rand? _instance;
	public static Rand Instance => _instance != null ? _instance : _instance = new Rand();
	public uint Seed = 2463534242; // seed (Xorshift requires non-zero)
	public const uint FloatResolutionBitmask = 0xffffff;
	public static uint Timestamp => (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
	public Rand(uint seed = 2463534242) { Seed = seed != 0 ? seed : Timestamp; }
	public Rand Random() => new Rand((uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds());
	/// <summary>Xorshift32</summary>
	uint Next() {
		Seed ^= Seed << 13;
		Seed ^= Seed >> 17;
		Seed ^= Seed << 5;
		return Seed;
	}
	public uint Next(uint max) => Next() % max;
	public int Next(int max) => (int)Next((uint)max);
	public float GetNumber() => (Instance.Next() & FloatResolutionBitmask) / (float)(FloatResolutionBitmask);
	public float GetNumber(float min, float max) => (Instance.GetNumber() * (max - min)) + min;
	public static float Number => Instance.GetNumber();
	public static float Range(float min, float max) => Instance.GetNumber(min, max);
}
