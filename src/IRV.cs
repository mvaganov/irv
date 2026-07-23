using src;
using src.Core;
using System.ComponentModel.Design;
namespace irv.src;

using VotesPerCandidate = Dictionary<IRV.Candidate, List<IRV.Ballot>>;

public class IRV {

	[System.Serializable]
	public class Candidate {
		public string name;
		public Color coloration = Color.clear;
		public float tieWeight = 0;
		public int totalVotes = 0;
		override public string ToString() { return name; }
		public Candidate(string name) { this.name = name; }
		public Candidate(string name, Color color) { this.name = name; this.coloration = color; }
		public Candidate(Candidate copy) { name = copy.name; coloration = copy.coloration; tieWeight = copy.tieWeight; totalVotes = copy.totalVotes; }
	}

	[System.Serializable]
	public class Ballot {
		[Tooltip("who is voting")]
		public string? id;
		[Tooltip("order being voted for")]
		public Candidate[]? vote;
		[Tooltip("how much this vote should count")]
		public float weight = 1;
		public override string? ToString() => vote != null ? string.Join(", ", Array.ConvertAll(vote, v => v.name)) : null;
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

	public class RunoffHistory {
		public string title;
		// TODO remove this variable?
		public IList<Candidate>? winner;
		/// how many votes total were recorded
		public int numVotes;
		/// <summary>who the candidates are.</summary>
		public List<Candidate> candidates;
		public string notes;
		/// <summary>data to describe graphical representation [IRV rank][candidate]</summary>
		public List<List<VoteBloc>> data;
		public RunoffHistory(int numVotes, List<Candidate> candidates, string notes, List<List<VoteBloc>> data) {
			this.numVotes = numVotes;
			this.candidates = candidates;
			this.notes = notes;
			this.data = data;
		}
	}

	public class RunoffResult {
		public int rank; // TODO remove?
		public IList<Candidate> winner;
		public int voteCount; // TODO remove?
		public RunoffHistory showme;
		public RunoffResult(int r, List<Candidate> C, int v, RunoffHistory showme) {
			this.rank = r; this.winner = C; this.voteCount = v; this.showme = showme;
		}
	}

