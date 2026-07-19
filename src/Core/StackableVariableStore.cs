using System.Collections;
using System.Diagnostics.CodeAnalysis;
namespace src.Core;

public class StackableVariableStore : IDictionary<string, object?> {
	public const string VariablePrefix = "${";
	public const string VariableSuffix = "}";
	public Dictionary<string, object?> vars = new Dictionary<string, object?>();
	private Dictionary<string, List<object?>>? ContextStacks = null;
	public void PushContext(string key, object? value) {
		if (ContextStacks == null) { ContextStacks = new Dictionary<string, List<object?>>(); }
		if (!ContextStacks.TryGetValue(key, out List<object?>? stack)) {
			stack = new List<object?>();
			ContextStacks[key] = stack;
		}
		stack.Add(value);
		vars[key] = value;
	}
	public void PopContext(string key) {
		if (ContextStacks != null && ContextStacks.TryGetValue(key, out List<object?>? stack) && stack.Count > 0) {
			if (stack.Count > 0) {
				object? value = stack[stack.Count - 1];
				stack.RemoveAt(stack.Count - 1);
				vars[key] = value;
			} else {
				vars.Remove(key);
			}
		}
	}
	public string ProcessVariables(string text) {
		for (int i = 0; i < text.Length; ++i) {
			char c = text[i];
			if (c == VariablePrefix[0] && text.Substring(i).StartsWith(VariablePrefix)) {
				int varNameStart = i + VariablePrefix.Length;
				int varNameEnd = text.IndexOf(VariableSuffix, varNameStart);
				if (varNameEnd > varNameStart) {
					string varName = text.Substring(varNameStart, varNameEnd - varNameStart);
					if (TryGetValue(varName, out object? value)) {
						text = text.Substring(0, i) + ConvertToString(value) + text.Substring(varNameEnd + VariableSuffix.Length);
						--i;
					}
				}
			}
		}
		return text;
	}
	public static string ConvertToString(object? obj) {
		return (obj is Func<string> f) ? f.Invoke() : obj?.ToString() ?? string.Empty;
	}

	public object? this[string key] { get => vars[key]; set => vars[key] = value; }
	public ICollection<string> Keys => vars.Keys;
	public ICollection<object?> Values => vars.Values;
	public int Count => vars.Count;
	public bool IsReadOnly => ((IDictionary<string, object?>)vars).IsReadOnly;
	public void Add(string key, object? value) => vars.Add(key, value);
	public void Add(KeyValuePair<string, object?> item) => ((IDictionary<string, object?>)vars).Add(item);
	public void Clear() {
		vars.Clear();
		if (ContextStacks != null) {
			foreach (var kvp in ContextStacks) {
				kvp.Value.Clear();
			}
		}
	}
	public bool Contains(KeyValuePair<string, object?> item) => ((IDictionary<string, object?>)vars).Contains(item);
	public bool ContainsKey(string key) => vars.ContainsKey(key);
	public void CopyTo(KeyValuePair<string, object?>[] array, int arrayIndex) => ((IDictionary<string, object?>)vars).CopyTo(array, arrayIndex);
	public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => vars.GetEnumerator();
	public bool Remove(string key) => vars.Remove(key);
	public bool Remove(KeyValuePair<string, object?> item) => ((IDictionary<string, object?>)vars).Remove(item);
	public bool TryGetValue(string key, [MaybeNullWhen(false)] out object? value) => vars.TryGetValue(key, out value);
	IEnumerator IEnumerable.GetEnumerator() => vars.GetEnumerator();
}
