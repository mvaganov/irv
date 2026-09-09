using src;
using src.Core;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
namespace irv.src;
using BallotsTransferingToCandidate = Dictionary<Candidate, List<Ballot>>;

// TODO cull weakest candidates by ability to win majority against highest bordacount, not highest popularity.
public class Candidate : IComparable<Candidate> {
	public string name;
	/// <summary>value of <see cref="Color.clear"/> will be replaced by <see cref="FillInUnassignedColors"/></summary>
	public Color color = Color.clear;
	private float harmonicBordaCount = 0;
	private float totalVotesWeighted = 0;
	public int totalBallots = 0;
	public float HarmonicBordaCount => harmonicBordaCount;
	public float TotalVotesWeighted => totalVotesWeighted;
	public void SetExhaustedPlaceholder() { harmonicBordaCount = totalVotesWeighted = float.NegativeInfinity; }
	public bool IsExhaustedPlaceholder => harmonicBordaCount == float.NegativeInfinity && totalVotesWeighted == float.NegativeInfinity;
	override public string ToString() { return name; }
	public Candidate(string name) { this.name = name; }
	public Candidate(string name, Color color) { this.name = name; this.color = color; }
	public Candidate(Candidate copy) { name = copy.name; color = copy.color; harmonicBordaCount = copy.harmonicBordaCount; totalVotesWeighted = copy.totalVotesWeighted; }
	public static void FillInUnassignedColors(IList<Candidate> candidates) {
		Color.AssignUniqueColors(candidates, c => c.color, (candidate, color) => candidate.color = color);
	}

	/// <summary>calculates vote heuristics for each candidate</summary>
	/// <returns>list of candidates</returns>
	public static List<Candidate> InstantVoteCalc(IList<Ballot> ballots, out Candidate[][] harmonicBorda, out Candidate[][] popularity) {
		HashSet<Candidate> completeSet = new HashSet<Candidate>();
		for (int v = 0; v < ballots.Count; ++v) {
			Ballot ballot = ballots[v];
			if (ballot.RankedVote == null) continue;
			for (int i = 0; i < ballot.RankedVote.Length; ++i) {
				Candidate candidate = ballot.RankedVote[i];
				if (completeSet.Add(candidate)) {
					candidate.totalBallots = 0;
					candidate.totalVotesWeighted = 0;
					candidate.harmonicBordaCount = 0;
				}
				candidate.totalBallots += 1;
				candidate.totalVotesWeighted += ballot.BallotWeight;
				candidate.harmonicBordaCount += (1 / (i + 1.0f)) * ballot.BallotWeight;
			}
		}
		List<Candidate> candidateList = completeSet.ToList();
		candidateList.Sort((a, b) => Math.Sign(b.totalVotesWeighted - a.totalVotesWeighted));
		popularity = GenerateOrderIncludingTies(candidateList, c => c.harmonicBordaCount);
		candidateList.Sort((a, b) => Math.Sign(b.harmonicBordaCount - a.harmonicBordaCount));
		harmonicBorda = GenerateOrderIncludingTies(candidateList, c => c.harmonicBordaCount);
		return candidateList;
	}

	private static Candidate[][] GenerateOrderIncludingTies(IList<Candidate> candidates, Func<Candidate, float> getValue) {
		int ties = 0;
		for (int i = 0; i < candidates.Count - 1; ++i) {
			if (getValue(candidates[i]) == getValue(candidates[i + 1])) ++ties;
		}
		Candidate[][] order = new Candidate[candidates.Count - ties][];
		int index = 0;
		for (int i = 0; i < candidates.Count; ++i) {
			ties = 0;
			for (int j = i; j < candidates.Count - 1; j++) {
				if (getValue(candidates[j]) != getValue(candidates[j + 1])) break;
				++ties;
			}
			order[index] = new Candidate[ties + 1];
			order[index][0] = candidates[i];
			for (int j = 0; j < ties; j++) {
				order[index][j + 1] = candidates[i + 1 + j];
			}
			++index;
			i += ties;
		}
		return order;
	}

	public int CompareTo(Candidate? other) {
		return other == null ? 1
			: TotalVotesWeighted != other.TotalVotesWeighted ? TotalVotesWeighted.CompareTo(other.TotalVotesWeighted)
			: HarmonicBordaCount.CompareTo(other.HarmonicBordaCount);
	}
}
public class Ballot {
	public string? id;
	public Candidate[]? RankedVote;
	public float BallotWeight = 1; // useful for combining identical ballots
	public override string? ToString() => (RankedVote != null ? "[" + string.Join(", ", Array.ConvertAll(RankedVote, v => v.name)) + "]" : "")
		+ (BallotWeight != 1 ? $"({BallotWeight})" : "");
	public int GetBestChoiceIndex(HashSet<Candidate> exhastedCandidates) {
		if (RankedVote == null) { return -1; }
		for (int i = 0; i < RankedVote.Length; ++i) {
			if (!exhastedCandidates.Contains(RankedVote[i])) {
				return i;
			}
		}
		return -1;
	}
	public Candidate? GetBestChoice(HashSet<Candidate>? exhastedCandidates) {
		if (RankedVote == null) return null;
		int index = exhastedCandidates != null ? GetBestChoiceIndex(exhastedCandidates) : 0;
		return index >= 0 ? RankedVote[index] : null;
	}
}

