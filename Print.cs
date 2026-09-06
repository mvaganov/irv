
using irv.src;
using src;
using static Program;
namespace irv;

using VotesPerCandidate = VoteState;//Dictionary<Candidate, List<Ballot>>;
public class Print {
	public static ConsoleBuffer buffer = new ConsoleBuffer(), back = new ConsoleBuffer();
	static Print() {
	}
	public static void DebugShow(VotesPerCandidate tally, HashSet<Candidate> exhaustedCandidates) {
		List<Candidate> inOrder = CompleteElectionResults.OrderByBallotCount(tally);
		ShowAllBallotsSortedByCandidate(tally, inOrder, exhaustedCandidates, null);
		int w = 100;
		Log.WriteLine($"exhausted: {string.Join(", ", exhaustedCandidates)}".PadRight(100));
		Pause(1);
		Clear();
	}
	public static void Pause(int stackOffset = 0) {
		Log.Write(Log.StackPosition(2 + stackOffset));
		Console.ReadKey();
	}
	public static void Clear() {
		buffer.Clear();
		back.Clear();
		buffer.RenderConsoleBuffer();
	}
	public static void EnsureMinimum(int width, int height) {
		buffer.EnsureMinimumSize(width, height);
		back.EnsureMinimumSize(width, height);
	}
	public static void Explore(IRV irv) {
		CompleteElectionResults election = irv.instantRunoffElectionsByRank![0][0];
		char input = ' ';
		int stateShowing = 0;
		HashSet<Candidate>? exhausted = null;
		HashSet<Candidate>? whoToDrawExpanded = null;
		int maxState = election.voteStates.Count - 1;
		while (input != 'q') {
			List<Ballot>? ballots = GetBallotList(election.voteStates[stateShowing], irv.candidates!);
			ShowAllBallotsSortedByCandidate(ballots, irv.candidates!, exhausted, whoToDrawExpanded);
			Log.d("qws");
			input = Console.ReadKey().KeyChar;
			switch (input) {
				case 'w': --stateShowing; if (stateShowing < 0) stateShowing = 0; break;
				case 's': ++stateShowing; if (stateShowing >= maxState) stateShowing = maxState; break;
			}
		}
	}

	public IList<VoteBloc>? ConvertBlocToMigrantBlocs(VoteBloc bloc, bool from) {
		if (bloc.migrations == null) return null;
		VoteBloc[] migrants = new VoteBloc[bloc.migrations.Count];
		for (int i = 0; i < bloc.migrations.Count; ++i) {
			VoteBloc.Migration migration = bloc.migrations[i];
			migrants[i] = new VoteBloc(migration.newBoss, from ? migration.fromPosition : migration.toPosition, migration.ballots);
		}
		return migrants;
	}
	public static void ShowAllBallotsSortedByCandidate(IList<Ballot> ballots, IList<Candidate> candidates, HashSet<Candidate>? exhausted = null, HashSet<Candidate>? whoToDrawExpanded = null) {
		VotesPerCandidate votesPerCandidate = new VotesPerCandidate();
		CompleteElectionResults.TallyVotes(votesPerCandidate, ballots, exhausted, null);
		ShowAllBallotsSortedByCandidate(votesPerCandidate, candidates, exhausted, whoToDrawExpanded);
	}
	public static void ShowAllBallotsSortedByCandidate(VotesPerCandidate votesPerCandidate, IList<Candidate> candidates, HashSet<Candidate>? exhausted = null, HashSet<Candidate>? whoToDrawExpanded = null) {
		ShowAllBallots(GetBallotList(votesPerCandidate, candidates), candidates, exhausted, whoToDrawExpanded);
	}

	public static List<Ballot> GetBallotList(VotesPerCandidate votesPerCandidate, IList<Candidate> candidatesInOrder) {
		List<Ballot> sortedList = new List<Ballot>();
		for (int i = 0; i < candidatesInOrder.Count; ++i) {
			if (votesPerCandidate.TryGetValue(candidatesInOrder[i], out List<Ballot>? candidateBallots)) {
				sortedList.AddRange(candidateBallots);
			}
		}
		return sortedList;
	}

