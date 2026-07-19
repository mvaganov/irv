#define KeepLogs
using src.Core;
using System.Diagnostics;
using System.Text;
namespace src;

public static class Log {
	public enum Code {
		/// <summary>
		/// Unprintable log type. If this is the visibility, never print anything
		/// </summary>
		None = 0,
		/// <summary>
		/// failed asserts: something has already gone very wrong, likely the final error message from the thread
		/// </summary>
		Critical = 1,
		/// <summary>
		/// Log.e: when things are about to go very wrong
		/// </summary>
		Error = 2,
		/// <summary>
		/// Log.w: when things could go wrong
		/// </summary>
		Warning = 3,
		/// <summary>
		/// Log.i: helpful markers through a process
		/// </summary>
		Info = 4,
		/// <summary>
		/// Log.d: additional info for a programmer debugging something, likely temporary code
		/// </summary>
		Debug = 5,
		/// <summary>
		/// Log.v: even more additional info for a programmer debugging. for messages that are useful enough to keep forever, but not for everyone
		/// </summary>
		Verbose = 6,
		/// <summary>
		/// successful asserts: if you're using Asserts well, prepare for so much spam.
		/// </summary>
		SuccessfulAsserts = 8
	}
	public static ConsoleColor SpecialColor = ConsoleColor.Yellow;
	public static ConsoleColor LiteralColor = ConsoleColor.Cyan;
	public class Visibility {
		public Code code; public ConsoleColor color;
		public Visibility(Code code, ConsoleColor color) { this.code = code; this.color = color; }
	}
	static Log() {
		GenerateVisibilities();
		File.WriteAllText(LogFile, DateTime.Now.ToString() + "\n");
	}
	public static void GenerateVisibilities() {
		Visibilities = new Visibility[] {
			new Visibility(Code.None, Console.ForegroundColor),
			new Visibility(Code.Critical, ConsoleColor.Magenta),
			new Visibility(Code.Error, ConsoleColor.Red),
			new Visibility(Code.Warning, ConsoleColor.Yellow),
			new Visibility(Code.Info, ConsoleColor.White),
			new Visibility(Code.Debug, ConsoleColor.DarkGreen),
			new Visibility(Code.Verbose, ConsoleColor.DarkGray),
			new Visibility(Code.SuccessfulAsserts, ConsoleColor.DarkMagenta),
		};
	}
	private static StackableVariableStore VariableContext = new StackableVariableStore();
	public static string GetValue(string key) => VariableContext.TryGetValue(key, out object? value) ? value?.ToString() ?? string.Empty : string.Empty;
	public static void PushContext(string key, object value) => VariableContext.PushContext(key, value);
	public static void PopContext(string key) => VariableContext.PopContext(key);
	public static Visibility[] Visibilities = new Visibility[0];
	public static string LogFile = "log.txt";
	public const char ColorSuffix = '\b';
	public static readonly string ColorReset = "-" + ColorSuffix;
	public static string ColorCode(ConsoleColor color) => ((int)color).ToString("X") + ColorSuffix;
	public static Code visibility = Code.Verbose;
	public static bool IdentifySourceCode = true;
	public static bool AssertThrowsException = true;
	public static void v(object? text) => PrintWithVisibilityInternal(text, Code.Verbose, true, IdentifySourceCode);
	public static void d(object? text) => PrintWithVisibilityInternal(text, Code.Debug, true, IdentifySourceCode);
	public static void i(object? text) => PrintWithVisibilityInternal(text, Code.Info, true, IdentifySourceCode);
	public static void w(object? text) => PrintWithVisibilityInternal(text, Code.Warning, true, IdentifySourceCode);
	public static void e(object? text) => PrintWithVisibilityInternal(text, Code.Error, true, IdentifySourceCode);
	public static void f(object? text) {
		PrintWithVisibilityInternal(text, Code.Critical, true, IdentifySourceCode);
		if (AssertThrowsException) {
			StackTrace t = new StackTrace(2);
			PrintWithVisibilityInternal(t.ToString(), Code.Verbose, true, IdentifySourceCode);
			if (text == null) text = string.Empty;
			throw new Exception(text.ToString());
		}
	}
	public static void Assert(bool condition, string message) {
		if (condition) {
			PrintWithVisibilityInternal(message, Code.SuccessfulAsserts, true, IdentifySourceCode);
		} else {
			f(message);
		}
	}
	public static void PrintWithVisibility(object? msg, Code minimumVisibility, bool endline)
		=> PrintWithVisibilityInternal(msg, minimumVisibility, endline, IdentifySourceCode);
	private static void PrintWithVisibilityInternal(object? msg, Code visibility, bool endline, bool identifySourceCode) {
#if KeepLogs
		if (Log.visibility < visibility) { return; }
		if (identifySourceCode) {
			ShowSourceCodeMetaData(visibility);
		}
		ForcePrintWithVisibility(msg, visibility, endline);
#endif
	}
	private static void ShowSourceCodeMetaData(Code visibility) {
		ConsoleColor oldFColor = Console.ForegroundColor;
		ConsoleColor oldBColor = Console.BackgroundColor;
		Console.ForegroundColor = oldBColor;
		Console.BackgroundColor = oldFColor;
		Write(Log.StackPosition(4));
		Console.ForegroundColor = oldFColor;
		Console.BackgroundColor = oldBColor;
	}
	public static string StackPosition(int framesBack = 1, bool includeFullPath = false) {
		StackFrame? frame = new StackTrace(true)?.GetFrame(framesBack);
		if (frame == null) { return "?:?"; }
		string currentFile = frame.GetFileName() ?? "?";
		if (!includeFullPath) {
			int lastFolder = currentFile.LastIndexOf('/');
			if (lastFolder < 0) {
				lastFolder = currentFile.LastIndexOf('\\');
			}
			lastFolder += 1;
			int fileSuffix = currentFile.LastIndexOf('.');
			if (fileSuffix < 0) {
				fileSuffix = currentFile.Length;
			}
			int suffixSize = currentFile.Length - fileSuffix;
			currentFile = currentFile.Substring(lastFolder, currentFile.Length - lastFolder - suffixSize);
		}
		int currentLine = frame.GetFileLineNumber();
		return $"{currentFile}:{currentLine}";
	}
	public static void ForcePrintWithVisibility(object? message, Code visiblity, bool endLine) {
		ConsoleColor oldColor = Console.ForegroundColor;
		Console.ForegroundColor = Visibilities[(int)visiblity].color;
		if (message != null) {
			Write(message.ToString()!);
		}
		Console.ForegroundColor = oldColor;
		if (endLine) {
			Write("\n");
		}
	}
	public static void WriteLine(object text) => WriteLine(text?.ToString() ?? "");
	public static void WriteLine(string text) => Write(text + "\n");
	public static string ProcessQuoteColors(string text) {
		StringBuilder sb = new StringBuilder();
		bool inDoubleQuotes = false;
		bool inBacktickQuotes = false;
		string? colorChange = null;
		foreach (char c in text) {
			switch (c) {
				case '\"':
					inDoubleQuotes = !inDoubleQuotes;
					colorChange = inDoubleQuotes ? ColorCode(LiteralColor) : ColorReset;
					break;
				case '`':
					inBacktickQuotes = !inBacktickQuotes;
					colorChange = inBacktickQuotes ? ColorCode(SpecialColor) : ColorReset;
					break;
			}
			if (colorChange == ColorReset) {
				sb.Append(colorChange);
				colorChange = null;
			}
			sb.Append(c);
			if (colorChange != null) {
				sb.Append(colorChange);
				colorChange = null;
			}
		}
		return sb.ToString();
	}
	public static string Format(string text) {
		text = VariableContext?.ProcessVariables(text) ?? text;
		text = ProcessQuoteColors(text);
		return text;
	}
	public static void Write(string text) {
		text = Format(text);
		int start = 0, end;
		ConsoleColor defaultColor = Console.ForegroundColor;
		char c = '\0';
		while (start < text.Length) {
			end = text.Length;
			for (int i = start; i < text.Length; ++i) {
				c = text[i];
				if (c == ColorSuffix) {
					end = i - 1;
					break;
				}
			}
			int count = end - start;
			string substring = text.Substring(start, count);
			Console.Write(substring);
			if (c == ColorSuffix && end >= 0) {
				c = text[end];
				if (c == ColorReset[0]) {
					Console.ForegroundColor = defaultColor;
				} else {
					Console.ForegroundColor = (ConsoleColor)GetNumberFromHexCode(c);
				}
			}
			start = end + 2;
		}
		Console.ForegroundColor = defaultColor;
	}
	static int GetNumberFromHexCode(char c) {
		return (c >= '0' && c <= '9') ? c - '0' : (c >= 'A' && c <= 'F') ? c - 'A' + 10 : (c >= 'a' && c <= 'f') ? c - 'a' + 10 : 0;
	}
	public static string StripColorCodes(string text) {
		StringBuilder sb = new StringBuilder();
		int start = 0;
		for (int i = 0; i < text.Length; ++i) {
			char c = text[i];
			if (c == ColorSuffix) {
				int end = i - 1;
				sb.Append(text.Substring(start, end - start));
				start = i + 1;
			}
		}
		if (start == 0) {
			return text;
		}
		sb.Append(text.Substring(start));
		return sb.ToString();
	}

}
