using src;
using src.Core;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
namespace irv.src;
using VotesPerCandidate = Dictionary<Candidate, List<Ballot>>;

public class Candidate {
	public string name;
	public Color color = Color.clear; // clear will be replaced automatically
	public float harmonicBordaCount = 0;
	public float totalVotesWeighted = 0;
	public int totalBallots = 0;
	override public string ToString() { return name; }
	public Candidate(string name) { this.name = name; }
	public Candidate(string name, Color color) { this.name = name; this.color = color; }
	public Candidate(Candidate copy) { name = copy.name; color = copy.color; harmonicBordaCount = copy.harmonicBordaCount; totalVotesWeighted = copy.totalVotesWeighted; }
}
public class Ballot {
	public string? id;
	public Candidate[]? vote;
	public float voteWeight = 1;
	public override string? ToString() => (vote != null ? "[" + string.Join(", ", Array.ConvertAll(vote, v => v.name)) + "]" : "")
		+ (voteWeight != 1 ? $"({voteWeight})" : "");
	public int GetBestChoiceIndex(HashSet<Candidate> exhastedCandidates) {
		if (vote == null) { return -1; }
		for (int i = 0; i < vote.Length; ++i) {
			if (!exhastedCandidates.Contains(vote[i])) {
				return i;
			}
		}
		return -1;
	}
	public Candidate? GetBestChoice(HashSet<Candidate> exhastedCandidates) {
		if (vote == null) return null;
		int index = GetBestChoiceIndex(exhastedCandidates);
		return index >= 0 ? vote[index] : null;
	}
}
public class VoteVisualization {
	//public IList<Candidate>? winner;
	public IList<Candidate> candidates;
	/// <summary>data to describe graphical representation [IRV rank][candidate]</summary>
	public List<List<VoteBloc>> data;
	public VoteVisualization(IList<Candidate> candidates, List<List<VoteBloc>> data) {
		this.candidates = candidates;
		this.data = data;
	}
	public static VoteVisualization Create(
		List<VotesPerCandidate> voteStateHistory,
		List<Dictionary<Candidate, VotesPerCandidate>> voteMigrationHistory,
		Candidate? candidateForExausted,
		IList<Candidate> candidatesListing) {
		List<List<VoteBloc>> visBlocs = new List<List<VoteBloc>>();
		CalculateVisBlocsBasedOnHistory(visBlocs, voteStateHistory, voteMigrationHistory, candidateForExausted);
		VoteVisualization sr = new VoteVisualization(candidatesListing, visBlocs);
		return sr;
	}
	/// <summary>calculate visualization model.</summary>
	/// <param name="out_visBlocs">where to append the visualization model.
	/// Each visualiation block explains which block moved from where to where.
	/// Every block exists at some index in a number line, and is the size of it's number of votes</param>
	/// <param name="voteStateHistory">the state of the votes at each step.</param>
	/// <param name="voteMigrationHistory">how the votes moved each state.</param>
	public static void CalculateVisBlocsBasedOnHistory(
		List<List<VoteBloc>> out_visBlocs,
		List<VotesPerCandidate> voteStateHistory,
		List<Dictionary<Candidate, VotesPerCandidate>> voteMigrationHistory, Candidate? candidateForExhausted) {
		List<VoteBloc> blocsThisState;
		List<VoteBloc>? blocsLastState = null;
		Dictionary<Candidate, float> weightsForThisVisualization = IRV.CalculateWeightByStateImportance(voteStateHistory);
		for (int stateIndex = 0; stateIndex < voteStateHistory.Count; ++stateIndex) {
			List<Candidate> sorted = IRV_OrderCandidatesForBlocs(voteStateHistory[stateIndex], weightsForThisVisualization, candidateForExhausted, true);
			blocsThisState = CalculateBlocs(sorted, voteStateHistory[stateIndex], weightsForThisVisualization);
			out_visBlocs.Add(blocsThisState);
			bool migrationsFromLastStateIsAvailable = blocsLastState != null;
			if (migrationsFromLastStateIsAvailable) {
				VoteBloc.CalculateMigrations(blocsThisState, blocsLastState!, candidateForExhausted, voteMigrationHistory[stateIndex - 1]);
			}
			blocsLastState = blocsThisState;
		}
	}