	public static void ShowAllBallots(IList<Ballot> ballots, IList<Candidate> candidates, HashSet<Candidate>? exhausted = null, HashSet<Candidate>? whoToDrawExpanded = null) {
		int height = candidates.Count;
		int width = ballots.Count;
		EnsureMinimum(width, height);
		for (int col = 0; col < width; ++col) {
			int exhaustedChoices = 0;
			Candidate? ballotTop = null;
			for (int row = 0; row < height; ++row) {
				buffer.text[row][col] = ' ';
				buffer.color[row][col] = ConsoleColor.Gray;
				Ballot b = ballots[col];
				if (b.RankedVote == null || exhaustedChoices >= b.RankedVote.Length) continue;
				Candidate? c;
				bool thisOneShouldBeShown;
				do {
					if (exhaustedChoices >= b.RankedVote.Length) {
						c = null;
						break;
					}
					c = b.RankedVote[exhaustedChoices];
					if (ballotTop == null) { ballotTop = c; }
					++exhaustedChoices;
					thisOneShouldBeShown = whoToDrawExpanded == null || whoToDrawExpanded.Contains(c);
				} while (exhausted != null && exhausted.Contains(c) && thisOneShouldBeShown);
				bool expansionDrawingIsBeingLimited = row > 0 && whoToDrawExpanded != null && (ballotTop != null && !whoToDrawExpanded.Contains(ballotTop));
				if (c != null && !expansionDrawingIsBeingLimited) {
					buffer.text[row][col] = c.name[0];
					buffer.color[row][col] = c.color;
				}
			}
		}
		buffer.RenderConsoleBuffer(10, back);
	}


	public static void ShowFancyVisual(IList<VoteBloc> from, IList<Candidate> candidates, int width, HashSet<Candidate>? out_exhaustedThisTime,
		VotesPerCandidate? stateNow = null, VotesPerCandidate? stateNext = null) {
		int height = candidates.Count;
		EnsureMinimum(width, height);
		void Render(int delay = 0) {
			buffer.RenderConsoleBuffer(delay, back);
			ConsoleBuffer swap = buffer; buffer = back; back = swap;
		}
		// draw start
		DrawVoteBlocsToLine(from, buffer.text[0], buffer.color[0]);
		DrawVoteBlocsToLine(null, buffer.text[1], buffer.color[1]);

		//HashSet<Candidate>? expand = null;// new HashSet<Candidate>();
		//if (stateNow != null) {
		//	List<Ballot>? ballots = GetBallotList(stateNow, candidates);
		//	ShowAllBallotsSortedByCandidate(buffer, ballots, candidates, out_exhaustedThisTime, expand);
		//	Render(1);
		//	Log.d(""); Console.ReadKey();
		//}
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
				if (out_exhaustedThisTime != null) {
					out_exhaustedThisTime.Add(from[i].candidate);
				}
				for (int m = 0; m < migrations.Count; ++m) {
					VoteBloc.Migration migration = migrations[m];
					VoteBloc start = new VoteBloc(migration.newBoss, migration.fromPosition, migration.ballots);
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
		DrawVoteBlocsToLine(normalBlocs, buffer.text[0], buffer.color[0]);
		DrawVoteBlocsToLine(exhaustedBlocs, buffer.text[1], buffer.color[1]);
		Render(500);

		// TODO show full ballots of those blocks
		// ShowAllBallots(stateNow, candidates, exhausted, out_exhaustedThisTime);
		// show full ballots after exhausted canidate is removed

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
				DrawVoteBlocsToLine(normalBlocs, buffer.text[0], buffer.color[0]);
				Array.Copy(exhaustedAnimating, buffer.text[1], width);
				Array.Copy(exhastedAnimatingColor, buffer.color[1], width);
				Render(10);
			}
		} while (differenceFound >= 0);