public class VoteState : IDictionary<Candidate, List<Ballot>> {
	public Dictionary<Candidate, List<Ballot>> votesPerCandidate = new Dictionary<Candidate, List<Ballot>>();
	public HashSet<Candidate>? exhausted = new HashSet<Candidate>();
	public string note = string.Empty; // TODO -- "initial state -({prev winners})", "uncompetitive candidate cull: -({candidate})", "weakest candidate cull: -({candidate})"
	public ICollection<Candidate> Keys => votesPerCandidate.Keys;
	public ICollection<List<Ballot>> Values => votesPerCandidate.Values;
	// TODO rename TotalCandidateCount
	public int Count => votesPerCandidate.Count;
	public bool IsReadOnly => ((IDictionary<Candidate, List<Ballot>>)votesPerCandidate).IsReadOnly;
	public bool TryGetValue(Candidate candidate, [NotNullWhen(true)] out List<Ballot>? votes) => votesPerCandidate.TryGetValue(candidate, out votes);
	public bool ContainsKey(Candidate candidate) => votesPerCandidate.ContainsKey(candidate);
	public void Add(Candidate key, List<Ballot> value) => ((IDictionary<Candidate, List<Ballot>>)votesPerCandidate).Add(key, value);
	public bool Remove(Candidate key) => votesPerCandidate.Remove(key);
	public void Add(KeyValuePair<Candidate, List<Ballot>> item) => votesPerCandidate.Add(item.Key, item.Value);
	public void Clear() => votesPerCandidate.Clear();
	public bool Contains(KeyValuePair<Candidate, List<Ballot>> item) => votesPerCandidate.Contains(item);
	public void CopyTo(KeyValuePair<Candidate, List<Ballot>>[] array, int arrayIndex) => ((IDictionary<Candidate, List<Ballot>>)votesPerCandidate).CopyTo(array, arrayIndex);
	public bool Remove(KeyValuePair<Candidate, List<Ballot>> item) => votesPerCandidate.Remove(item.Key);
	public IEnumerator<KeyValuePair<Candidate, List<Ballot>>> GetEnumerator() => votesPerCandidate.GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	public List<Ballot> this[Candidate candidate] {
		get => votesPerCandidate[candidate];
		set => votesPerCandidate[candidate] = value;
	}
	public VoteState(IList<Ballot> ballots, HashSet<Candidate>? exhastedCandidates) {
		exhausted = exhastedCandidates;
		TallyVotes(ballots);
	}
	private void TallyVotes(IList<Ballot> ballots) {
		foreach (var kvp in votesPerCandidate) {
			kvp.Value?.Clear();
		}
		for (int i = 0; i < ballots.Count; ++i) {
			Ballot b = ballots[i];
			Candidate? bestChoice = b.GetBestChoice(exhausted);
			if (bestChoice == null) continue;
			List<Ballot>? supportForChoice = GetVotes(bestChoice);
			supportForChoice.Add(b);
		}
	}
	public VoteState(VoteState other) {
		foreach (var k in other.votesPerCandidate) {
			List<Ballot> list = votesPerCandidate[k.Key] = new List<Ballot>();
			list.AddRange(k.Value);
		}
		note = other.note;
		exhausted = other.exhausted != null ? new HashSet<Candidate>(other.exhausted) : null;
	}
	public List<Ballot> GetVotes(Candidate candidate) {
		if (!votesPerCandidate.TryGetValue(candidate, out List<Ballot>? votes) || votes == null) {
			votesPerCandidate[candidate] = votes = new List<Ballot>();
		}
		return votes;
	}
	//public int CountValidCandidates() {
	//	int count = 0;
	//	foreach (var k in votesPerCandidate) {
	//		if (exhausted.Contains(k.Key)) continue;
	//		++count;
	//	}
	//	return count;
	//}
	//public bool HasCandidate(Candidate candidate) => votesPerCandidate.ContainsKey(candidate);
	//public VoteState(Dictionary<Candidate, List<Ballot>> votesPerCandidate, HashSet<Candidate> exhausted) {
	//	foreach (var kvp in votesPerCandidate) { this.votesPerCandidate.Add(kvp.Key, kvp.Value); }
	//	foreach (var k in exhausted) { this.exhausted.Add(k); }
	//}