	/// <returns>order of choices based on the tally, using tieBreakerData weighting to separate ties.</returns>
	static List<Candidate> IRV_OrderCandidatesForBlocs(VotesPerCandidate tally, Dictionary<Candidate, float> tieBreakerData,
		Candidate? candidateForExhausted, bool forceTieBreakerDataAsOrder = false) {
		List<Candidate> order = new List<Candidate>(tally.Keys);
		HashSet<Candidate> candidatesInTheVisualization = new HashSet<Candidate>(order);
		foreach (var kvp in tieBreakerData) {
			if (candidatesInTheVisualization.Add(kvp.Key)) {
				order.Add(kvp.Key);
			}
		}
		order.Sort((a, b) => {
			float countA = tally.TryGetValue(a, out List<Ballot>? ballotsA) ? IRV.SumVoteValue(ballotsA) : 0;
			float countB = tally.TryGetValue(b, out List<Ballot>? ballotsB) ? IRV.SumVoteValue(ballotsB) : 0;
			float diff = countB - countA;
			if (forceTieBreakerDataAsOrder || diff == 0) {
				diff = tieBreakerData[b] - tieBreakerData[a];
			}
			return (int)(diff * 1024);
		});
		// ensure that exhausted candidates appear at the end
		if (candidateForExhausted != null && order[order.Count - 1] != candidateForExhausted) {
			int exhaustedIndex = order.IndexOf(candidateForExhausted);
			if (exhaustedIndex >= 0) {
				order.RemoveAt(exhaustedIndex);
				order.Add(candidateForExhausted);
			}
		}
		return order;
	}
	public static List<VoteBloc> CalculateBlocs(List<Candidate> sorted, VotesPerCandidate voteState, Dictionary<Candidate, float> candidateWeight) {
		List<VoteBloc> blocsThisState = new List<VoteBloc>();
		int cursor = 0;
		for (int s = 0; s < sorted.Count; ++s) {
			int voteCount = 0;
			if (voteState.TryGetValue(sorted[s], out List<Ballot>? thisGuyVotes) && thisGuyVotes.Count != 0) {
				voteCount = thisGuyVotes.Count;
			}
			VoteBloc bloc = new VoteBloc(sorted[s], cursor, voteCount);
			blocsThisState.Add(bloc);
			cursor += voteCount;
		}
		return blocsThisState;
	}
}
public class VoteBloc {
	public Candidate candidate;
	public int position;
	public int ballotCount;
	public class Migration {
		public Candidate newBoss;
		public int count, fromPosition, toPosition;
		public Migration(Candidate destination, int ballotCount, int indexFrom, int indexTo) {
			newBoss = destination; count = ballotCount; fromPosition = indexFrom; toPosition = indexTo;
		}
	}
	/// the next blocs that these votes go into
	public List<Migration>? migrations;
	public VoteBloc(Candidate candidate, int start, int ballots) {
		this.candidate = candidate; position = start; ballotCount = ballots;
	}

