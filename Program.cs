using irv.src;
using src;
using src.Core;
using System.Text;

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
		ShowAllBallots(votes, candidates);
		Console.ReadKey();
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
						ShowFancyVisual(state, 100);
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
				text[x] = letter;
				color[x] = candidateColor;
			}
		}
	}
	public IList<VoteBloc>? ConvertBlocToMigrantBlocs(VoteBloc bloc, bool from) {
		if (bloc.migrations == null) return null;
		VoteBloc[] migrants = new VoteBloc[bloc.migrations.Count];
		for(int i = 0; i < bloc.migrations.Count; ++i) {
			VoteBloc.Migration migration = bloc.migrations[i];
			migrants[i] = new VoteBloc(migration.newBoss, from ? migration.fromPosition : migration.toPosition, migration.count);
		}
		return migrants;
	}
	public static void ShowAllBallots(IList<Ballot> ballots, IList<Candidate> candidates) {
		int height = candidates.Count;
		int width = ballots.Count;
		char[][] text = new char[height][];
		ConsoleColor[][] color = new ConsoleColor[height][];
		for (int row = 0; row < height; ++row) {
			text[row] = new char[width];
			color[row] = new ConsoleColor[width];
			for (int col = 0; col < width; ++col) {
				text[row][col] = ' ';
				color[row][col] = ConsoleColor.Gray;
				Ballot b = ballots[col];
				if (b.RankedVote == null || row >= b.RankedVote.Length) continue;
				Candidate c = b.RankedVote[row];
				text[row][col] = c.name[0];
				color[row][col] = c.color;
			}
		}
		RenderConsoleBuffer(text, color, 10);
	}
	public static void ShowFancyVisual(IList<VoteBloc> from, int width) {
		int height = 2;
		char[][] text = new char[height][];
		ConsoleColor[][] color = new ConsoleColor[height][];
		void Render(int delay) => RenderConsoleBuffer(text, color, delay);
		for (int i = 0; i < height; ++i) {
			text[i] = new char[width];
			color[i] = new ConsoleColor[width];
		}
		// draw start
		DrawVoteBlocsToLine(from, text[0], color[0]);
		DrawVoteBlocsToLine(null, text[1], color[1]);
		Render(500);

		List<VoteBloc> normalBlocs = new List<VoteBloc>();
		List<VoteBloc> normalBlocsEnd = new List<VoteBloc>();
		List<VoteBloc> movingBlocs = new List<VoteBloc>();
		List<VoteBloc> movingBlocsEnd = new List<VoteBloc>();
		List<VoteBloc> exhaustedBlocs = new List<VoteBloc>();
		// calculate start and end positions of normal blocs and moving blocs
		for (int i = 0; i < from.Count; ++i) {
			List<VoteBloc.Migration>? migrations = from[i].migrations;
			if (migrations == null) continue;
			if (migrations.Count == 1 && migrations[0].newBoss == from[i].candidate) {
				VoteBloc start = new VoteBloc(from[i]);
				VoteBloc end = new VoteBloc(from[i]);
				end.position = migrations[0].toPosition;
				normalBlocs.Add(start);
				normalBlocsEnd.Add(end);
			} else {
				exhaustedBlocs.Add(new VoteBloc(from[i]));
				for (int m = 0; m < migrations.Count; ++m) {
					VoteBloc.Migration migration = migrations[m];
					VoteBloc start = new VoteBloc(migration.newBoss, migration.fromPosition, migration.count);
					VoteBloc end = new VoteBloc(start);
					end.position = migration.toPosition;
					movingBlocs.Add(start);
					movingBlocsEnd.Add(end);
				}
			}
		}

		// calculate bloc animations
		List<Lerping> lerps = new List<Lerping>();
		for (int i = 0; i < normalBlocs.Count; ++i) {
			VoteBloc bloc = normalBlocs[i];
			lerps.Add(new Lerping(bloc.position, normalBlocsEnd[i].position, p => bloc.position = (int)p));
		}
		for (int i = 0; i < movingBlocs.Count; ++i) {
			VoteBloc bloc = movingBlocs[i];
			lerps.Add(new Lerping(bloc.position, movingBlocsEnd[i].position, p => bloc.position = (int)p));
		}

		// drop exhausted candidate
		DrawVoteBlocsToLine(normalBlocs, text[0], color[0]);
		DrawVoteBlocsToLine(exhaustedBlocs, text[1], color[1]);
		Render(500);

		char[] exhaustedAnimating = new char[width];
		char[] exhaustedConverted = new char[width];
		ConsoleColor[] exhastedAnimatingColor = new ConsoleColor[width];
		ConsoleColor[] exhastedConvertedColor = new ConsoleColor[width];
		DrawVoteBlocsToLine(exhaustedBlocs, exhaustedAnimating, exhastedAnimatingColor);
		DrawVoteBlocsToLine(movingBlocs, exhaustedConverted, exhastedConvertedColor);

		// convert to next blocs
		int differenceFound = -1;
		do {
			differenceFound = -1;
			for (int i = 0; i < width; ++i) {
				if (exhaustedAnimating[i] != exhaustedConverted[i] || exhastedAnimatingColor[i] != exhastedConvertedColor[i]) {
					differenceFound = i;
					break;
				}
			}
			if (differenceFound >= 0) {
				exhaustedAnimating[differenceFound] = exhaustedConverted[differenceFound];
				exhastedAnimatingColor[differenceFound] = exhastedConvertedColor[differenceFound];
				Array.Copy(exhaustedAnimating, text[1], width);
				Array.Copy(exhastedAnimatingColor, color[1], width);
				Render(10);
			}
		} while (differenceFound >= 0);

		DrawVoteBlocsToLine(normalBlocs, text[0], color[0]);
		DrawVoteBlocsToLine(movingBlocs, text[1], color[1]);
		Render(500);
		DrawVoteBlocsToLine(normalBlocs, text[0], color[0]);
		DrawVoteBlocsToLine(movingBlocs, text[1], color[1], '#');
		Render(100);
		DrawVoteBlocsToLine(movingBlocs, text[1], color[1], '+');
		Render(100);
		DrawVoteBlocsToLine(movingBlocs, text[1], color[1], '-');
		Render(100);

		if (movingBlocs.Count == 0) { return; }

		// do animation
		const int maxSteps = 10;
		for (int i = 0; i < maxSteps; ++i) {
			float progress = (float)(i + 1) / maxSteps;
			lerps.ForEach(l => l.Lerp(progress));
			DrawVoteBlocsToLine(normalBlocs, text[0], color[0]);
			DrawVoteBlocsToLine(movingBlocs, text[1], color[1], '-');
			Render(10);
		}
	}
	static void RenderConsoleBuffer(char[][] text, ConsoleColor[][] color, int delay = 10) {
		Console.SetCursorPosition(0, 0);
		for (int row = 0; row < text.Length; ++row) {
			for (int col = 0; col < text[row].Length; ++col) {
				Console.ForegroundColor = color[row][col];
				Console.Write(text[row][col]);
			}
			Console.WriteLine();
		}
		Thread.Sleep(delay);
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