	//public VoteState() { }
	//public float SumUnexhaustedVotes() {
	//	float sumVotes = 0;
	//	foreach (var k in votesPerCandidate) {
	//		if (exhausted.Contains(k.Key)) continue;
	//		float voteCount = IRV.SumVoteValue(k.Value);
	//		sumVotes += voteCount;
	//	}
	//	return sumVotes;
	//}
	//public List<Ballot> ExhaustCandidate(Candidate candidate, Candidate? candidateForExhausted, Dictionary<Candidate, List<Ballot>> whereVotesMovedTo) {
	//	exhausted.Add(candidate);
	//	List<Ballot> exhaustedBallots = new List<Ballot>();
	//	if (!votesPerCandidate.TryGetValue(candidate, out List<Ballot>? votes)) {
	//		votes = new List<Ballot>();
	//	} else {
	//		votesPerCandidate.Remove(candidate);
	//	}
	//	for (int i = 0; i < votes.Count; ++i) {
	//		Ballot ballot = votes[i];
	//		Candidate? next = ballot.GetBestChoice(exhausted);
	//		if (next == null) {
	//			exhaustedBallots.Add(ballot);
	//			if (candidateForExhausted != null) {
	//				next = candidateForExhausted;
	//				if (!votesPerCandidate.TryGetValue(candidateForExhausted, out List<Ballot>? exhuastedBallots)) {
	//					votesPerCandidate[candidateForExhausted] = exhuastedBallots = new List<Ballot>();
	//				}
	//				exhuastedBallots.Add(ballot);
	//			}
	//		} else {
	//			if (!votesPerCandidate.TryGetValue(next, out List<Ballot>? ballots)) {
	//				votesPerCandidate[next] = ballots = new List<Ballot>();
	//			}
	//			ballots.Add(ballot);
	//		}
	//		if (whereVotesMovedTo != null && next != null) {
	//			if (!whereVotesMovedTo.TryGetValue(next, out List<Ballot>? movedTo)) {
	//				whereVotesMovedTo[next] = movedTo = new List<Ballot>();
	//			}
	//			movedTo.Add(ballot);
	//		}
	//	}
	//	return exhaustedBallots;
	//}
}
public class VoteVisualization {
	/// <summary>data to describe graphical representation [IRV rank][candidate]</summary>
	public List<List<VoteBloc>> data;
	public VoteVisualization(List<List<VoteBloc>> data) {
		this.data = data;
	}
	public static VoteVisualization Create(
		List<VoteState> voteStateHistory,
		List<Dictionary<Candidate, BallotsTransferingToCandidate>> voteMigrationHistory) {
		List<List<VoteBloc>> visBlocs = new List<List<VoteBloc>>();
		CalculateVisBlocsBasedOnHistory(visBlocs, voteStateHistory, voteMigrationHistory);
		VoteVisualization sr = new VoteVisualization(visBlocs);
		return sr;
	}
	/// <summary>calculate visualization model.</summary>
	/// <param name="out_visBlocs">where to append the visualization model.
	/// Each visualiation block explains which block moved from where to where.
	/// Every block exists at some index in a number line, and is the size of it's number of votes</param>
	/// <param name="voteStateHistory">the state of the votes at each step.</param>
	/// <param name="voteMigrationHistory">how the votes moved each state.</param>
	public static void CalculateVisBlocsBasedOnHistory(List<List<VoteBloc>> out_visBlocs, List<VoteState> voteStateHistory,
		List<Dictionary<Candidate, BallotsTransferingToCandidate>> voteMigrationHistory) {
		List<VoteBloc> blocsThisState;
		List<VoteBloc>? blocsLastState = null;
		for (int stateIndex = 0; stateIndex < voteStateHistory.Count; ++stateIndex) {
			List<Candidate> sorted = OrderCandidates(voteStateHistory[stateIndex]);
			blocsThisState = CalculateBlocs(sorted, voteStateHistory[stateIndex]);
			bool migrationsFromLastStateIsAvailable = blocsLastState != null;
			if (migrationsFromLastStateIsAvailable) {
				VoteBloc.CalculateMigrations(blocsThisState, blocsLastState!, voteMigrationHistory[stateIndex - 1]);
			}
			out_visBlocs.Add(blocsThisState);
			blocsLastState = blocsThisState;
		}
	}

	private static List<Candidate> OrderCandidates(VoteState tally) {
		List<Candidate> order = new List<Candidate>(tally.Keys);
		order.Sort((a, b) => b.HarmonicBordaCount.CompareTo(a.HarmonicBordaCount));
		return order;
	}

	public static List<VoteBloc> CalculateBlocs(List<Candidate> sorted, VoteState voteState) {
		List<VoteBloc> blocsThisState = new List<VoteBloc>();
		int cursor = 0;
		for (int s = 0; s < sorted.Count; ++s) {
			voteState.TryGetValue(sorted[s], out List<Ballot>? thisGuyVotes);
			IReadOnlyList<Ballot> ballots = thisGuyVotes != null ? thisGuyVotes : Array.Empty<Ballot>();
			VoteBloc bloc = new VoteBloc(sorted[s], cursor, ballots);
			blocsThisState.Add(bloc);
			cursor += ballots.Count;
		}
		return blocsThisState;
	}
}
public class VoteBloc {
	public Candidate candidate;
	public readonly IReadOnlyList<Ballot> ballots;
	public int position;
	/// the next blocs that these votes go into
	public List<Migration>? migrations;
	public int ballotCount => ballots.Count;
	public class Migration {
		public Candidate newBoss;
		public int fromPosition, toPosition;
		public IReadOnlyList<Ballot> ballots;
		public int count => ballots.Count;
		public Migration(Candidate destination, IReadOnlyList<Ballot> ballots, int indexFrom, int indexTo) {
			newBoss = destination; this.ballots = ballots; fromPosition = indexFrom; toPosition = indexTo;
		}
	}
	public VoteBloc(Candidate candidate, int start, IReadOnlyList<Ballot> ballots) {
		this.candidate = candidate; position = start; this.ballots = ballots;
	}
	public VoteBloc(VoteBloc other) {
		candidate = other.candidate; position = other.position; ballots = other.ballots;
	}