	public static void CalculateMigrations(List<VoteBloc> blocsThisState, List<VoteBloc> blocsLastState, Candidate? candidateForExhausted,
		Dictionary<Candidate, VotesPerCandidate> voteMigration) {
		List<Candidate> properOrderOfCandidatesInState = CalculateProperOrderOfAllCandidatesBetweenTwoStates(
			blocsThisState, blocsLastState, candidateForExhausted);
		// voters are in a line, where in the line are the votes coming and going? calculate for each candidate.
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
				lastBloc.migrations.Add(new Migration(candidate, lastBloc.ballotCount, lastBloc.position, thisBloc.position));
				continue;
			}
			if (!whereVotesComeFrom.TryGetValue(candidate, out _)) {
				whereVotesComeFrom[candidate] = lastBloc.position;
			}
			if (!voteMigration.TryGetValue(candidate, out VotesPerCandidate? whereTheVotesAreGoing)) {
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
				lastBloc.migrations.Add(new Migration(theNextGuy, movingVotes.Count, votesCameFrom, votesGoingTo));
				whereVotesComeFrom[thisLoser] = votesCameFrom + movingVotes.Count;
				whereVotesGoingTo[theNextGuy] = votesGoingTo + movingVotes.Count;
				votesMoved += movingVotes.Count;
			}
		}
	}
	private static List<Candidate> CalculateProperOrderOfAllCandidatesBetweenTwoStates(
		List<VoteBloc> blocsThisState, List<VoteBloc> blocsLastState, Candidate? candidateForExhausted) {
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
		if (candidateForExhausted != null) {
			int index = properOrderOfCandidates.IndexOf(candidateForExhausted);
			if (index >= 0) {
				properOrderOfCandidates.RemoveAt(index);
				properOrderOfCandidates.Add(candidateForExhausted);
			}
		}
		return properOrderOfCandidates;
	}
}

/// <summary>
/// Complete results  of an election
/// </summary>
public class CompleteElectionResults {
	/// <summary>when ballots are exhausted, they should count for this candidate TODO implement so this argument can stop being passed everywhere</summary>
	public Candidate? candidateForExhausted;
	/// <summary>candidates who were exhausted, not allowed to win</summary>
	public HashSet<Candidate> exhaustedCandidates;
	/// <summary>state of votes after each candidate elimination</summary>
	public List<VotesPerCandidate> out_voteState;
	/// <summary>where ballows travelled to between each state</summary>
	public List<Dictionary<Candidate, VotesPerCandidate>> out_voteMigrationHistory;
	public List<Ballot> exhaustedBallots = new List<Ballot>();
	/// <summary>if not null, this is the list of winners. a tie is possible if plurality is less than 50%. TODO determine if a tie really is possible... what happens if multiple plurality is possible after another disqualification?</summary>
	public IList<Candidate>? winner;
	public VoteVisualization? visualization;
	public string? label;
	public VotesPerCandidate CurrentCandidateVoteTallies => out_voteState[out_voteState.Count - 1];
	public CompleteElectionResults(Candidate? candidateForExhausted) {
		this.candidateForExhausted = candidateForExhausted;
		exhaustedCandidates = new HashSet<Candidate>();
		out_voteState = new List<VotesPerCandidate>();
		out_voteMigrationHistory = new List<Dictionary<Candidate, VotesPerCandidate>>();
		exhaustedBallots = new List<Ballot>();
	}
	public CompleteElectionResults(CompleteElectionResults other) {
		candidateForExhausted = other.candidateForExhausted;
		exhaustedCandidates = new HashSet<Candidate>(other.exhaustedCandidates);
		out_voteState = new List<VotesPerCandidate>(other.out_voteState);
		out_voteMigrationHistory = new List<Dictionary<Candidate, VotesPerCandidate>>(other.out_voteMigrationHistory);
		exhaustedBallots = new List<Ballot>(other.exhaustedBallots);
		if (other.winner != null) { winner = new List<Candidate>(other.winner); }
		if (other.label != null) { label = other.label; }
	}
	public void ExhaustCandidates(IEnumerable<Candidate> candidates) {
		foreach (Candidate c in candidates) exhaustedCandidates.Add(c);
	}
	public void AddVoteCalculationState(List<Ballot> allBallots) {
		VotesPerCandidate tally = new VotesPerCandidate();
		TallyVotes(tally, allBallots, exhaustedCandidates, candidateForExhausted);
		out_voteState.Add(tally);
	}

