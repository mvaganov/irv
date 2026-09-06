using irv;
using irv.src;
using src;
using src.Core;
using System.Text;
using VotesPerCandidate = irv.src.VoteState;//Dictionary<Candidate, List<Ballot>>;
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
		candidates.Add(new Candidate("Nunov", Color.darkYellow));
		candidates.Add(new Candidate("Glokglok", Color.darkYellow));
		candidates.Add(new Candidate("Naltron", Color.blue));
		candidates.Add(new Candidate("Dunhab", Color.darkGreen));
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
			v.RankedVote = ranked;
			votes.Add(v);
		}
		Color.AssignUniqueColors(candidates, c => c.color, (cand, colr) => cand.color = colr);
		Print.ShowAllBallots(votes, candidates);
		Print.Pause();
		Print.ShowAllBallotsSortedByCandidate(votes, candidates);
		Print.Pause();
		Print.Clear();

		//for(int i = 0; i < votes.Count; ++i) { Log.WriteLine(votes[i]); }
		IRV irv = new IRV();
		IEnumerator<Response> iter = irv.Calc(votes);
		uint last = Rand.Timestamp;
		while (iter.MoveNext()) {
			uint now = Rand.Timestamp;
			int passed = (int)(now - last);
			Response response = iter.Current;
			object? messsge = response.Message;
			string typeLabel = messsge?.GetType().Name ?? "null";
			List<CompleteElectionResults>? allData = messsge as List<CompleteElectionResults>;
			if (allData != null) {
				typeLabel += $"[{allData.Count}]";
				for (int e = 0; e < allData.Count; ++e) {
					CompleteElectionResults election = allData[e];
					if (election.visualization == null) continue;
					Log.WriteLine(election.label);
					List<List<VoteBloc>> allStates = election.visualization.data;
					HashSet<Candidate> exhaustedForVisual = new HashSet<Candidate>();
					for (int i = 0; i < allStates.Count; ++i) {
						List<VoteBloc> state = allStates[i];
						//string currentStateDiagram = StateToString(state, out int width);
						//Log.WriteLine(currentStateDiagram);
						//// draw moves
						//char[] bufferFrom = new char[width];
						//char[] bufferTo = new char[width];
						//for (int b = 0; b < width; ++b) bufferFrom[b] = bufferTo[b] = ' ';
						//for (int b = 0; b < state.Count; ++b) {
						//	VoteBloc bloc = state[b];
						//	if (bloc.migrations == null) continue;
						//	int letterIndex = 0;
						//	for (int m = 0; m < bloc.migrations.Count; ++m) {
						//		VoteBloc.Migration migration = bloc.migrations[m];
						//		if (migration.newBoss == bloc.candidate) continue; // hide uninteresting direct migrations
						//		for (int j = 0; j < migration.count; ++j) {
						//			bufferFrom[migration.fromPosition + j] = GetLetter(bloc.candidate.name, letterIndex, '.');
						//			bufferTo[migration.toPosition + j] = GetLetter(bloc.candidate.name, letterIndex, '.');
						//			letterIndex++;
						//		}
						//	}
						//}
						//Console.WriteLine(new string(bufferFrom));
						//Console.WriteLine(new string(bufferTo));
						//// TODO create animation where the exhausted candidate from `currentStateDiagram`
						//// drops to the line below, as `bufferFrom`. each normally transfering bloc animates
						//// to its new position, while each transfering vote animates to it's `bufferTo`
						//// position, then when all transfers reach, next state is drawn.
						//// timer queue facilitates animation
						//if (allStates.Count > i+1) {
						//	Log.WriteLine(StateToString(allStates[i + 1], out width));
						//}
						HashSet<Candidate> exhaustedThisTime = new HashSet<Candidate>();
						VotesPerCandidate? next_vState = i < election.voteStates.Count-1 ? election.voteStates[i + 1] : null;
						Print.ShowFancyVisual(state, candidates, 100, exhaustedThisTime, election.voteStates[i], next_vState);
						// TODO show full ballot visualization as part of fancy visual.
						// after candidate is dropped, show all ballots, tick the candidate off, then go back to compressed line form

						//Dictionary<Candidate, List<Ballot>> votesPerCandidate = election.voteState[i];
						//ShowAllBallotsSortedByCandidate(votesPerCandidate, candidates, exhaustedForVisual, exhaustedThisTime);
						//Log.d(""); Console.ReadKey();
						foreach (Candidate c in exhaustedThisTime) exhaustedForVisual.Add(c);
					}
					//Log.d("------------------ winner: " + string.Join(", ", election.winner));

				}
			}
			switch (response.CommandState) {
				case CommandState.Error: Log.e(response.MessageString); break;
				case CommandState.Fail:  Log.f(response.MessageString); break;
				case CommandState.Processing: break;
				case CommandState.Success:    break;
				default:
					Log.WriteLine($"{passed} {iter.Current.CommandState} {typeLabel}");
					break;
			}
		}
	}
	public static char GetLetter(string str, int index, char fallback) =>
		(str != null && index >= 0 && index < str.Length) ? str[index] : fallback;
	public static string StateToString(List<VoteBloc> state, out int width) {
		width = 0;
		StringBuilder sb = new StringBuilder();
		for (int b = 0; b < state.Count; ++b) {
			VoteBloc bloc = state[b];
			Candidate candidate = bloc.candidate;
			sb.Append(Log.ColorCode(candidate.color));
			for (int w = 0; w < bloc.ballotCount; ++w) {
				sb.Append(GetLetter(candidate.name, w, '.'));
				++width;
			}
		}
		return sb.ToString();
	}

	public static void DrawVoteBlocsToLine(IList<VoteBloc>? blocs, char[] text, ConsoleColor[] color, char showLetters = '\0') {
		for (int col = 0; col < text.Length; ++col) {
			text[col] = ' ';
			color[col] = ConsoleColor.Gray;
		}
		if (blocs == null) return;
		for (int b = 0; b < blocs.Count; ++b) {
			VoteBloc bloc = blocs[b];
			Candidate candidate = bloc.candidate;
			ConsoleColor candidateColor = bloc.candidate.color;
			for (int w = 0; w < bloc.ballotCount; ++w) {
				char letter = showLetters == '\0' ? GetLetter(candidate.name, w, '.') : showLetters;
				int x = bloc.position + w;
				if (x < text.Length) {
					text[x] = letter;
					color[x] = candidateColor;
				}
			}
		}
	}

	public struct Lerping {
		public float start, end;
		public Action<float>? doLerp;
		public Lerping(float start, float end, Action<float>? doLerp) { this.start = start; this.end = end; this.doLerp = doLerp; }
		public float Delta => end - start;
		public float Lerp(float progress) {
			float value = start + progress * Delta;
			if (doLerp != null) { doLerp(value); }
			return value;
		}
	}
}