	/// <summary>voters are in a line, clumping with their top candidate</summary>
	public static void CalculateMigrations(List<VoteBloc> blocsThisState, List<VoteBloc> blocsLastState,
		Dictionary<Candidate, BallotsTransferingToCandidate> voteMigration) {
		List<Candidate> properOrderOfCandidatesInState = CalculateProperOrderOfAllCandidatesBetweenTwoStates(
			blocsThisState, blocsLastState);
		Dictionary<Candidate, int> whereVotesComeFrom = new Dictionary<Candidate, int>();
		Dictionary<Candidate, int> whereVotesGoingTo = new Dictionary<Candidate, int>();
		for (int i = 0; i < properOrderOfCandidatesInState.Count; ++i) {
			Candidate candidate = properOrderOfCandidatesInState[i];
			if (candidate == null) continue;
			int indexOfBlockLastState = IRV.GetBlocIndex(candidate, blocsLastState);
			if (indexOfBlockLastState < 0) continue;
			int indexOfBlockThisState = IRV.GetBlocIndex(candidate, blocsThisState);
			VoteBloc lastBloc = blocsLastState[indexOfBlockLastState];
			VoteBloc? thisBloc = indexOfBlockThisState >= 0 ? blocsThisState[indexOfBlockThisState] : null;
			if (thisBloc != null && thisBloc.ballotCount > 0) {
				if (lastBloc.migrations == null) lastBloc.migrations = new List<Migration>();
				lastBloc.migrations.Add(new Migration(candidate, lastBloc.ballots, lastBloc.position, thisBloc.position));
				continue;
			}
			if (!whereVotesComeFrom.TryGetValue(candidate, out _)) {
				whereVotesComeFrom[candidate] = lastBloc.position;
			}
			if (!voteMigration.TryGetValue(candidate, out BallotsTransferingToCandidate? whereTheVotesAreGoing)) {
				continue;
			}
			int votesMoved = 0;
			for (int j = 0; j < properOrderOfCandidatesInState.Count; ++j) {
				Candidate whoGetsMeNow = properOrderOfCandidatesInState[j];
				if (!whereTheVotesAreGoing.TryGetValue(whoGetsMeNow, out List<Ballot>? movingVotes) || movingVotes.Count == 0) {
					continue;
				}
				int blocIndexTargetThisState = IRV.GetBlocIndex(whoGetsMeNow, blocsThisState);
				if (blocIndexTargetThisState < 0) {
					throw new Exception($"`{whoGetsMeNow}` is not in the present state?");
				}
				VoteBloc nextBloc = blocsThisState[blocIndexTargetThisState];
				if (!whereVotesGoingTo.TryGetValue(nextBloc.candidate, out _)) {
					int blocIndexLastState = IRV.GetBlocIndex(whoGetsMeNow, blocsLastState);
					VoteBloc? nextBlocLastState = (blocIndexLastState >= 0) ? blocsLastState[blocIndexLastState] : null;
					if (nextBlocLastState != null) {
						whereVotesGoingTo[nextBloc.candidate] = nextBloc.position + nextBlocLastState.ballotCount;
					} else {
						whereVotesGoingTo[nextBloc.candidate] = nextBloc.position;
					}
				}
				if (lastBloc.migrations == null) lastBloc.migrations = new List<Migration>();
				Candidate thisLoser = lastBloc.candidate, theNextGuy = nextBloc.candidate;
				int votesCameFrom = whereVotesComeFrom[thisLoser], votesGoingTo = whereVotesGoingTo[theNextGuy];
				lastBloc.migrations.Add(new Migration(theNextGuy, movingVotes, votesCameFrom, votesGoingTo));
				whereVotesComeFrom[thisLoser] = votesCameFrom + movingVotes.Count;
				whereVotesGoingTo[theNextGuy] = votesGoingTo + movingVotes.Count;
				votesMoved += movingVotes.Count;
			}
		}
	}
	private static List<Candidate> CalculateProperOrderOfAllCandidatesBetweenTwoStates(
		List<VoteBloc> blocsThisState, List<VoteBloc> blocsLastState) {
		List<Candidate> properOrderOfCandidates = new List<Candidate>();
		HashSet<Candidate> listed = new HashSet<Candidate>();
		for (int i = 0; i < blocsLastState.Count; ++i) {
			Candidate c = blocsLastState[i].candidate;
			properOrderOfCandidates.Add(c);
			listed.Add(c);
		}
		for (int i = 0; i < blocsThisState.Count; ++i) {
			Candidate c = blocsLastState[i].candidate;
			if (!listed.Contains(c)) { properOrderOfCandidates.Add(c); }
		}
		return properOrderOfCandidates;
	}
}