	/// <param name="out_tally">a table of all of the votes, seperated by vote winner</param>
	/// <param name="exhastedCandidates">candidates who should not count (move to the next choice in the vote's ranked list)</param>
	/// <param name="candidateForExhausted">candidate that takes fully exuausted ballots</param>
	public static void TallyVotes(VotesPerCandidate out_tally, List<Ballot> ballots, HashSet<Candidate> exhastedCandidates, Candidate? candidateForExhausted) {
		for (int i = 0; i < ballots.Count; ++i) {
			Ballot b = ballots[i];
			Candidate? bestChoice = b.GetBestChoice(exhastedCandidates);
			if (bestChoice == null) bestChoice = candidateForExhausted;
			if (bestChoice == null) continue;
			List<Ballot>? supportForChoice = out_tally.ContainsKey(bestChoice) ? out_tally[bestChoice] : null;
			if (supportForChoice == null) {
				out_tally[bestChoice] = supportForChoice = new List<Ballot>();
			}
			supportForChoice.Add(b);
		}
	}

	public void DuplicateLatestState() {
		out_voteState.Add(CloneVotesPerCandidate(CurrentCandidateVoteTallies));
	}
	static VotesPerCandidate CloneVotesPerCandidate(VotesPerCandidate tally) {
		VotesPerCandidate cloned = new VotesPerCandidate();
		foreach (var k in tally) {
			cloned[k.Key] = new List<Ballot>(k.Value);
		}
		return cloned;
	}
	public bool CalculateWinner(float pluralityPercentage, out float voteCount) {
		winner = MajorityCandidates(CurrentCandidateVoteTallies, out voteCount, pluralityPercentage);
		bool hasWinner = winner?.Count > 0;
		return hasWinner;
	}
	public IList<Candidate>? MajorityCandidates(VotesPerCandidate tally, out float voteCountTotal, float pluralityPercentage = 0.5f) {
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
	public float SumUnexhaustedVotes(VotesPerCandidate tally) {
		float sumVotes = 0;
		foreach (var k in tally) {
			if (k.Key == candidateForExhausted) continue;
			float voteCount = IRV.SumVoteValue(k.Value);
			sumVotes += voteCount;
		}
		return sumVotes;
	}
	public List<Ballot> ExhaustCandidate(VotesPerCandidate state, Candidate candidate) {
		exhaustedCandidates.Add(candidate);
		List<Ballot> exhaustedBallots = new List<Ballot>();
		if (!state.TryGetValue(candidate, out List<Ballot>? votes)) {
			votes = new List<Ballot>();
		} else {
			state.Remove(candidate);
		}
		Dictionary<Candidate, VotesPerCandidate> changesThisTime = new Dictionary<Candidate, VotesPerCandidate>();
		VotesPerCandidate votesMoveTo = new VotesPerCandidate();
		changesThisTime[candidate] = votesMoveTo;
		out_voteMigrationHistory.Add(changesThisTime);
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
		int exhaustedCandidate = candidateForExhausted != null && CurrentCandidateVoteTallies.ContainsKey(candidateForExhausted) ? 1 : 0;
		return CurrentCandidateVoteTallies.Count - exhaustedCandidate;
	}
	public bool IsExhausted() => CountValidCanidates() == 0;
}

// TODO check some of the static methods and make them non-static if it could reduce argument count
public class IRV {
	/// <summary>where votes go when none of their candidates survived the runoff.
	/// regenerated each vote to ensure no collision with candidate names</summary>
	public static readonly Candidate BasicExhaustedCandidate = new Candidate(".", Color.darkGray);
	// TODO move to Color?
	private static Color[] UnambiguousColorSequence = new Color[]{
		Color.red, Color.green, Color.blue, //"888",
		Color.yellow, Color.cyan, Color.magenta, //"222",
		Color.darkRed, new Color(.75f,1,.75f), Color.darkBlue, //"666", 
		new Color(1,1,.75f),Color.darkCyan,new Color(1,.75f,1),
		Color.darkYellow,new Color(.75f,1,1),Color.darkMagenta,
		new Color(1,.75f,.75f),Color.darkGreen,new Color(.75f,.75f,1),
		new Color(1,.5f,0),new Color(0,1,.5f),new Color(.5f,0,1),
		new Color(1,0,.5f),new Color(.5f,1,0),new Color(0,.5f,1),
		new Color(.25f,.5f,0),new Color(0,.25f,.5f),new Color(.5f,0,.25f)
	};