	public class VoteBloc {
		public Candidate candidate;
		public int position;
		public int voteCount;
		public class Migration {
			public Candidate newBoss;
			public int voteCount, fromPosition, toPosition;
			public Migration(Candidate destination, int voteCount, int indexFrom, int indexTo) {
				this.newBoss = destination; this.voteCount = voteCount; this.fromPosition = indexFrom; this.toPosition = indexTo;
			}
		}
		/// the next blocs that these votes go into
		public List<Migration>? migrations;
		public VoteBloc(Candidate candidate, int start, int votes) {
			this.candidate = candidate; this.position = start; this.voteCount = votes;
		}
		public void PutMigrationsInOrder(Dictionary<Candidate, int> order) {
			if (migrations == null) return;
			int startIndex = int.MaxValue;
			for (int i = 0; i < migrations.Count; ++i) {
				if (migrations[i].fromPosition < startIndex) { startIndex = migrations[i].fromPosition; }
			}
			int cursor = startIndex;
			migrations.Sort((a, b) => {
				if (!order.TryGetValue(a.newBoss, out int aVal)) { aVal = order.Count; }
				if (!order.TryGetValue(b.newBoss, out int bVal)) { bVal = order.Count; }
				return aVal - bVal;
			});
			for (int i = 0; i < migrations.Count; ++i) {
				migrations[i].fromPosition = cursor;
				if (cursor > 100) {
					Log.w("why?");
				}
				cursor += migrations[i].voteCount;
			}
		}
		public static void CalculateMigrations(List<VoteBloc> blocsThisState, List<VoteBloc> blocsLastState, Candidate? candidateForExhausted,
			Dictionary<Candidate, VotesPerCandidate> voteMigration) {
			List<Candidate> properOrderOfCandidatesInState = new List<Candidate>();
			HashSet<Candidate> listed = new HashSet<Candidate>();
			for (int i = 0; i < blocsLastState.Count; ++i) {
				Candidate c = blocsLastState[i].candidate; properOrderOfCandidatesInState.Add(c); listed.Add(c);
			}
			for (int i = 0; i < blocsThisState.Count; ++i) {
				Candidate c = blocsLastState[i].candidate;
				if (!listed.Contains(c)) { properOrderOfCandidatesInState.Add(c); }
			}
			if (candidateForExhausted != null) {
				int index = properOrderOfCandidatesInState.IndexOf(candidateForExhausted);
				if (index >= 0) {
					properOrderOfCandidatesInState.RemoveAt(index);
					properOrderOfCandidatesInState.Add(candidateForExhausted);
				}
			}
			Dictionary<Candidate, int> indexOfVotesMovedFromLastBloc = new Dictionary<Candidate, int>();
			Dictionary<Candidate, int> indexOfVotesMovedToThisBloc = new Dictionary<Candidate, int>();
			for (int i = 0; i < properOrderOfCandidatesInState.Count; ++i) {
				Candidate candidate = properOrderOfCandidatesInState[i];
				if (candidate == null) continue;
				int indexOfBlockLastState = GetBlocIndex(candidate, blocsLastState);
				if (indexOfBlockLastState < 0) continue;
				int indexOfBlockThisState = GetBlocIndex(candidate, blocsThisState);
				VoteBloc lastBloc = blocsLastState[indexOfBlockLastState];
				VoteBloc? thisBloc = indexOfBlockThisState >= 0 ? blocsThisState[indexOfBlockThisState] : null;
				if (thisBloc != null && thisBloc.voteCount > 0) {
					if (lastBloc.voteCount > thisBloc.voteCount) {
						throw new Exception("strange situation where votes are partially lost.");
					}
					if (lastBloc.migrations == null) lastBloc.migrations = new List<Migration>();
					lastBloc.migrations.Add(new Migration(candidate, thisBloc.voteCount, lastBloc.position, thisBloc.position));
					continue;
				}
				if (!indexOfVotesMovedFromLastBloc.TryGetValue(candidate, out _)) {
					indexOfVotesMovedFromLastBloc[candidate] = lastBloc.position;
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
					int blocIndexTargetThisState = GetBlocIndex(whoGetsMeNow, blocsThisState);
					if (blocIndexTargetThisState < 0) {
						throw new Exception($"`{whoGetsMeNow}` is not in the present state?");
					}
					VoteBloc nextBloc = blocsThisState[blocIndexTargetThisState];
					if (!indexOfVotesMovedToThisBloc.TryGetValue(nextBloc.candidate, out _)) {
						int blocIndexLastState = GetBlocIndex(whoGetsMeNow, blocsLastState);
						VoteBloc? nextBlocLastState = (blocIndexLastState >= 0) ? blocsLastState[blocIndexLastState] : null;
						if (nextBlocLastState != null) {
							indexOfVotesMovedToThisBloc[nextBloc.candidate] = nextBloc.position + nextBlocLastState.voteCount;
						} else {
							indexOfVotesMovedToThisBloc[nextBloc.candidate] = nextBloc.position;
						}
					}
					if (lastBloc.migrations == null) lastBloc.migrations = new List<Migration>();
					lastBloc.migrations.Add(new Migration(nextBloc.candidate, movingVotes.Count,
						indexOfVotesMovedFromLastBloc[lastBloc.candidate], indexOfVotesMovedToThisBloc[nextBloc.candidate]));
					indexOfVotesMovedFromLastBloc[lastBloc.candidate] = indexOfVotesMovedFromLastBloc[lastBloc.candidate] + movingVotes.Count;
					indexOfVotesMovedToThisBloc[nextBloc.candidate] = indexOfVotesMovedToThisBloc[nextBloc.candidate] + movingVotes.Count;
					votesMoved += movingVotes.Count;
				}
			}
		}
	}

	/// <summary>exhausted ballot token: where votes go when none of their candidates survived the runoff.
	/// regenerated to ensure no collision with candidate names</summary>
	public static readonly Candidate BasicExhaustedCandidate = new Candidate("`", new Color(.875f, .875f, .875f));
	public List<Color> IRV_colorList = new List<Color>(s_IRV_colorList);
	private static Color[] s_IRV_colorList = new Color[]{
		Color.red, Color.green, Color.blue, //"888",
		Color.yellow, Color.cyan, Color.magenta, //"222",
		new Color(.5f,0,0), new Color(.75f,1,.75f), new Color(0,0,.5f), //"666", 
		new Color(1,1,.75f),new Color(0,.5f,.5f),new Color(1,.75f,1),
		new Color(.5f,.5f,0),new Color(.75f,1,1),new Color(.5f,0,.5f),
		new Color(1,.75f,.75f),new Color(0,.5f,0),new Color(.75f,.75f,1),
		new Color(1,.5f,0),new Color(0,1,.5f),new Color(.5f,0,1),
		new Color(1,0,.5f),new Color(.5f,1,0),new Color(0,.5f,1),
		new Color(.25f,.5f,0),new Color(0,.25f,.5f),new Color(.5f,0,.25f)
	};