/// <summary>Complete results  of an election</summary>
public class CompleteElectionResults {
	/// <summary>when ballots are exhausted, they should count for this candidate</summary>
	public Candidate? candidateForExhausted;
	/// <summary>candidates who were exhausted, not allowed to win</summary>
	public HashSet<Candidate> exhaustedCandidates; // TODO delete, and use the version in `CurrentCandidateVoteTallies`
	/// <summary>state of votes after each candidate elimination</summary>
	public List<VoteState> voteStates; // TODO is this needed after visualization is done? can we just calculate visualization and not do voteStates?
	/// <summary>where ballows travelled to between each state</summary>
	public List<Dictionary<Candidate, BallotsTransferingToCandidate>> voteMigrationHistory;
	public List<Ballot> exhaustedBallots = new List<Ballot>();
	/// <summary>list of winner. Tie possible if plurality is less than majority</summary>
	public IList<Candidate>? winner;
	public VoteVisualization? visualization;
	public string? label;
	public VoteState CurrentCandidateVoteTallies => voteStates[voteStates.Count - 1];
	public CompleteElectionResults(Candidate? candidateForExhausted) {
		this.candidateForExhausted = candidateForExhausted;
		exhaustedCandidates = new HashSet<Candidate>();
		if (candidateForExhausted != null) {
			exhaustedCandidates.Add(candidateForExhausted);
		}
		voteStates = new List<VoteState>();
		voteMigrationHistory = new List<Dictionary<Candidate, BallotsTransferingToCandidate>>();
		exhaustedBallots = new List<Ballot>();
	}
	public CompleteElectionResults(CompleteElectionResults other) {
		candidateForExhausted = other.candidateForExhausted;
		exhaustedCandidates = new HashSet<Candidate>(other.exhaustedCandidates);
		voteStates = new List<VoteState>(other.voteStates);
		voteMigrationHistory = new List<Dictionary<Candidate, BallotsTransferingToCandidate>>(other.voteMigrationHistory);
		exhaustedBallots = new List<Ballot>(other.exhaustedBallots);
		if (other.winner != null) { winner = new List<Candidate>(other.winner); }
		if (other.label != null) { label = other.label; }
	}
	public void AddExhaustedCandidates(IEnumerable<Candidate> candidates) {
		foreach (Candidate c in candidates) exhaustedCandidates.Add(c);
	}
	public void AddVoteCalculationState(List<Ballot> allBallots) {
		VoteState tally = new VoteState(allBallots, exhaustedCandidates);
		voteStates.Add(tally);
		//Print.DebugShow(tally, exhaustedCandidates);
	}

	public static List<Candidate> OrderByBallotCount(VoteState tally) {
		List<Candidate> out_order = new List<Candidate>();
		OrderByBallotCount(tally, out_order);
		return out_order;
	}
	public static void OrderByBallotCount(VoteState tally, List<Candidate> out_order) {
		out_order.Clear(); out_order.AddRange(tally.Keys);
		out_order.Sort((a,b) => b.TotalVotesWeighted.CompareTo(a.TotalVotesWeighted));
	}

	public void DuplicateLatestState() {
		voteStates.Add(new VoteState(CurrentCandidateVoteTallies));
	}
	public bool CalculateWinner(float pluralityPercentage, out float voteCount) {
		winner = MajorityCandidates(CurrentCandidateVoteTallies, out voteCount, pluralityPercentage);
		bool hasWinner = winner?.Count > 0;
		return hasWinner;
	}
	public IList<Candidate>? MajorityCandidates(VoteState tally, out float voteCountTotal, float pluralityPercentage = 0.5f) {
		voteCountTotal = SumUnexhaustedVotes(tally);
		if (voteCountTotal == 0) { return null; }
		List<Candidate>? winners = null;
		float majority = voteCountTotal * pluralityPercentage;
		foreach (var k in tally) {
			if (k.Key == candidateForExhausted) continue;
			float voteCountOfCandidate = IRV.SumVoteValue(k.Value);
			if (voteCountOfCandidate >= majority) {
				if (winners == null) winners = new List<Candidate>();
				winners.Add(k.Key);
			}
		}
		return winners;
	}
	// TODO replace with `tally.SumUnexhaustedVotes()`
	public float SumUnexhaustedVotes(VoteState tally) {
		float sumVotes = 0;
		foreach (var k in tally) {
			if (k.Key == candidateForExhausted) continue;
			float voteCount = IRV.SumVoteValue(k.Value);
			sumVotes += voteCount;
		}
		return sumVotes;
	}
	public List<Ballot> ExhaustCandidate(VoteState state, Candidate candidate) {
		exhaustedCandidates.Add(candidate);
		List<Ballot> exhaustedBallots = new List<Ballot>();
		if (!state.TryGetValue(candidate, out List<Ballot>? votes)) {
			votes = new List<Ballot>();
		} else {
			state.Remove(candidate);
		}
		Dictionary<Candidate, BallotsTransferingToCandidate> changesThisTime = new Dictionary<Candidate, BallotsTransferingToCandidate>();
		BallotsTransferingToCandidate votesMoveTo = new BallotsTransferingToCandidate();
		changesThisTime[candidate] = votesMoveTo;
		voteMigrationHistory.Add(changesThisTime);
		for (int i = 0; i < votes.Count; ++i) {
			Ballot ballot = votes[i];
			Candidate? next = ballot.GetBestChoice(exhaustedCandidates);
			if (next == null) {
				exhaustedBallots.Add(ballot);
				if (candidateForExhausted != null) {
					next = candidateForExhausted;
					if (!state.TryGetValue(candidateForExhausted, out List<Ballot>? exhuastedBallots)) {
						state[candidateForExhausted] = exhuastedBallots = new List<Ballot>();
					}
					exhuastedBallots.Add(ballot);
				}
				this.exhaustedBallots.Add(ballot);
			} else {
				if (!state.TryGetValue(next, out List<Ballot>? ballots)) {
					state[next] = ballots = new List<Ballot>();
				}
				ballots.Add(ballot);
			}
			if (next != null) {
				if (!votesMoveTo.TryGetValue(next, out List<Ballot>? movedTo)) {
					votesMoveTo[next] = movedTo = new List<Ballot>();
				}
				movedTo.Add(ballot);
			}
		}
		return exhaustedBallots;
	}
	/// <returns>number of candidates who can win (excludes 'exhausted' candidate)</returns>
	public int CountValidCanidates() {
		// TODO replace with `CurrentCandidateVoteTallies.CountValidCandidates()`
		int exhaustedCandidate = candidateForExhausted != null && CurrentCandidateVoteTallies.ContainsKey(candidateForExhausted) ? 1 : 0;
		return CurrentCandidateVoteTallies.Count - exhaustedCandidate;
	}
	public bool IsExhausted() => CountValidCanidates() == 0;
}