	public List<Ballot>? ballots;
	public Candidate? candidateForExhaustedBallots;
	public Candidate[][]? orderHarmonicBorda;
	public Candidate[][]? orderPopularity;
	public Candidate[][]? orderInstantRunoff;
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
		List<Candidate> candidates = SimpleVoteCalc(ballots, out orderHarmonicBorda, out orderPopularity);
		FillInUnassignedColors(candidates);
		candidateForExhaustedBallots = GenerateExhaustedCandidatePlaceholder(candidates);
		instantRunoffElectionsByRank = new List<List<CompleteElectionResults>>();

		List<Candidate> winningCandidates = new List<Candidate>();
		List<Candidate[]> winnersIncTies = new List<Candidate[]>();
		IEnumerator<Response> CalculateVote() {
			for (int place = 0; maxWinnersCalculated < 0 || place < maxWinnersCalculated; ++place) {
				HashSet<Candidate> exhastedCandidates = new HashSet<Candidate>(winningCandidates);
				IEnumerator<Response> electionCalculationProcess = ElectionCalculation(exhastedCandidates, ballots, candidateForExhaustedBallots, orderPopularity, pluralityPercentage);
				List<CompleteElectionResults>? electionVariations = null;
				while (electionCalculationProcess.MoveNext()) {
					electionVariations = electionCalculationProcess.Current.Message as List<CompleteElectionResults>;
					yield return electionCalculationProcess.Current;
				}
				if (electionVariations == null) continue;
				bool noElectionsCouldBeCalculated = electionVariations.Count == 0;
				if (noElectionsCouldBeCalculated) break;
				bool uniquePreferenceDetermined = AmbiguityInElectionsCollapsesToSingleOutcome(electionVariations);
				GenerateVisualsForEachElection(electionVariations, candidates, candidateForExhaustedBallots);
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
	private static void FillInUnassignedColors(IList<Candidate> candidates) {
		List<Color> colorList = new List<Color>(UnambiguousColorSequence);
		AssignColorsToCandidates(candidates, colorList);
	}
	// TODO move to Color somehow? Func args for getting and setting colors?
	/// <summary>Generates a default color for each candidate, if needed.</summary>
	/// <param name="listing">out_Listing. the list of Candidates. If the Candidate has no coloration, it will have one after this method</param>
	private static void AssignColorsToCandidates(IList<Candidate> candidates, List<Color> colorList) {
		// remove auto-colors that are too close to the existing candidates
		for (int i = 0; i < candidates.Count; ++i) {
			if (candidates[i].color == Color.clear) continue;
			var mostSimilarColors = colorList.OrderBy(c => Color.Distance(c, candidates[i].color));
			foreach (Color similarColor in mostSimilarColors) {
				float dist = Color.Distance(similarColor, candidates[i].color);
				if (dist > 32) break;
				colorList.Remove(similarColor);
			}
		}
		// assign colors to candidates without coloration
		int colorindex = 0;
		foreach (Candidate k in candidates) {
			if (k.color != Color.clear) continue;
			k.color = colorList[(colorindex++) % colorList.Count];
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
	private static void GenerateVisualsForEachElection(List<CompleteElectionResults> elections, IList<Candidate> candidatesInOrder, Candidate? candidateForExhaustedBallots) {
		for (int i = 0; elections != null && i < elections.Count; ++i) {
			CompleteElectionResults election = elections[i];
			VoteVisualization visualization = VoteVisualization.Create(
				election.out_voteState, election.out_voteMigrationHistory, candidateForExhaustedBallots, candidatesInOrder);
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

	/// <summary>calculates vote heuristics for each candidate</summary>
	/// <returns>list of candidates</returns>
	static List<Candidate> SimpleVoteCalc(IList<Ballot> ballots, out Candidate[][] harmonicBorda, out Candidate[][] popularity) {
		HashSet<Candidate> completeSet = new HashSet<Candidate>();
		for (int v = 0; v < ballots.Count; ++v) {
			Ballot ballot = ballots[v];
			if (ballot.vote == null) continue;
			for (int i = 0; i < ballot.vote.Length; ++i) {
				Candidate candidate = ballot.vote[i];
				if (completeSet.Add(candidate)) {
					candidate.totalBallots = 0;
					candidate.totalVotesWeighted = 0;
					candidate.harmonicBordaCount = 0;
				}
				candidate.totalBallots += 1;
				candidate.totalVotesWeighted += ballot.voteWeight;
				candidate.harmonicBordaCount += (1 / (i + 1.0f)) * ballot.voteWeight;
			}
		}
		List<Candidate> candidateList = completeSet.ToList();
		candidateList.Sort((a, b) => Math.Sign(b.totalVotesWeighted - a.totalVotesWeighted));
		popularity = GenerateOrderIncludingTies(candidateList, c => c.harmonicBordaCount);
		candidateList.Sort((a, b) => Math.Sign(b.harmonicBordaCount - a.harmonicBordaCount));
		harmonicBorda = GenerateOrderIncludingTies(candidateList, c => c.harmonicBordaCount);
		return candidateList;
	}
	private static Candidate[][] GenerateOrderIncludingTies(IList<Candidate> candidates, Func<Candidate,float> getValue) {
		int ties = 0;
		for (int i = 0; i < candidates.Count - 1; ++i) {
			if (getValue(candidates[i]) == getValue(candidates[i + 1])) ++ties;
		}
		Candidate[][] order = new Candidate[candidates.Count - ties][];
		int index = 0;
		for (int i = 0; i < candidates.Count; ++i) {
			ties = 0;
			for (int j = i; j < candidates.Count - 1; j++) {
				if (getValue(candidates[j]) != getValue(candidates[j+1])) break;
				++ties;
			}
			order[index] = new Candidate[ties+1];
			order[index][0] = candidates[i];
			for (int j = 0; j < ties; j++) {
				order[index][j+1] = candidates[i+1+j];
			}
			++index;
			i += ties;
		}
		return order;
	}

	static Candidate GenerateExhaustedCandidatePlaceholder(List<Candidate> listOfCandidates) {
		Candidate candidateForExhaustedBallots = new Candidate(BasicExhaustedCandidate);
		IncrementingString.UniqueStringTest((str) => {
			candidateForExhaustedBallots.name = str;
			bool canidateHasThisName = listOfCandidates.FindIndex(c => c.name == str) >= 0;
			return canidateHasThisName;
		});
		return candidateForExhaustedBallots;
	}

	// finds where a bloc is in a given bloc state
	public static int GetBlocIndex(Candidate candidateName, List<VoteBloc> blocList) {
		for (int i = 0; i < blocList.Count; ++i) {
			if (blocList[i].candidate == candidateName) { return i; }
		}
		return -1;
	}

	public static Dictionary<Candidate,float> CalculateWeightByStateImportance(List<VotesPerCandidate> voteStateHistory) {
		Dictionary<Candidate, float> weightsForThisVisualization = new Dictionary<Candidate, float>();
		for (int s = 0; s < voteStateHistory.Count; ++s) {
			VotesPerCandidate state = voteStateHistory[s];
			foreach (KeyValuePair<Candidate, List<Ballot>> kvp in state) {
				float voteCountOfCandidate = SumVoteValue(kvp.Value);
				if (weightsForThisVisualization.TryGetValue(kvp.Key, out float val)) {
					val += voteCountOfCandidate;
				} else {
					val = voteCountOfCandidate;
				}
				weightsForThisVisualization[kvp.Key] = val;
			}
		}
		return weightsForThisVisualization;
	}

	private static IEnumerator<Response> ElectionCalculation(
		HashSet<Candidate> exhastedCandidates,
		List<Ballot> allBallots,
		Candidate candidateForExhaustedBallots,
		Candidate[][] candidatesByPopularity,
		float pluralityPercentage = 0.5f) {
		int iterations = 0;
		int processedElection = 0;
		List<CompleteElectionResults> electionsToProcess = new List<CompleteElectionResults>();
		CompleteElectionResults result = new CompleteElectionResults(candidateForExhaustedBallots);// TODO test this code with null as the exhausted candidate.
		result.ExhaustCandidates(exhastedCandidates);
		result.AddVoteCalculationState(allBallots);
		if (result.IsExhausted()) {
			yield return Response.Success(electionsToProcess);
			yield break;
		}
		electionsToProcess.Add(result);
		do {
			if (++iterations > 10000) throw new Exception("too many iterations");
			bool winnerFound = result.CalculateWinner(pluralityPercentage, out float voteCount);
			if (winnerFound || result.IsExhausted()) {
				yield return Response.Processing(electionsToProcess);
				if (++processedElection >= electionsToProcess.Count) {
					break;
				} else {
					result = electionsToProcess[processedElection];
				}
			}
			CountVoteExtremes(result.CurrentCandidateVoteTallies, out float leastVotes, out float mostVotes, candidateForExhaustedBallots);
			Candidate? largestMinimumVoterBloc = GetFirstUnexhaustedCandidate(candidatesByPopularity, exhastedCandidates);
			float futureVoteCountEstimate = largestMinimumVoterBloc?.totalVotesWeighted ?? voteCount;
			// before doing the standard remove-the-current-loser logic, clear out the extremely weak candidates that could never win.
			// eliminates the chance that statistical noise could remove an actual popular choice
			if (!TryGetTrulyWeakestCandidates(result.CurrentCandidateVoteTallies, futureVoteCountEstimate, pluralityPercentage, candidatesByPopularity, out List<Candidate> losers)) {
				losers = GetLosers(result.CurrentCandidateVoteTallies, leastVotes, candidateForExhaustedBallots);
			}
			losers.Sort((a, b) => { return a.totalVotesWeighted != b.totalVotesWeighted ? a.totalVotesWeighted.CompareTo(b.totalVotesWeighted) : a.harmonicBordaCount.CompareTo(b.harmonicBordaCount); });
			if (losers.Count > 1) {
				Log.v($"tie for worst: {string.Join(", ", losers)}\n");
			}
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
	public static bool TryGetTrulyWeakestCandidates(VotesPerCandidate state, float voteCount, float pluralityPercentage, Candidate[][] likelyOrder,
		[NotNullWhen(true)] out List<Candidate>? losers) {
		int minRequiredToWin = (int)(voteCount * pluralityPercentage);
		HashSet<Candidate> extremelyWeakCandidates = new HashSet<Candidate>();
		foreach (var kvp in state) {
			Candidate canditate = kvp.Key;
			if (canditate.totalVotesWeighted < minRequiredToWin) {
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
	public static List<Candidate> GetLosers(VotesPerCandidate tally, float leastVotes, Candidate fullyExhausted) {
		List<Candidate> losers = new List<Candidate>();
		foreach (var k in tally) {
			if (k.Key == fullyExhausted) continue;
			float voteCountOfCandidate = SumVoteValue(k.Value);
			if (voteCountOfCandidate <= leastVotes) {
				if (k.Key == null) { Log.e("why is null losing?... how is null a valid key?"); continue; }
				losers.Add(k.Key);
			}
		}
		return losers;
	}
	public static void CountVoteExtremes(VotesPerCandidate tally, out float leastVotes, out float mostVotes, Candidate candidateForExhaustedVotes) {
		leastVotes = float.PositiveInfinity;
		mostVotes = float.NegativeInfinity;
		foreach (var k in tally) {
			if (k.Key == candidateForExhaustedVotes) continue;
			float voteCount = SumVoteValue(k.Value);
			if (voteCount < leastVotes) { leastVotes = voteCount; }
			if (voteCount > mostVotes) { mostVotes = voteCount; }
		}
	}

	public static float SumVoteValue(IList<Ballot> votes) {
		float sumVotes = 0;
		for (int i = 0; i < votes.Count; i++) {
			sumVotes += votes[i].voteWeight;
		}
		return sumVotes;
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
}
