namespace irv.src;
public struct Color {
	public byte r, g, b, a;
	public Color(uint color) {
		a = (byte)(color >> 24);
		b = (byte)(color >> 16);
		g = (byte)(color >> 8);
		r = (byte)(color >> 0);
	}
	public Color(float r, float g, float b, float a = 1) {
		this.r = (byte)(r * 255);
		this.g = (byte)(g * 255);
		this.b = (byte)(b * 255);
		this.a = (byte)(a * 255);
	}
	public static implicit operator Color(uint color) => new Color(color);
	public static Color clear = 0x00000000;
	public static Color red = 0xff0000ff;
	public static Color green = 0xff00ff00;
	public static Color blue = 0xffff0000;
	public static Color yellow = 0xff00ffff;
	public static Color cyan = 0xffffff00;
	public static Color magenta = 0xffff00ff;
	public bool Equals(Color c) {
		return r == c.r && g == c.g && b == c.b && a == c.a;
	}
	public static bool operator ==(Color a, Color b) => a.Equals(b);
	public static bool operator !=(Color a, Color b) => !a.Equals(b);
}