public class IRV {
	public static readonly Candidate BasicExhaustedCandidate = new Candidate(".", Color.darkGray);
	public List<Ballot>? ballots;
	/// <summary>where votes go when none of their candidates survived the runoff</summary>
	public Candidate? candidateForExhaustedBallots;
	public Candidate[][]? orderHarmonicBorda;
	public Candidate[][]? orderPopularity;
	public Candidate[][]? orderInstantRunoff;
	public List<Candidate>? candidates;
	public List<List<CompleteElectionResults>>? instantRunoffElectionsByRank;
	/// <param name="originalBallots"></param>
	/// <param name="maxWinnersCalculated">how many winners to calculate. -1 to calculate complete ranking</param>
	/// <param name="pluralityPercentage"></param>
	/// <returns></returns>
	public IEnumerator<Response> Calc(List<Ballot> originalBallots, int maxWinnersCalculated = -1, float pluralityPercentage = 0.5f) {
		IEnumerator<Response> ballotIngestion = IngestBallotsAndPurgeDuplicates(originalBallots);
		while (ballotIngestion.MoveNext()) { yield return ballotIngestion.Current; }
		if (ballots == null) {
			yield return Response.Error("Ballot ingestion failure");
			yield break;
		}
		candidates = Candidate.InstantVoteCalc(ballots, out orderHarmonicBorda, out orderPopularity);
		Candidate.FillInUnassignedColors(candidates);
		candidateForExhaustedBallots = GenerateExhaustedCandidatePlaceholder(candidates);
		instantRunoffElectionsByRank = new List<List<CompleteElectionResults>>();

		List<Candidate> winningCandidates = new List<Candidate>();
		List<Candidate[]> winnersIncTies = new List<Candidate[]>();
		IEnumerator<Response> CalculateVote() {
			for (int place = 0; maxWinnersCalculated < 0 || place < maxWinnersCalculated; ++place) {
				HashSet<Candidate> exhastedCandidates = new HashSet<Candidate>(winningCandidates);
				IEnumerator<Response> electionCalculationProcess = ElectionCalculation(exhastedCandidates, orderPopularity, pluralityPercentage);
				List<CompleteElectionResults>? electionVariations = null;
				while (electionCalculationProcess.MoveNext()) {
					electionVariations = electionCalculationProcess.Current.Message as List<CompleteElectionResults>;
					yield return electionCalculationProcess.Current;
				}
				if (electionVariations == null) continue;
				bool noElectionsCouldBeCalculated = electionVariations.Count == 0;
				if (noElectionsCouldBeCalculated) break;
				bool uniquePreferenceDetermined = AmbiguityInElectionsCollapsesToSingleOutcome(electionVariations);
				GenerateVisualsForEachElection(electionVariations, candidateForExhaustedBallots);
				LabelEachElection(electionVariations, place);
				CollateResults(electionVariations, winningCandidates, winnersIncTies);
				instantRunoffElectionsByRank.Add(electionVariations);
				yield return Response.Processing(electionVariations);
			}
			yield return Response.Success(this);
		}
		IEnumerator<Response> iterator = CalculateVote();
		while (iterator.MoveNext()) {
			yield return iterator.Current;
		}
		orderInstantRunoff = winnersIncTies.ToArray();
		Log.v("harmonic borda:  " + DebugPrint(orderHarmonicBorda));
		Log.v("just popularity: " + DebugPrint(orderPopularity));
		Log.v("instant runoff:  " + DebugPrint(orderInstantRunoff));
		string DebugPrint(Candidate[][] listing) {
			string s = "";
			for (int i = 0; i < listing.Length; i++) {
				if (i > 0) s += ";";
				s += string.Join(", ", (object[])listing[i]);
			}
			return s;
		}
	}

	private IEnumerator<Response> IngestBallotsAndPurgeDuplicates(IList<Ballot> originalBallots) {
		ballots = new List<Ballot>(originalBallots);
		int duplicateVotes = 0;
		foreach (var duplicateBallot in WhoVotedMoreThanOnce(originalBallots)) {
			yield return Response.Error($"`{ballots[duplicateBallot.Item1].id}` voted more than once, at `{duplicateBallot.Item1}` and `{duplicateBallot.Item2}`.");
			ballots.RemoveAt(duplicateBallot.Item2 - duplicateVotes);
			++duplicateVotes;
		}
	}