	static string replaceAt(string s, int i, char c) {
		return s.Substring(0, i) + c + s.Substring(i + 1);
	}
	static string nextCharAtIndex(string s, int i) {
		return replaceAt(s, i, (char)(s[i] + 1));
	}

	private delegate bool ReturnTrueToContinue(string test);
	/// <summary>brute-force run through every string</summary>
	/// <param name="returnsTrueToContinue">the function that checks each string. keep returning true to keep the loop going.</param>
	/// <param name="minchar">Minchar.</param>
	/// <param name="maxchar">Maxchar.</param>
	static void tryEveryString(ReturnTrueToContinue returnsTrueToContinue, char minchar = (char)33, char maxchar = (char)126) {
		bool collision;
		string test = minchar.ToString();
		do {
			collision = returnsTrueToContinue(test);
			if (collision) {
				int index = 0;
				char v = (char)minchar;
				do {
					test = nextCharAtIndex(test, index);
					v = test[index];
					if (v >= maxchar) {
						test = replaceAt(test, index, ' ');
						index++;
						while (index >= test.Length) { test += ' '; }
					}
				} while (v >= maxchar);
			}
		} while (collision);
	}
	/// <summary>make sure the EX code is unique amoung the list of candidates, by brute force if necessary</summary>
	/// <param name="listOfCandidates">List of candidates.</param>
	void IRV_ensure_EX_code(List<Candidate> listOfCandidates) {
		tryEveryString((str) => {
			BasicExhaustedCandidate.name = str;
			// if this string is in the listOfCandidates, return true, to keep looking for a new string.
			return listOfCandidates.FindIndex((Candidate c) => { return c.name == str; }) >= 0;
		});
	}
	static Candidate GenerateExhaustedCandidatePlaceholder(List<Candidate> listOfCandidates) {
		Candidate IRV_EX = new Candidate(BasicExhaustedCandidate);
		tryEveryString((str) => {
			IRV_EX.name = str;
			// if this string is in the listOfCandidates, return true, to keep looking for a new string.
			return listOfCandidates.FindIndex((Candidate c) => { return c.name == str; }) >= 0;
		});
		return IRV_EX;
	}

	static float distanceBetweenColors(Color a, Color b) {
		float R = b.r - a.r, G = b.g - a.g, B = b.b - a.b;
		float magnitude = (float)Math.Sqrt(R * R + G * G + B * B);
		return magnitude;
	}

