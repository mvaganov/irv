using irv.src;
using src;
using src.Core;
using System.Text;
using static irv.src.IRV;

public class Program {
	public static void Main(string[] args) {
		List<Ballot> votes = new List<Ballot>();
		int randomlyGenerateTest = 100;
		List<Candidate> candidates = new List<Candidate>();
		candidates.Add(new Candidate("Mr. V", Color.cyan));
		candidates.Add(new Candidate("Professor V"));
		candidates.Add(new Candidate("Vaganov"));
		candidates.Add(new Candidate("V", Color.red));
		candidates.Add(new Candidate("Sensei"));
		candidates.Add(new Candidate("Cheif"));
		candidates.Add(new Candidate("Chort"));
		candidates.Add(new Candidate("Nunov"));
		candidates.Add(new Candidate("Glokglok"));
		candidates.Add(new Candidate("Naltron"));
		candidates.Add(new Candidate("Dunhab"));
		for (int i = 0; i < randomlyGenerateTest; ++i) {
			int picks = (int)(Rand.Number * Rand.Number * (candidates.Count - 1) + 2);
			picks = (int)Math.Min(picks, candidates.Count);
			Candidate[] ranked = new Candidate[picks];
			for (int r = 0; r < ranked.Length; ++r) {
				int pick;
				do {
					if (r < ranked.Length / 2) {
						pick = (int)(Rand.Number * (candidates.Count));
					} else {
						pick = (int)(Rand.Number * Rand.Number * (candidates.Count));
					}
				} while (System.Array.IndexOf(ranked, candidates[pick]) >= 0);
				ranked[r] = candidates[pick];
			}
			Ballot v = new Ballot();
			v.id = $"rand{i}";
			v.vote = ranked;
			votes.Add(v);
		}
		//for(int i = 0; i < votes.Count; ++i) { Log.WriteLine(votes[i]); }
		IEnumerator<Response> iter = IRV.Calc(votes);
		uint last = Rand.Timestamp;
		ConsoleColor[] colors = new ConsoleColor[] { ConsoleColor.Red, ConsoleColor.Green, ConsoleColor.Blue, ConsoleColor.Yellow, ConsoleColor.Magenta, ConsoleColor.Cyan };
		while (iter.MoveNext()) {
			uint now = Rand.Timestamp;
			int passed = (int)(now - last);
			Response response = iter.Current;
			object? messsge = response.Message;
			string typeLabel = messsge?.GetType().Name ?? "null";
			List<RankedChoiceElectionResultsStepByStep>? allData = messsge as List<RankedChoiceElectionResultsStepByStep>;
			if (allData != null) {
				typeLabel += $"[{allData.Count}]";
				for (int e = 0; e < allData.Count; ++e) {
					RankedChoiceElectionResultsStepByStep election = allData[e];
					if (election.serialized == null) continue;
					Log.WriteLine(election.serialized.title);
					List<List<VoteBloc>> allStates = election.serialized.data;
					for (int i = 0; i < allStates.Count; ++i) {
						List<VoteBloc> state = allStates[i];
						Log.WriteLine(StateToString(state, colors, out int index));
						// draw moves
						char[] bufferFrom = new char[index];
						char[] bufferTo = new char[index];
						for (int b = 0; b < index; ++b) bufferFrom[b] = bufferTo[b] = ' ';
						for (int b = 0; b < state.Count; ++b) {
							VoteBloc bloc = state[b];
							if (bloc.migrations == null) continue;
							for (int m = 0; m < bloc.migrations.Count; ++m) {
								VoteBloc.Migration migration = bloc.migrations[m];
								if (migration.newBoss == bloc.candidate) continue; // hide uninteresting direct migrations
								for (int j = 0; j < migration.voteCount; ++j) {
									bufferFrom[migration.fromPosition + j] = migration.newBoss.name[0];
									bufferTo[migration.toPosition + j] = migration.newBoss.name[0];
								}
							}
						}
						Console.WriteLine(new string(bufferFrom));
						Console.WriteLine(new string(bufferTo));
					}
					Console.WriteLine("------------------ winner: " + string.Join(", ", election.winner));
				}
			}
			switch (response.CommandState) {
				case CommandState.Error:      Log.e(response.MessageString); break;
				case CommandState.Processing: Log.d(response.MessageString); break;
				case CommandState.Fail:       Log.f(response.MessageString); break;
				case CommandState.Success:    Log.i(response.MessageString); break;
				default:
					Log.WriteLine($"{passed} {iter.Current.CommandState} {typeLabel}");
					break;
			}
		}
	}

	public static string StateToString(List<VoteBloc> state, ConsoleColor[] colors, out int index) {
		index = 0;
		StringBuilder sb = new StringBuilder();
		for (int b = 0; b < state.Count; ++b) {
			sb.Append(Log.ColorCode(colors[b % colors.Length]));
			for (int w = 0; w < state[b].voteCount; ++w) {
				if (w < state[b].candidate.name.Length) {
					sb.Append(state[b].candidate.name[w]);
				} else {
					sb.Append('.');
				}
				++index;
			}
		}
		return sb.ToString();
	}
}