	private static bool AmbiguityInElectionsCollapsesToSingleOutcome(List<CompleteElectionResults> elections) {
		bool electionResultsAreUnambiguous = true;
		for (int i = 0; i < elections.Count; ++i) {
			IList<Candidate>? winner = elections[i].winner;
			if (winner == null) continue;
			for (int j = i + 1; j < elections.Count; ++j) {
				IList<Candidate>? otherWinner = elections[j].winner;
				if (otherWinner == null) continue;
				if (winner.SequenceEqual(otherWinner)) {
					elections.RemoveAt(j--);
					continue;
				}
				electionResultsAreUnambiguous = false;
			}
		}
		return electionResultsAreUnambiguous;
	}

	private static void GenerateVisualsForEachElection(List<CompleteElectionResults> elections, Candidate? candidateForExhaustedBallots) {
		for (int i = 0; elections != null && i < elections.Count; ++i) {
			CompleteElectionResults election = elections[i];
			// TODO move this to `CompleteElectionResults`
			VoteVisualization visualization = VoteVisualization.Create(election.voteStates, election.voteMigrationHistory);
			election.visualization = visualization;
		}
	}

	private static void LabelEachElection(List<CompleteElectionResults> elections, int whichRank) {
		elections.ForEach(e => {
			string winnerString = e.winner != null ? string.Join(", ", e.winner) : "<TBD>";
			e.label = $"rank {whichRank}: {winnerString}";
		});
	}

	private static void CollateResults(List<CompleteElectionResults> elections, List<Candidate> winningCandidates, List<Candidate[]> orderOfWinnersIncludingTies) {
		List<Candidate>? tie = elections.Count > 1 ? new List<Candidate>() : null;
		for (int i = 0; i < elections.Count; ++i) {
			CompleteElectionResults e = elections[i];
			if (e.winner == null) continue;
			if (e.winner.Count > 1 && tie == null) tie = new List<Candidate>();
			if (tie != null) tie.AddRange(e.winner);
			winningCandidates.AddRange(e.winner);
		}
		if (tie != null) {
			orderOfWinnersIncludingTies.Add(tie.ToArray());
		} else if (elections.Count == 1 && elections[0].winner != null) {
			orderOfWinnersIncludingTies.Add(new Candidate[] { elections[0].winner![0] });
		}
	}

	/// <returns>Duplicate ballot indexes</returns>
	public static IEnumerable<(int, int)> WhoVotedMoreThanOnce(IList<Ballot> allBallots) {
		Dictionary<string, int> voterId = new Dictionary<string, int>();
		for (int i = 0; i < allBallots.Count; ++i) {
			string? id = allBallots[i].id;
			if (id == null) throw new Exception($"Ballot {i} has null voter id");
			if (voterId.TryGetValue(id, out int alreadyInHere)) { yield return (alreadyInHere, i); }
			voterId[id] = i;
		}
	}

	static Candidate GenerateExhaustedCandidatePlaceholder(List<Candidate> listOfCandidates) {
		Candidate candidateForExhaustedBallots = new Candidate(BasicExhaustedCandidate);
		if (listOfCandidates.FindIndex(c => c.name == candidateForExhaustedBallots.name) >= 0) {
			IncrementingString.UniqueStringTest((str) => {
				candidateForExhaustedBallots.name = str;
				bool canidateHasThisName = listOfCandidates.FindIndex(c => c.name == str) >= 0;
				return canidateHasThisName;
			});
		}
		candidateForExhaustedBallots.SetExhaustedPlaceholder();
		return candidateForExhaustedBallots;
	}

	// finds where a bloc is in a given bloc state
	public static int GetBlocIndex(Candidate candidateName, List<VoteBloc> blocList) {
		for (int i = 0; i < blocList.Count; ++i) {
			if (blocList[i].candidate == candidateName) { return i; }
		}
		return -1;
	}

	private IEnumerator<Response> ElectionCalculation(
		HashSet<Candidate> exhastedCandidates,
		Candidate[][] candidatesByPopularity,
		float pluralityPercentage = 0.5f) {
		int loopGuard = 0;
		int processedElection = 0;
		List<CompleteElectionResults> electionsToProcess = new List<CompleteElectionResults>();
		CompleteElectionResults result = new CompleteElectionResults(candidateForExhaustedBallots);
		result.AddExhaustedCandidates(exhastedCandidates);
		result.AddVoteCalculationState(ballots!);
		if (result.IsExhausted()) {
			yield return Response.Success(electionsToProcess);
			yield break;
		}
		electionsToProcess.Add(result);
		int mostIterationsReasonablyPossible = candidatesByPopularity.Length * candidatesByPopularity.Length;
		do {
			if (++loopGuard > mostIterationsReasonablyPossible) throw new Exception("too many iterations");
			bool winnerFound = result.CalculateWinner(pluralityPercentage, out float voteCount);
			if (winnerFound || result.IsExhausted()) {
				yield return Response.Processing(electionsToProcess);
				if (++processedElection >= electionsToProcess.Count) {
					break;
				} else {
					result = electionsToProcess[processedElection];
				}
			}
			CountVoteExtremes(result.CurrentCandidateVoteTallies, out float leastVotes, out float mostVotes);
			Candidate? largestMinimumVoterBloc = GetFirstUnexhaustedCandidate(candidatesByPopularity, exhastedCandidates);
			float futureVoteCountEstimate = largestMinimumVoterBloc?.TotalVotesWeighted ?? voteCount;
			// before doing the standard remove-the-current-loser logic, clear out the extremely weak candidates that could never win.
			// eliminates the chance that statistical noise could remove an actual popular choice
			if (!TryGetTrulyWeakestCandidates(result.CurrentCandidateVoteTallies, futureVoteCountEstimate, pluralityPercentage, candidatesByPopularity, out List<Candidate> losers)) {
				losers = GetLosers(result.CurrentCandidateVoteTallies, leastVotes);
			}
			losers.Sort((a, b) => a.CompareTo(b));
			//if (losers.Count > 1) { Log.v($"tie for worst: {string.Join(", ", losers)}\n"); }
			for (int i = 0; i < losers.Count; i++) {
				CompleteElectionResults election;
				if (i == 0) {
					election = result;
				} else {
					election = new CompleteElectionResults(result);
					election.label += "drop " + losers[i];
					electionsToProcess.Add(election);
				}
				election.DuplicateLatestState();
				election.ExhaustCandidate(election.CurrentCandidateVoteTallies, losers[i]);
//				Print.DebugShow(election.CurrentCandidateVoteTallies, election.exhaustedCandidates);
				yield return Response.Processing(electionsToProcess);
			}
		} while (processedElection < electionsToProcess.Count);
		yield return Response.Success(electionsToProcess);
	}

