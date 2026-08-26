using System.Diagnostics.CodeAnalysis;
namespace irv.src;
public struct Color {
	public static readonly Color clear = 0x00000000, black = 0xff000000, white = 0xffffffff,
		red = 0xff0000ff, darkRed = 0xff000088, green = 0xff00ff00, darkGreen = 0xff008800,
		blue = 0xffff0000, darkBlue = 0xff880000, yellow = 0xff00ffff, darkYellow = 0xff008888,
		cyan = 0xffffff00, darkCyan = 0xff888800, magenta = 0xffff00ff, darkMagenta = 0xff880088,
		gray = 0xff888888, darkGray = 0xff444444;
	private static Color[] UnambiguousColorSequence = new Color[]{
		red, green, blue, yellow, cyan, magenta, darkRed, new Color(.75f,1,.75f), darkBlue,
		new Color(1,1,.75f), darkCyan, new Color(1,.75f,1), darkYellow, new Color(.75f,1,1),
		darkMagenta, new Color(1,.75f,.75f),darkGreen,new Color(.75f,.75f,1),
		new Color(1,.5f,0),new Color(0,1,.5f),new Color(.5f,0,1),
		new Color(1,0,.5f),new Color(.5f,1,0),new Color(0,.5f,1),
		new Color(.25f,.5f,0),new Color(0,.25f,.5f),new Color(.5f,0,.25f)
	};
	public static readonly (ConsoleColor, Color)[] ConsoleColorMap = new (ConsoleColor, Color)[] {
		(ConsoleColor.Gray, gray),    (ConsoleColor.DarkGray, darkGray),
		(ConsoleColor.White, white),  (ConsoleColor.Black, black),
		(ConsoleColor.Red, red),      (ConsoleColor.DarkRed, darkRed),
		(ConsoleColor.Green, green),  (ConsoleColor.DarkGreen, darkGreen),
		(ConsoleColor.Blue, blue),    (ConsoleColor.DarkBlue, darkBlue),
		(ConsoleColor.Yellow, yellow),(ConsoleColor.DarkYellow, darkYellow),
		(ConsoleColor.Cyan, cyan),    (ConsoleColor.DarkCyan, darkCyan),
		(ConsoleColor.Magenta,magenta),(ConsoleColor.DarkMagenta, darkMagenta),
	};

	public byte r, g, b, a;
	public Color(uint color) {
		a = (byte)(color >> 24); b = (byte)(color >> 16); g = (byte)(color >> 8); r = (byte)(color >> 0);
	}
	public Color(float R, float G, float B, float A = 1) {
		r = (byte)(R * 255); g = (byte)(G * 255); b = (byte)(B * 255); a = (byte)(A * 255);
	}
	public static implicit operator Color(uint color) => new Color(color);
	public UInt32 GetUInt32() => (UInt32)a << 24 | (UInt32)b << 16 | (UInt32)g << 8 | r;
	public bool Equals(Color c) => r == c.r && g == c.g && b == c.b && a == c.a;
	public override int GetHashCode() => (int)GetUInt32();
	public override bool Equals([NotNullWhen(true)] object? obj) => obj is Color c && Equals(c);
	public static bool operator ==(Color a, Color b) => a.Equals(b);
	public static bool operator !=(Color a, Color b) => !a.Equals(b);
	public static float Distance(Color a, Color b) {
		float R = b.r - a.r, G = b.g - a.g, B = b.b - a.b;
		float magnitude = (float)Math.Sqrt(R * R + G * G + B * B);
		return magnitude;
	}
	public static implicit operator ConsoleColor(Color c) => ConvertToConsoleColor(c);
	public static ConsoleColor ConvertToConsoleColor(Color c) {
		ConsoleColor result = ConsoleColor.Gray;
		float bestDist = float.PositiveInfinity;
		for(int i = 0; i < ConsoleColorMap.Length; ++i) {
			float dist = Distance(c, ConsoleColorMap[i].Item2);
			if (dist < bestDist) {
				bestDist = dist;
				result = ConsoleColorMap[i].Item1;
			}
		}
		return result;
	}
	public static void AssignUniqueColors<T>(IList<T> candidates, Func<T, Color> getColor, Action<T, Color> setColor, float minDistance = 32) {
		AssignUniqueColors(candidates, UnambiguousColorSequence, getColor, setColor, minDistance);
	}
	/// <summary>Generates a default color for each candidate with <see cref="clear"/></summary>
	public static void AssignUniqueColors<T>(IList<T> candidates, IList<Color> colorList, Func<T,Color> getColor, Action<T, Color> setColor, float minDistance = 32) {
		List<Color> colors = new List<Color>(colorList);
		// remove auto-colors that are too close to the existing candidates
		for (int i = 0; i < candidates.Count; ++i) {
			Color thisColor = getColor(candidates[i]);
			if (thisColor == Color.clear) continue;
			var mostSimilarColors = colors.OrderBy(c => Distance(c, thisColor));
			foreach (Color similarColor in mostSimilarColors) {
				float dist = Distance(similarColor, thisColor);
				if (dist > minDistance) break;
				colors.Remove(similarColor);
			}
		}
		int colorindex = 0;
		foreach (T k in candidates) {
			if (getColor(k) != clear) continue;
			setColor(k, colors[(colorindex++) % colors.Count]);
		}
	}
}
