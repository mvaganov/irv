using System.Diagnostics.CodeAnalysis;
namespace irv.src;
public struct Color {
	public byte r, g, b, a;
	public Color(uint color) {
		a = (byte)(color >> 24); b = (byte)(color >> 16); g = (byte)(color >> 8); r = (byte)(color >> 0);
	}
	public Color(float R, float G, float B, float A = 1) {
		r = (byte)(R * 255); g = (byte)(G * 255); b = (byte)(B * 255); a = (byte)(A * 255);
	}
	public static implicit operator Color(uint color) => new Color(color);
	public UInt32 GetUInt32() => (UInt32)a << 24 | (UInt32)b << 16 | (UInt32)g << 8 | r;
	public static Color clear = 0x00000000, red = 0xff0000ff, green = 0xff00ff00, blue = 0xffff0000,
		yellow = 0xff00ffff, cyan = 0xffffff00, magenta = 0xffff00ff;
	public bool Equals(Color c) => r == c.r && g == c.g && b == c.b && a == c.a;
	public override int GetHashCode() => (int)GetUInt32();
	public override bool Equals([NotNullWhen(true)] object? obj) => obj is Color c && Equals(c);
	public static bool operator ==(Color a, Color b) => a.Equals(b);
	public static bool operator !=(Color a, Color b) => !a.Equals(b);
}