	public static Candidate? GetFirstUnexhaustedCandidate(Candidate[][] candidatesByPopularity, HashSet<Candidate> exhastedCandidates) {
		for (int i = 0; i < candidatesByPopularity.Length; ++i) {
			for (int j = 0; j < candidatesByPopularity[i].Length; ++j) {
				if (!exhastedCandidates.Contains(candidatesByPopularity[i][j])) {
					return candidatesByPopularity[i][j];
				}
			}
		}
		return null;
	}

	// TODO move to VoteState
	public static bool TryGetTrulyWeakestCandidates(VoteState state, float voteCount, float pluralityPercentage, Candidate[][] likelyOrder,
		[NotNullWhen(true)] out List<Candidate> losers) {
		int minRequiredToWin = (int)(voteCount * pluralityPercentage);
		HashSet<Candidate> extremelyWeakCandidates = new HashSet<Candidate>();
		foreach (var kvp in state) {
			Candidate canditate = kvp.Key;
			if (canditate.TotalVotesWeighted < minRequiredToWin) {
				extremelyWeakCandidates.Add(canditate);
			}
		}
		losers = new List<Candidate>();
		if (extremelyWeakCandidates.Count == 0) {
			return false;
		}
		for (int i = likelyOrder.Length - 1; i >= 0; --i) {
			if (extremelyWeakCandidates.Contains(likelyOrder[i][0])) {
				foreach (Candidate loser in likelyOrder[i]) losers.Add(loser);
				return true;
			}
		}
		return false;
	}
	// TODO move to VoteState
	public List<Candidate> GetLosers(VoteState tally, float leastVotes) {
		List<Candidate> losers = new List<Candidate>();
		foreach (var k in tally) {
			if (k.Key == candidateForExhaustedBallots) continue;
			float voteCountOfCandidate = SumVoteValue(k.Value);
			if (voteCountOfCandidate <= leastVotes) {
				if (k.Key == null) { Log.e("why is null losing?... how is null a valid key?"); continue; }
				losers.Add(k.Key);
			}
		}
		return losers;
	}
	// TODO move to VoteState
	public void CountVoteExtremes(VoteState tally, out float leastVotes, out float mostVotes) {
		leastVotes = float.PositiveInfinity;
		mostVotes = float.NegativeInfinity;
		foreach (var k in tally) {
			if (k.Key == candidateForExhaustedBallots) continue;
			float voteCount = SumVoteValue(k.Value);
			if (voteCount < leastVotes) { leastVotes = voteCount; }
			if (voteCount > mostVotes) { mostVotes = voteCount; }
		}
	}

	public static float SumVoteValue(IList<Ballot> votes) {
		float sumVotes = 0;
		for (int i = 0; i < votes.Count; i++) {
			sumVotes += votes[i].BallotWeight;
		}
		return sumVotes;
	}
}

public static class IncrementingString {
	public delegate bool ReturnTrueToContinue(string test);
	/// <summary>brute-force run through every string</summary>
	/// <param name="returnsTrueToContinue">the function that checks each string. keep returning true to keep the loop going.</param>
	public static void UniqueStringTest(ReturnTrueToContinue returnsTrueToContinue, char minChar = (char)33, char maxCharInclusive = (char)126) {
		bool collision;
		string test = minChar.ToString();
		do {
			collision = returnsTrueToContinue(test);
			if (collision) {
				test = GetNext(test, minChar, maxCharInclusive);
			}
		} while (collision);
	}
	static string ReplaceAt(string str, int index, char c) => str.Substring(0, index) + c + str.Substring(index + 1);
	static string IncrementCharAtIndex(string str, int index) => ReplaceAt(str, index, (char)(str[index] + 1));
	static string GetNext(string test, char minChar = (char)33, char maxCharInclusive = (char)126) {
		if (test.Length == 0) return minChar.ToString();
		int index = 0;
		char c;
		do {
			test = IncrementCharAtIndex(test, index);
			c = test[index];
			if (c > maxCharInclusive) {
				test = ReplaceAt(test, index, minChar);
				index++;
				if (index >= test.Length) { test += minChar; return test; }
			}
		} while (c > maxCharInclusive);
		return test;
	}
}