	/// <summary>Generates a default color for each candidate, if needed.</summary>
	/// <param name="listing">out_Listing. the list of Candidates. If the Candidate has no coloration, it will have one after this method</param>
	static void IRV_ColorAssignment(List<Candidate> out_listing, List<Color> IRV_colorList) {
		// remove auto-colors that are too close to the existing candidates
		for (int i = 0; i < out_listing.Count; ++i) {
			if (out_listing[i].coloration != Color.clear) {
				var mostSimilarColors = IRV_colorList.OrderBy(c => distanceBetweenColors(c, out_listing[i].coloration));
				Color co = mostSimilarColors.First();
				float dist = distanceBetweenColors(co, out_listing[i].coloration);
				if (dist < 32) {
					IRV_colorList.Remove(co);
				}
			}
		}
		// assign colors to candidates without coloration
		int colorindex = 0;
		int startingIndex = 0;
		for (int i = startingIndex; i < out_listing.Count; ++i) {
			Candidate k = out_listing[i];
			if (k.coloration == Color.clear) {
				k.coloration = IRV_colorList[(colorindex++) % IRV_colorList.Count];
			}
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

	/// <returns>The id of the voter who voted more than once.</returns>
	/// <param name="allBallots">All ballots.</param>
	protected static string? IRV_whoVotedMoreThanOnce(List<Ballot> allBallots) {
		HashSet<string> voterId = new HashSet<string>();
		for (int i = 0; i < allBallots.Count; ++i) {
			string id = allBallots[i].id;
			if (voterId.Contains(id)) { return id; }
			voterId.Add(id);
			//if (_indexOfVoter(allBallots, allBallots[i].id, i + 1, allBallots.Count) >= 0) {
			//	return allBallots[i].id;
			//}
		}
		return null;
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
	private static int GetBlocIndex(Candidate candidateName, List<VoteBloc> blocList) {
		for (int i = 0; i < blocList.Count; ++i) {
			if (blocList[i].candidate == candidateName) { return i; }
		}
		return -1;
	}

	public static Dictionary<Candidate,float> CalculateWeightByStateImportance(List<VotesPerCandidate> voteStateHistory) {
		Dictionary<Candidate, float> weightsForThisVisualization = new Dictionary<Candidate, float>();
		for (int s = 0; s < voteStateHistory.Count; ++s) {
			VotesPerCandidate state = voteStateHistory[s];
			foreach (KeyValuePair<Candidate, List<Ballot>> c in state) {
				float val;
				if (weightsForThisVisualization.TryGetValue(c.Key, out val)) {
					val += c.Value.Count;
				} else {
					val = c.Value.Count;
				}
				weightsForThisVisualization[c.Key] = val;
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

	static RunoffHistory IRV_serializeVisualizationBlocData(
		List<List<VoteBloc>> visBlocs,
		List<Candidate> candidatesListing,
		//Dictionary<Candidate, Color> colorMap,
		int voteCount,
		string title) {
		// create a lookup table for unique IDs to reduce serialized data. only use IDs that are in this bloc visualization.
		Dictionary<Candidate, int> actuallyNeeded = new Dictionary<Candidate, int>();
		Dictionary<Candidate, int> idToIndexInUse = new Dictionary<Candidate, int>();
		List<Color> colorListToSend = new List<Color>();
		List<Candidate> indexToIdToSend = new List<Candidate>();
		actuallyNeeded[BasicExhaustedCandidate] = 1; // make sure IRV_EX is in the list (will be first if it is).
		IRV_convertVisualizationBlocIds(visBlocs, null, actuallyNeeded);
		for (int i = 0; i < candidatesListing.Count; ++i) {
			if (actuallyNeeded.ContainsKey(candidatesListing[i])) {
				idToIndexInUse[candidatesListing[i]] = indexToIdToSend.Count;
				indexToIdToSend.Add(candidatesListing[i]);
				// FIXME make sure that hex codes are printed here...
				colorListToSend.Add(candidatesListing[i].coloration);
			}
		}
		IRV_convertVisualizationBlocIds(visBlocs, idToIndexInUse);
		RunoffHistory sr = new RunoffHistory(voteCount, indexToIdToSend, title, visBlocs);
		return sr;
	}


	/// <summary>client-side visualization
	/// filter the visualization bloc object data. allows size reduction</summary>
	/// <param name="allVisBlocsStates">All vis blocs states.</param>
	/// <param name="conversionTable">if not null, used to replace ids with an alternate value</param>
	/// <param name="out_conversionsMade">if not null, counts how many times any id was replaced</param>
	public static void IRV_convertVisualizationBlocIds(List<List<VoteBloc>> allVisBlocsStates,
		Dictionary<Candidate, int> conversionTable, Dictionary<Candidate, int> out_conversionsMade = null) {
		for (int s = 0; s < allVisBlocsStates.Count; ++s) {
			List<VoteBloc> state = allVisBlocsStates[s];
			for (int b = 0; b < state.Count; ++b) {
				VoteBloc bloc = state[b];
				if (out_conversionsMade != null) {
					out_conversionsMade[bloc.candidate] = (out_conversionsMade.ContainsKey(bloc.candidate))
						? (out_conversionsMade[bloc.candidate] + 1) : 1;
				}
				List<VoteBloc.Migration> nextList = bloc.migrations;
				if (nextList != null) {
					for (int n = 0; n < nextList.Count; ++n) {
						VoteBloc.Migration nextEntry = nextList[n];
						if (out_conversionsMade != null) {
							out_conversionsMade[nextEntry.newBoss] = (out_conversionsMade.ContainsKey(nextEntry.newBoss))
								? (out_conversionsMade[nextEntry.newBoss] + 1) : 1;
						}
						//						if(conversionTable != null) {nextEntry.D = conversionTable[nextEntry.D].ToString();}
					}
				}
			}
		}
	}

	//void IRV_standardOutput(List<RunoffResult> results, object? graphicOutput = null) {
	//	for (int i = 0; i < results.Count; ++i) {
	//		IRV_vis.IRV_deserializeVisualizationBlocData(results[i].showme, 0, 0, 500, -1, graphicOutput);
	//	}
	//}

	/// <returns>list of Candidates by weight, which is used for tie-breaking when multiple candidates are about to be removed</returns>
	static List<Candidate> IRV_weightedVoteCalc(List<Ballot> ballots) {
		// calculate a weighted score, and total-vote-count, which are simpler algorithms than Instant Runoff Voting
		HashSet<Candidate> completeSet = new HashSet<Candidate>();
		for (int v = 0; v < ballots.Count; ++v) {
			Candidate[] voterRanking = ballots[v].vote;
			for (int i = 0; i < voterRanking.Length; ++i) {
				Candidate candidate = voterRanking[i];
				completeSet.Add(candidate);
				candidate.totalVotes++;
				// first-pick adds 1 point. 2nd pick adds 1/2 a point. 3rd pick 1/3, 4th pick 1/4, 5 pick 1/5, ...
				candidate.tieWeight += 1 / (i + 1.0f);
			}
		}
		List<Candidate> candidateList = completeSet.ToList();
		candidateList.Sort((a, b) => {
			return (int)((b.tieWeight - a.tieWeight) * 1024);
		});
		return candidateList;
	}

	private delegate void WhatToDoWithResults(List<RunoffResult> results);
	private delegate void InstantRunoff(WhatToDoWithResults cb);

	//public string Stringify(object obj, string indentation = "    ") {
	//	return OMU.Serializer.Stringify(obj, indentation);
	//}

	public static IEnumerator<Response> Calc(List<Ballot> allBallots, int maxWinnersCalculated = -1, float pluralityPercentage = 0.5f) {
		List<Ballot> originalBallots = allBallots; // reverence to source data. originalBallots may be marked up.
		allBallots = new List<Ballot>(originalBallots);
		// if anyone voted more than once...
		string votedMoreThanOnce = IRV_whoVotedMoreThanOnce(allBallots);
		if (votedMoreThanOnce != null) {
			// TODO do some logic to pick which vote is the correct one and remove the others?
			yield return Response.Error($"`{votedMoreThanOnce}` voted more than once.");
			yield break;
		}
		List<Candidate> candidates = IRV_weightedVoteCalc(allBallots); // do a simple guess of who will win using a weighted vote algorithm
		List<Candidate> winners = new List<Candidate>(); // simple list of candidates who have won

		Candidate candidateForExhaustedBallots = GenerateExhaustedCandidatePlaceholder(candidates);
		IRV_ColorAssignment(candidates, new List<Color>(s_IRV_colorList));
		candidates.Insert(0, candidateForExhaustedBallots);

		List<RankedChoiceElectionResultsStepByStep>? elections = null;
		IEnumerator<Response> calcIteration() {
			//List<RunoffHistory> results = new List<RunoffHistory>(); // detailed results: {r:Number (rank),C:String||Array (winning candidates),v:Number (vote count),showme:String (how the results were developed visual)
			for (int place = 0; maxWinnersCalculated < 0 || place < maxWinnersCalculated; ++place) {
				//int place = 0; // keeps track of which rank is being calculated right now
				// start with the winners from the system. they can't win again.
				HashSet<Candidate> exhastedCandidates = new HashSet<Candidate>(winners);
				// how votes move during the instant-runoff-vote
				//List<VotesPerCandidate> voteStateHistory = new List<VotesPerCandidate>();
				// array of rounds, each round has an array of shifts, each shift is an array with the voter ID and the choice.
				//List<Dictionary<Candidate, VotesPerCandidate>> voteMigrationHistory;

				// do process!
				IEnumerator<Response> iter = RankedVoteProcessing(exhastedCandidates, allBallots, candidateForExhaustedBallots, pluralityPercentage);
				while (iter.MoveNext()) {
					elections = iter.Current.Message as List<RankedChoiceElectionResultsStepByStep>;
					yield return iter.Current;
				}
				// TODO check if the elections have a different outcome. remove subsequent elections with the same outcome
				// TODO if there are multiple outcomes, title each with the notes from the different one.
				Log.WriteLine(elections?.Count ?? 0);
				if (elections != null && elections.Count == 0) {
					break;
				}
				//best = IRV_calcBestFrom(exhastedCandidates, allBallots, voteStateHistory, voteMigrationHistory, exhaustedCandidate);

				//			Debug.Log(Stringify(best));
				//			Debug.Log(Stringify(voteStateHistory));
				//			Debug.Log(Stringify(voteMigrationHistory));
				for (int i = 0; elections != null && i < elections.Count; ++i) {
					Log.d($"calculating visuals for election[{i}]");
					//if (best != null) {
					// array of voting blocs {candidate:id, indexRange:[#,#], color:"#XXXXXX", votes:[]}
					List<List<VoteBloc>> visBlocs = new List<List<VoteBloc>>();
					// create serializable easily expression of the Instant Run-off Vote
					List<VotesPerCandidate> voteStateHistory = elections[i].out_voteState;
					List<Dictionary<Candidate, VotesPerCandidate>> voteMigrationHistory = elections[i].out_voteMigrationHistory;
					IRV_calculateVisualizationModel(visBlocs, voteStateHistory, voteMigrationHistory, candidateForExhaustedBallots);

					RunoffHistory serialized =
						IRV_serializeVisualizationBlocData(visBlocs, candidates, allBallots.Count, "rank" + place);

					// IRV_out(place+ "> "+best.winner);
					serialized.title = $"rank {place}";
					serialized.winner = elections[i].winner;
					elections[i].serialized = serialized;
					//best.rank = place;
					//best.showme = serialized;
					//results.Add(serialized);
					if (serialized.winner != null) {
						//place += 1;// serialized.winner.Count - 1; // the -1 is because place gets an automatic ++ in the main loop
						winners.AddRange(serialized.winner); //winners = winners.concat(best.winner);
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

	/// <returns>a clone of the given table of lists. used to store logs of vote state TODO rename cloneVoteCollection</returns>
	static VotesPerCandidate IRV_cloneTableOfLists(VotesPerCandidate tally) {
		VotesPerCandidate cloned = new VotesPerCandidate();
		foreach (var k in tally) {
			cloned[k.Key] = new List<Ballot>(tally[k.Key]); // TODO k.Value
		}
		return cloned;
	}

	static VotesPerCandidate CloneVotesPerCandidate(VotesPerCandidate tally) {
		VotesPerCandidate cloned = new VotesPerCandidate();
		foreach (var k in tally) {
			cloned[k.Key] = new List<Ballot>(k.Value);
		}
		return cloned;
	}

	// VotesPerCandidate Dictionary<Candidate, List<Ballot>>

	class RankedChoiceElectionResultsStepByStep {
		public HashSet<Candidate> exhaustedCandidates;
		public List<VotesPerCandidate> out_voteState;
		public List<Dictionary<Candidate, VotesPerCandidate>> out_voteMigrationHistory;
		public List<Ballot> exhaustedBallots = new List<Ballot>();
		public IList<Candidate>? winner;
		public RunoffHistory? serialized;
		public string? note;
		public VotesPerCandidate LatestState => out_voteState[out_voteState.Count - 1];
		public RankedChoiceElectionResultsStepByStep() {
			exhaustedCandidates = new HashSet<Candidate>();
			out_voteState = new List<VotesPerCandidate>();
			out_voteMigrationHistory = new List<Dictionary<Candidate, VotesPerCandidate>>();
			exhaustedBallots = new List<Ballot>();
		}
		public RankedChoiceElectionResultsStepByStep(RankedChoiceElectionResultsStepByStep other) {
			exhaustedCandidates = new HashSet<Candidate>(other.exhaustedCandidates);
			out_voteState = new List<VotesPerCandidate>(other.out_voteState);
			out_voteMigrationHistory = new List<Dictionary<Candidate, VotesPerCandidate>>(other.out_voteMigrationHistory);
			exhaustedBallots = new List<Ballot>(other.exhaustedBallots);
			if (other.winner != null) { winner = new List<Candidate>(other.winner); }
			if (other.note != null) { note = other.note; }
		}
		public void CalculateNextState(List<Ballot> allBallots, Candidate? candidateForExhausted) {
			VotesPerCandidate tally = new VotesPerCandidate();
			IRV_tallyVotes(allBallots, exhaustedCandidates, tally, candidateForExhausted);
			out_voteState.Add(tally);
		}
		public void DuplicateLatestState() {
			out_voteState.Add(CloneVotesPerCandidate(LatestState));
		}
		public bool CalculateWinner(Candidate? candidateForExhausted, float pluralityPercentage) {
			winner = MajorityCandidates(LatestState, candidateForExhausted, pluralityPercentage);
			bool hasWinner = winner?.Count > 0;
			if (hasWinner && winner != null) {
				Log.d(string.Join(", ", winner));
			}
			return hasWinner;
		}
		public List<Ballot> ExhaustCandidate(VotesPerCandidate state, Candidate candidate, Candidate? candidateForExhausted) {
			exhaustedCandidates.Add(candidate);
			List<Ballot> exhaustedBallots = new List<Ballot>();
			if (!state.TryGetValue(candidate, out List<Ballot>? votes)) {
				throw new Exception($"`{candidate.name}` missing from current state?");
			}
			Log.w($"exhausting {candidate} {votes.Count}");
			state.Remove(candidate);
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
		public bool IsExhausted(Candidate? candidateForExhausted) {
			int countsAsExhausted = candidateForExhausted != null && LatestState.ContainsKey(candidateForExhausted) ? 2 : 1;
			return LatestState.Count <= countsAsExhausted;
		}
	}

	static IEnumerator<Response> RankedVoteProcessing(HashSet<Candidate> exhastedCandidates, List<Ballot> allBallots, Candidate candidateForExhaustedBallots, float pluralityPercentage = 0.5f) {
		int iterations = 0;
		int processedElection = 0;
		List<RankedChoiceElectionResultsStepByStep> electionsToProcess = new List<RankedChoiceElectionResultsStepByStep>();
		RankedChoiceElectionResultsStepByStep r = new RankedChoiceElectionResultsStepByStep();
		foreach(Candidate c in exhastedCandidates) r.exhaustedCandidates.Add(c);
		r.CalculateNextState(allBallots, candidateForExhaustedBallots);
		if (r.IsExhausted(candidateForExhaustedBallots)) {
			yield return Response.Success(electionsToProcess);
			yield break;
		}
		electionsToProcess.Add(r);
		do {
			if (++iterations > 10000) {
				throw new Exception("too many iterations");
			}
			if (r.CalculateWinner(candidateForExhaustedBallots, pluralityPercentage) || r.IsExhausted(candidateForExhaustedBallots)) {
				yield return Response.Processing(electionsToProcess);
				if (++processedElection >= electionsToProcess.Count) {
					break;
				} else {
					Log.w("next...");
					r = electionsToProcess[processedElection];
				}
			}
			CountVoteExtremes(r.LatestState, out int leastVotes, out int mostVotes, candidateForExhaustedBallots);
			List<Candidate> losers = GetLosers(r.LatestState, leastVotes, candidateForExhaustedBallots);
			losers.Sort((a, b) => { return a.totalVotes != b.totalVotes ? a.totalVotes.CompareTo(b.totalVotes) : a.tieWeight.CompareTo(b.tieWeight); });
			if (losers.Count > 1) {
				Log.WriteLine($"tie for worst: {string.Join(", ", losers)}\n");
			}
			for (int i = 0; i < losers.Count; i++) {
				RankedChoiceElectionResultsStepByStep election;
				if (i == 0) {
					election = r;
				} else {
					election = new RankedChoiceElectionResultsStepByStep(r);
					election.note += "drop " + losers[i];
					//election.out_voteState.RemoveAt(election.out_voteState.Count - 1);
					electionsToProcess.Add(election);
				}
				election.DuplicateLatestState();
				election.ExhaustCandidate(election.LatestState, losers[i], candidateForExhaustedBallots);
				yield return Response.Processing(electionsToProcess);
			}
		} while (processedElection < electionsToProcess.Count);
		yield return Response.Success(electionsToProcess);
	}
	public static List<Candidate> GetLosers(VotesPerCandidate tally, int leastVotes, Candidate fullyExhausted) {
		List<Candidate> losers = new List<Candidate>();
		foreach (var k in tally) {
			if (k.Key == fullyExhausted) continue;
			if (k.Value.Count <= leastVotes) {
				losers.Add(k.Key);
				if (k.Key == null) { Log.e("why is null losing?... how is null a valid key?"); return null; }
			}
		}
		return losers;
	}
	public static void CountVoteExtremes(VotesPerCandidate tally, out int leastVotes, out int mostVotes, Candidate fullyExhausted) {
		leastVotes = int.MaxValue;
		mostVotes = 0;
		foreach (var k in tally) {
			if (k.Key == fullyExhausted) continue;
			int len = k.Value.Count;
			if (len > 0) {
				if (len < leastVotes) { leastVotes = len; }
				if (len > mostVotes) { mostVotes = len; }
			}
		}
	}

	public static int SumUnexhaustedVotes(VotesPerCandidate tally, Candidate? fullyExhausted) {
		int sumVotes = 0;
		foreach (var k in tally) {
			if (k.Key == fullyExhausted) continue;
			sumVotes += k.Value.Count;
		}
		return sumVotes;
	}

	public static IList<Candidate>? MajorityCandidates(VotesPerCandidate tally, Candidate? fullyExhausted, float pluralityPercentage = 0.5f) {
		int voteCount = SumUnexhaustedVotes(tally, fullyExhausted);
		if (voteCount == 0) { return null; }
		List<Candidate>? winners = null;
		int majority = (int)(voteCount * pluralityPercentage);
		foreach (var k in tally) {
			if (k.Key == fullyExhausted) continue;
			if (k.Value.Count >= majority) {
				if (winners == null) winners = new List<Candidate>();
				winners.Add(k.Key);
			}
		}
		return winners;
	}

	/// <param name="ballots">a list of ballots. A ballot is a {id:"unique voter id", vote:["list","of","candidates","(order","matters)"]}.</param>
	/// <param name="exhastedCandidates">list of which candidates should not count (move to the next choice in the vote's ranked list)</param>
	/// <param name="out_tally">a table of all of the votes, seperated by vote winner. {<candidate name>: [list of ballots]}</param>
	static void IRV_tallyVotes(List<Ballot> ballots, HashSet<Candidate> exhastedCandidates, VotesPerCandidate out_tally, Candidate? fullyExhausted) {
		for (int i = 0; i < ballots.Count; ++i) {
			Ballot b = ballots[i];
			Candidate? bestChoice = b.GetBestChoice(exhastedCandidates);
			if (bestChoice == null) bestChoice = fullyExhausted;
			if (bestChoice == null) continue;
			List<Ballot>? supportForChoice = out_tally.ContainsKey(bestChoice) ? out_tally[bestChoice] : null;
			if (supportForChoice == null) {
				out_tally[bestChoice] = supportForChoice = new List<Ballot>();
			}
			supportForChoice.Add(b);
		}
	}
	// Use this for initialization
	public static void Start() {
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
					pick = (int)(Rand.Number * Rand.Number * (candidates.Count));
				} while (System.Array.IndexOf(ranked, candidates[pick]) >= 0);
				ranked[r] = candidates[pick];
			}
			Ballot v = new Ballot();
			v.id = "rand" + i.ToString();
			v.vote = ranked;
			votes.Add(v);
		}
		for(int i = 0; i < votes.Count; ++i) {
			Log.WriteLine(votes[i]);
		}
		//IRV_calc(votes, transform, -1, (List<RunoffResult> results) => {
		//	MakeVisualization(results);
		//});
		IEnumerator<Response> iter = IRV.Calc(votes);
		uint last = Rand.Timestamp;
		while (iter.MoveNext()) {
			uint now = Rand.Timestamp;
			int passed = (int)(now - last);
			object? messsge = iter.Current.Message;
			string typeLabel = messsge?.GetType().Name ?? "null";
			List<RankedChoiceElectionResultsStepByStep>? allData = messsge as List<RankedChoiceElectionResultsStepByStep>;
			if (allData != null) {
				typeLabel += $"[{allData.Count}]";
				for(int e = 0; e < allData.Count; ++e) {
					RankedChoiceElectionResultsStepByStep election = allData[e];
					if (election.serialized == null) continue;
					Log.WriteLine(election.serialized.notes);
					List<List<VoteBloc>> allStates = election.serialized.data;
					for (int i = 0; i < allStates.Count; ++i) {
						List<VoteBloc> state = allStates[i];
						int index = 0;
						// draw state
						for (int b = 0; b < state.Count; ++b) {
							if (index != state[b].position) {
								Log.Write("_");
							}
							for (int w = 0; w < state[b].voteCount; ++w) {
								if (w < state[b].candidate.name.Length) {
									Console.Write(state[b].candidate.name[w]);
								} else {
									Console.Write((char)('0' + b));
								}
								++index;
							}
						}
						Console.WriteLine();
						// draw moves
						char[] bufferFrom = new char[index];
						char[] bufferTo = new char[index];
						for (int b = 0; b < index; ++b) bufferFrom[b] = bufferTo[b] = ' ';
						for (int b = 0; b < state.Count; ++b) {
							VoteBloc bloc = state[b];
							if (bloc.migrations == null) continue;
							for(int m = 0; m < bloc.migrations.Count; ++m) {
								VoteBloc.Migration migration = bloc.migrations[m];
								if (migration.newBoss == bloc.candidate) continue;
								for (int j = 0; j < migration.voteCount; ++j) {
									bufferFrom[migration.fromPosition+j] = migration.newBoss.name[0];
									bufferTo[migration.toPosition+j] = migration.newBoss.name[0];
								}
							}
						}
						Console.WriteLine(new string(bufferFrom));
						Console.WriteLine(new string(bufferTo));
					}
					Console.WriteLine("------------------");
				}
			}
			Log.WriteLine($"{passed} {iter.Current.CommandState} {typeLabel}");
		}

	}
}
