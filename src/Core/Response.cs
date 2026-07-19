
namespace src.Core;

public enum CommandState {
	None = -1,
	Success = 0,
	Fail = 1,
	Processing = 2,
	Error = 3,
}
public struct Response {
	public CommandState CommandState;
	public object? Message;
	public string MessageString => Message?.ToString() ?? string.Empty;
	public bool IsSuccess => CommandState == CommandState.Success;
	public bool IsError => CommandState == CommandState.Error;
	public bool IsNonTrivialError => IsError && Message != null;
	public bool IsProcessing => CommandState == CommandState.Processing;

	public Response(CommandState commandState, object? message) { CommandState = commandState; Message = message; }
	public static Response SUCCESS = new Response(CommandState.Success, null);
	public static Response FAIL = new Response(CommandState.Fail, null);
	public static Response PROCESSING = new Response(CommandState.Processing, null);
	public static Response Success(object? message) => new Response(CommandState.Success, message);
	/// <summary>Use for reasonably expected fail states that can happen during proper execution</summary>
	public static Response Fail(object? message) => new Response(CommandState.Fail, message);
	public static Response Processing(object? message) => new Response(CommandState.Processing, message);
	/// <summary>Use for erroneous states, ideally this code branch is never called</summary>
	public static Response Error(object? message) => new Response(CommandState.Error, message);
	public override bool Equals(object? obj) => obj != null && obj is Response r ? Equals(r) : false;
	public override int GetHashCode() => (int)CommandState | (Message?.GetHashCode() ?? 0);
	public bool Equals(Response other) => CommandState == other.CommandState && Message == other.Message;
	public static bool operator ==(Response a, Response b) => a.Equals(b);
	public static bool operator !=(Response a, Response b) => !a.Equals(b);
	public static Response Consolidate(IEnumerable<Response>? responses) {
		if (responses == null) { return SUCCESS; }
		List<Response>? list = null;
		bool hasNonSuccess = false;
		foreach (var r in responses) {
			if (r == SUCCESS) { continue; }
			if (r.CommandState != CommandState.Success) { hasNonSuccess = true; }
			if (r == FAIL) { continue; }
			if (list == null) { list = new List<Response>(); }
			list.Add(r);
		}
		if (list == null || list.Count == 0) { return hasNonSuccess ? FAIL : SUCCESS; }
		return hasNonSuccess ? Error(list) : Success(list);
	}
	public static Response TryCast<T>(object? obj, out T? result) where T : class {
		result = obj as T;
		if (result == null) {
			string typeName = obj != null ? obj.GetType().Name : "null";
			string message = $"Expected `{typeof(T).Name}`, not `{typeName}`";
			return Error(message);
		}
		return SUCCESS;
	}
}
