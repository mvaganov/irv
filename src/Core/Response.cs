namespace src.Core;
public enum CommandState { None = -1, Success = 0, Fail = 1, Processing = 2, Error = 3 }
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
}