		DrawVoteBlocsToLine(normalBlocs, buffer.text[0], buffer.color[0]);
		DrawVoteBlocsToLine(movingBlocs, buffer.text[1], buffer.color[1]);
		Render(500);
		DrawVoteBlocsToLine(normalBlocs, buffer.text[0], buffer.color[0]);
		DrawVoteBlocsToLine(movingBlocs, buffer.text[1], buffer.color[1], '#');
		Render(100);
		DrawVoteBlocsToLine(movingBlocs, buffer.text[1], buffer.color[1], '+');
		Render(100);
		DrawVoteBlocsToLine(movingBlocs, buffer.text[1], buffer.color[1], '-');
		Render(100);

		if (movingBlocs.Count == 0) { return; }

		// do animation
		const int maxSteps = 10;
		for (int i = 0; i < maxSteps; ++i) {
			float progress = (float)(i + 1) / maxSteps;
			lerps.ForEach(l => l.Lerp(progress));
			DrawVoteBlocsToLine(normalBlocs, buffer.text[0], buffer.color[0]);
			DrawVoteBlocsToLine(movingBlocs, buffer.text[1], buffer.color[1], '-');
			Render(10);
		}
	}

}

public class ConsoleBuffer {
	public char[][] text = new char[0][];
	public ConsoleColor[][] color = new ConsoleColor[0][];
	public char GetChar(int x, int y) => text[y][x];
	public ConsoleColor GetColor(int x, int y) => color[y][x];
	public void EnsureMinimumSize(int width, int height) {
		if (text == null || text.Length < height || text[0].Length < width) {
			Resize(width, height);
		}
	}
	public void Resize(int width, int height) {
		char[][] newText = new char[height][];
		ConsoleColor[][] newColor = new ConsoleColor[height][];
		for (int row = 0; row < height; ++row) {
			newText[row] = new char[width];
			newColor[row] = new ConsoleColor[width];
			for (int col = 0; col < width; ++col) {
				newText[row][col] = 'x';
				newColor[row][col] = ConsoleColor.Gray;
			}
		}
		if (text != null) {
			for (int r = 0; r < height && r < text.Length; ++r) {
				for (int c = 0; c < width && c < text[r].Length; ++c) {
					newText[r][c] = text[r][c];
					newColor[r][c] = color[r][c];
				}
			}
		}
		text = newText;
		color = newColor;
	}
	//public ConsoleBuffer(int width, int height) {
	//	text = new char[height][];
	//	color = new ConsoleColor[height][];
	//	for (int i = 0; i < height; ++i) {
	//		text[i] = new char[width];
	//		color[i] = new ConsoleColor[width];
	//	}
	//	Clear();
	//}
	public void RenderConsoleBuffer(int delay = 0, ConsoleBuffer? back = null) {
		ConsoleColor o = Console.ForegroundColor;
		if (back == null) {
			Console.SetCursorPosition(0, 0);
		}
		for (int row = 0; row < text.Length; ++row) {
			for (int col = 0; col < text[row].Length; ++col) {
				bool needsToBePrinted = back == null || back.color[row][col] != color[row][col] || back.text[row][col] != text[row][col];
				if (needsToBePrinted) {
					if (back != null) { Console.SetCursorPosition(col, row); }
					Console.ForegroundColor = color[row][col];
					Console.Write(text[row][col]);
				}
			}
			if (back == null) {
				Console.WriteLine();
			}
		}
		if (back != null) {
			Console.SetCursorPosition(0, text.Length);
		}
		Console.ForegroundColor = o;
		Thread.Sleep(delay/10);
	}
	public void Copy(ConsoleBuffer other) {
		if (text == null || text.Length != other.text.Length) text = new char[other.text.Length][];
		if (color == null || color.Length != other.color.Length) color = new ConsoleColor[other.color.Length][];
		for (int i = 0; i < text.Length; ++i) {
			text[i] = new char[text[i].Length];
			color[i] = new ConsoleColor[text[i].Length];
			for (int j = 0; j < text[i].Length; ++j) {
				text[i][j] = other.text[i][j];
				color[i][j] = other.color[i][j];
			}
		}
	}
	public void Clear() {
		for (int i = 0; i < text.Length; ++i) {
			for (int j = 0; j < text[i].Length; ++j) {
				text[i][j] = ' ';
				color[i][j] = ConsoleColor.Gray;
			}
		}
	}
}
