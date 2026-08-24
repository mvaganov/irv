using src;
using src.Core;
using System.Diagnostics.CodeAnalysis;
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
public class VoteCampaign {
	public string title;
	public IList<Candidate>? winner;
	public int numBallots;
	public IList<Candidate> candidates;
	/// <summary>data to describe graphical representation [IRV rank][candidate]</summary>
	public List<List<VoteBloc>> data;
	public VoteCampaign(string title, int numBallots, IList<Candidate> candidates, List<List<VoteBloc>> data) {
		this.numBallots = numBallots;
		this.candidates = candidates;
		this.title = title;
		this.data = data;
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
public class ElectionResultsStepByStep {
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
	public VoteCampaign? serialized;
	public string? note;
	public VotesPerCandidate CurrentCandidateVoteTallies => out_voteState[out_voteState.Count - 1];
	public ElectionResultsStepByStep(Candidate? candidateForExhausted) {
		this.candidateForExhausted = candidateForExhausted;
		exhaustedCandidates = new HashSet<Candidate>();
		out_voteState = new List<VotesPerCandidate>();
		out_voteMigrationHistory = new List<Dictionary<Candidate, VotesPerCandidate>>();
		exhaustedBallots = new List<Ballot>();
	}
	public ElectionResultsStepByStep(ElectionResultsStepByStep other) {
		candidateForExhausted = other.candidateForExhausted;
		exhaustedCandidates = new HashSet<Candidate>(other.exhaustedCandidates);
		out_voteState = new List<VotesPerCandidate>(other.out_voteState);
		out_voteMigrationHistory = new List<Dictionary<Candidate, VotesPerCandidate>>(other.out_voteMigrationHistory);
		exhaustedBallots = new List<Ballot>(other.exhaustedBallots);
		if (other.winner != null) { winner = new List<Candidate>(other.winner); }
		if (other.note != null) { note = other.note; }
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

public class IRV {
	/// <summary>where votes go when none of their candidates survived the runoff.
	/// regenerated each vote to ensure no collision with candidate names</summary>
	public static readonly Candidate BasicExhaustedCandidate = new Candidate(".", Color.darkGray);
	private static Color[] s_IRV_colorList = new Color[]{
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

	/// <param name="originalBallots"></param>
	/// <param name="maxWinnersCalculated">how many winners to calculate. -1 to calculate complete ranking</param>
	/// <param name="pluralityPercentage"></param>
	/// <returns></returns>
	public static IEnumerator<Response> Calc(List<Ballot> originalBallots, int maxWinnersCalculated = -1, float pluralityPercentage = 0.5f) {
		List<Ballot> ballots = new List<Ballot>(originalBallots);
		// purge duplicate ballots
		int duplicateVotes = 0;
		foreach (var duplicateBallot in WhoVotedMoreThanOnce(originalBallots)) {
			yield return Response.Error($"`{ballots[duplicateBallot.Item1].id}` voted more than once, at `{duplicateBallot.Item1}` and `{duplicateBallot.Item2}`.");
			ballots.RemoveAt(duplicateBallot.Item2 - duplicateVotes);
			++duplicateVotes;
		}
		// sort candidates by Harmonic Borda Count and Vote Popularity. used for visualization ordering. TODO are both needed?
		List<Candidate> candidates = WeightedVoteCalc(ballots);
		Candidate[] popularityOrder = candidates.ToArray();
		Array.Sort(popularityOrder, (a, b) => a.totalVotesWeighted < b.totalVotesWeighted ? -1 : b.totalVotesWeighted < a.totalVotesWeighted ? 1 : 0);

		Candidate candidateForExhaustedBallots = GenerateExhaustedCandidatePlaceholder(candidates);
		List<Color> colorList = new List<Color>(s_IRV_colorList);
		AssignColorsToCandidates(candidates, colorList);
		candidates.Insert(0, candidateForExhaustedBallots);

		List<Candidate> winningCandidatesInOrder = new List<Candidate>();
		List<ElectionResultsStepByStep>? elections = null;
		IEnumerator<Response> calcIteration() {
			for (int place = 0; maxWinnersCalculated < 0 || place < maxWinnersCalculated; ++place) {
				HashSet<Candidate> exhastedCandidates = new HashSet<Candidate>(winningCandidatesInOrder);
				IEnumerator<Response> electionCalculationProcess = ElectionCalculation(exhastedCandidates, ballots, candidateForExhaustedBallots, popularityOrder, pluralityPercentage);
				while (electionCalculationProcess.MoveNext()) {
					elections = electionCalculationProcess.Current.Message as List<ElectionResultsStepByStep>;
					yield return electionCalculationProcess.Current;
				}
				if (elections == null) continue;
				bool noElectionsCouldBeCalculated = elections.Count == 0;
				if (noElectionsCouldBeCalculated) break;
				// remove subsequent elections with the same outcome
				for (int i = 0; i < elections.Count; ++i) {
					IList<Candidate>? winner = elections[i].winner;
					if (winner == null) continue;
					for(int j = i + 1; j < elections.Count; ++j) {
						IList<Candidate>? otherWinner = elections[j].winner;
						if (otherWinner == null) continue;
						if (winner.SequenceEqual(otherWinner)) {
							elections.RemoveAt(j--);
							continue;
						}
						Log.w("---------------------- ALTERNATIVE RESULT!");
					}
				}
				for (int i = 0; elections != null && i < elections.Count; ++i) {
					Log.d($"calculating visuals for election[{i}]");
					List<List<VoteBloc>> visBlocs = new List<List<VoteBloc>>();
					List<VotesPerCandidate> voteStateHistory = elections[i].out_voteState;
					List<Dictionary<Candidate, VotesPerCandidate>> voteMigrationHistory = elections[i].out_voteMigrationHistory;
					IRV_calculateVisualizationModel(visBlocs, voteStateHistory, voteMigrationHistory, candidateForExhaustedBallots);

					VoteCampaign serialized =
						CalculateSerializedVisualization(visBlocs, candidates, ballots.Count, $"rank {place}");

					// IRV_out(place+ "> "+best.winner);
					serialized.title = $"rank {place}";
					serialized.winner = elections[i].winner;
					elections[i].serialized = serialized;
					//best.rank = place;
					//best.showme = serialized;
					//results.Add(serialized);
					if (serialized.winner != null) {
						//place += 1;// serialized.winner.Count - 1; // the -1 is because place gets an automatic ++ in the main loop
						winningCandidatesInOrder.AddRange(serialized.winner); //winners = winners.concat(best.winner);
					}
				}
				//place++;
				if (maxWinnersCalculated < 0 || place < maxWinnersCalculated) {
					yield return Response.Processing(elections);
				} else {
					break;
				}
			}
			yield return Response.Success(elections);
		}
		IEnumerator<Response> iterator = calcIteration();
		while (iterator.MoveNext()) {
			yield return iterator.Current;
		}
	}

	/// <returns>Duplicate ballot indexes</returns>
	public static IEnumerable<(int, int)> WhoVotedMoreThanOnce(List<Ballot> allBallots) {
		Dictionary<string, int> voterId = new Dictionary<string, int>();
		for (int i = 0; i < allBallots.Count; ++i) {
			string? id = allBallots[i].id;
			if (id == null) throw new Exception($"Ballot {i} has null voter id");
			if (voterId.TryGetValue(id, out int alreadyInHere)) { yield return (alreadyInHere, i); }
			voterId[id] = i;
		}
	}

	/// <summary>calculates vote heuristics for each candidate</summary>
	/// <returns>list of Candidates by weight, which is used for tie-breaking when multiple candidates are about to be removed</returns>
	static List<Candidate> WeightedVoteCalc(List<Ballot> ballots) {
		// calculate a weighted score, and total-vote-count, which are simpler algorithms than Instant Runoff Voting
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
		candidateList.Sort((a, b) => {
			return (int)((b.harmonicBordaCount - a.harmonicBordaCount) * 1024);
		});
		return candidateList;
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


	/// <summary>Generates a default color for each candidate, if needed.</summary>
	/// <param name="listing">out_Listing. the list of Candidates. If the Candidate has no coloration, it will have one after this method</param>
	static void AssignColorsToCandidates(List<Candidate> candidates, List<Color> colorList) {
		// remove auto-colors that are too close to the existing candidates
		for (int i = 0; i < candidates.Count; ++i) {
			if (candidates[i].color == Color.clear) continue;
			var mostSimilarColors = colorList.OrderBy(c => Color.Distance(c, candidates[i].color));
			foreach(Color similarColor in mostSimilarColors) {
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

	/// <returns>The order choices of choices based on the tally, using tieBreakerData weighting to separate ties.</returns>
	static List<Candidate> IRV_OrderCandidatesForBlocs(VotesPerCandidate tally, Dictionary<Candidate, float> tieBreakerData,
		Candidate? candidateForExhausted, bool forceTieBreakerDataAsOrder = false) {
		List<Candidate> order = new List<Candidate>(tally.Keys);
		HashSet<Candidate> candidatesInTheVisualization = new HashSet<Candidate>(order);
		foreach(var kvp in tieBreakerData) {
			if (candidatesInTheVisualization.Add(kvp.Key)) {
				order.Add(kvp.Key);
			}
		}
		order.Sort((a, b) => {
			// TODO sort by vote total, not vote count. there is a distinction because some ballots have a non-1 vote weight.
			int countA = tally.TryGetValue(a, out List<Ballot>? ballotsA) ? ballotsA.Count : 0;
			int countB = tally.TryGetValue(b, out List<Ballot>? ballotsB) ? ballotsB.Count : 0;
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

	static List<VoteBloc> CalculateBlocs(List<Candidate> sorted, VotesPerCandidate voteState, Dictionary<Candidate, float> candidateWeight) {
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

	/// <summary>calculate visualization model.</summary>
	/// <param name="out_visBlocs">where to append the visualization model.
	/// Each visualiation block explains which block moved from where to where.
	/// Every block exists at some index in a number line, and is the size of it's number of votes</param>
	/// <param name="voteStateHistory">the state of the votes at each step.</param>
	/// <param name="voteMigrationHistory">how the votes moved each state.</param>
	/// <param name="candidateWeight">the weight of each bloc, used to sort blocks of the same size (tie breaking)</param>
	static void IRV_calculateVisualizationModel(
		List<List<VoteBloc>> out_visBlocs,
		List<VotesPerCandidate> voteStateHistory,
		List<Dictionary<Candidate, VotesPerCandidate>> voteMigrationHistory, Candidate? candidateForExhausted) {
		List<VoteBloc> blocsThisState;
		List<VoteBloc>? blocsLastState = null;

		Dictionary<Candidate, float> weightsForThisVisualization = CalculateWeightByStateImportance(voteStateHistory);
		for (int stateIndex = 0; stateIndex < voteStateHistory.Count; ++stateIndex) {
			List<Candidate> sorted = IRV_OrderCandidatesForBlocs(voteStateHistory[stateIndex], weightsForThisVisualization, candidateForExhausted, true);
			blocsThisState = CalculateBlocs(sorted, voteStateHistory[stateIndex], weightsForThisVisualization);
			out_visBlocs.Add(blocsThisState);
			// if we can discover how the last vote state turned into this one
			if (blocsLastState != null) {
				VoteBloc.CalculateMigrations(blocsThisState, blocsLastState, candidateForExhausted, voteMigrationHistory[stateIndex - 1]);
			}
			blocsLastState = blocsThisState;
		}
	}

	static VoteCampaign CalculateSerializedVisualization(
		List<List<VoteBloc>> visBlocs,
		IList<Candidate> candidatesListing,
		//Dictionary<Candidate, Color> colorMap,
		int numBallotsTotal,
		string title) {
		// create a lookup table for unique IDs to reduce serialized data. only use IDs that are in this bloc visualization.
		Dictionary<Candidate, int> actuallyNeeded = new Dictionary<Candidate, int>();
		Dictionary<Candidate, int> idToIndexInUse = new Dictionary<Candidate, int>();
		//List<Color> colorListToSend = new List<Color>();
		//List<Candidate> candidatesInOrder = new List<Candidate>();
		actuallyNeeded[BasicExhaustedCandidate] = 1; // make sure IRV_EX is in the list (will be first if it is).
		IRV_convertVisualizationBlocIds(visBlocs, actuallyNeeded);
		for (int i = 0; i < candidatesListing.Count; ++i) {
			if (actuallyNeeded.ContainsKey(candidatesListing[i])) {
				idToIndexInUse[candidatesListing[i]] = i;
				//candidatesInOrder.Add(candidatesListing[i]);
				//colorListToSend.Add(candidatesListing[i].color);
			}
		}
		IRV_convertVisualizationBlocIds(visBlocs, idToIndexInUse);
		VoteCampaign sr = new VoteCampaign(title, numBallotsTotal, candidatesListing, visBlocs);
		return sr;
	}


	/// <summary>client-side visualization
	/// filter the visualization bloc object data. allows size reduction</summary>
	/// <param name="allVisBlocsStates">All vis blocs states.</param>
	/// <param name="out_conversionsMade">if not null, counts how many times any id was replaced</param>
	public static void IRV_convertVisualizationBlocIds(List<List<VoteBloc>> allVisBlocsStates,
		Dictionary<Candidate, int> out_conversionsMade) {
		for (int s = 0; s < allVisBlocsStates.Count; ++s) {
			List<VoteBloc> state = allVisBlocsStates[s];
			for (int b = 0; b < state.Count; ++b) {
				VoteBloc bloc = state[b];
				if (out_conversionsMade != null) {
					out_conversionsMade[bloc.candidate] = (out_conversionsMade.ContainsKey(bloc.candidate))
						? (out_conversionsMade[bloc.candidate] + 1) : 1;
				}
				List<VoteBloc.Migration>? nextList = bloc.migrations;
				if (nextList != null) {
					for (int n = 0; n < nextList.Count; ++n) {
						VoteBloc.Migration nextEntry = nextList[n];
						if (out_conversionsMade != null) {
							out_conversionsMade[nextEntry.newBoss] = (out_conversionsMade.ContainsKey(nextEntry.newBoss))
								? (out_conversionsMade[nextEntry.newBoss] + 1) : 1;
						}
					}
				}
			}
		}
	}

	private static IEnumerator<Response> ElectionCalculation(
		HashSet<Candidate> exhastedCandidates,
		List<Ballot> allBallots,
		Candidate candidateForExhaustedBallots,
		Candidate[] likelyOrder,
		float pluralityPercentage = 0.5f) {
		int iterations = 0;
		int processedElection = 0;
		List<ElectionResultsStepByStep> electionsToProcess = new List<ElectionResultsStepByStep>();
		ElectionResultsStepByStep result = new ElectionResultsStepByStep(candidateForExhaustedBallots);// TODO test this code with null as the exhausted candidate.
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
					Log.w("next...");
					result = electionsToProcess[processedElection];
				}
			}
			CountVoteExtremes(result.CurrentCandidateVoteTallies, out float leastVotes, out float mostVotes, candidateForExhaustedBallots);
			float futureVoteCountEstimate = 0;
			for (int i = 0; i < likelyOrder.Length; ++i) {
				if (!exhastedCandidates.Contains(likelyOrder[i])) {
					futureVoteCountEstimate = likelyOrder[i].totalVotesWeighted;
					break;
				}
			}
			futureVoteCountEstimate = Math.Min(futureVoteCountEstimate, voteCount);
			// before doing the standard remove-the-current-loser logic, clear out the extremely weak candidates that could never win.
			// eliminates the chance that statistical noise could remove an actual popular choice
			if (!TryGetExtremelyWeakCandidates(result.CurrentCandidateVoteTallies, futureVoteCountEstimate, pluralityPercentage, likelyOrder, out List<Candidate> losers)) {
				losers = GetLosers(result.CurrentCandidateVoteTallies, leastVotes, candidateForExhaustedBallots);
			}
			losers.Sort((a, b) => { return a.totalVotesWeighted != b.totalVotesWeighted ? a.totalVotesWeighted.CompareTo(b.totalVotesWeighted) : a.harmonicBordaCount.CompareTo(b.harmonicBordaCount); });
			if (losers.Count > 1) {
				Log.WriteLine($"tie for worst: {string.Join(", ", losers)}\n");
			}
			for (int i = 0; i < losers.Count; i++) {
				ElectionResultsStepByStep election;
				if (i == 0) {
					election = result;
				} else {
					election = new ElectionResultsStepByStep(result);
					election.note += "drop " + losers[i];
					electionsToProcess.Add(election);
				}
				election.DuplicateLatestState();
				election.ExhaustCandidate(election.CurrentCandidateVoteTallies, losers[i]);
				yield return Response.Processing(electionsToProcess);
			}
		} while (processedElection < electionsToProcess.Count);
		yield return Response.Success(electionsToProcess);
	}
	public static bool TryGetExtremelyWeakCandidates(VotesPerCandidate state, float voteCount, float pluralityPercentage, Candidate[] likelyOrder,
		[NotNullWhen(true)] out List<Candidate> losers) {
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
			if (extremelyWeakCandidates.Contains(likelyOrder[i])) {
				float cursedVoteCount = likelyOrder[i].totalVotesWeighted;
				losers.Add(likelyOrder[i]);
				while (--i >= 0 && extremelyWeakCandidates.Contains(likelyOrder[i]) && likelyOrder[i].totalVotesWeighted <= cursedVoteCount) {
					losers.Add(likelyOrder[i]);
				}
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

	// TODO rename SumTotalValidVotes
	public static float SumVoteValue(List<Ballot> votes) {
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
