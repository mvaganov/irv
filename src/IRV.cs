using src;
using src.Core;
using System.ComponentModel.Design;
namespace irv.src;

using VotesPerCandidate = Dictionary<IRV.Candidate, List<IRV.Ballot>>;

// TODO after the first rank is chosen, set up some kind of de-valuation algorithm for everyone who voted for the winner, so that otherwise disenfranchised voters would be more likely to have a say in representation beyond the first winner.
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
		public List<Migration> migrations;
		public VoteBloc(Candidate candidate, int start, int votes) {
			this.candidate = candidate; this.position = start; this.voteCount = votes; this.migrations = null;
		}

		public static void CalculateMigrations(List<VoteBloc> blocsThisState, List<VoteBloc> blocsLastState, Candidate? candidateForExhausted,
			Dictionary<Candidate, VotesPerCandidate> voteMigration) {
			for (int c = 0; c < blocsThisState.Count; ++c) {
				VoteBloc thisBloc = blocsThisState[c];
				Candidate thisBlocName = thisBloc.candidate;
				if (thisBlocName == candidateForExhausted) continue; // don't work on exhausted ballots
				int oldBlocIndex = GetBlocIndex(thisBlocName, blocsLastState);
				VoteBloc? lastBloc = null;
				int delta = thisBloc.voteCount;
				if (oldBlocIndex != -1) {
					lastBloc = blocsLastState[oldBlocIndex];
					if (lastBloc.candidate != thisBlocName) {
						throw new System.Exception("we got a naming and/or searching problem...");
					}
					delta = thisBloc.voteCount - delta;
				} else {
					//int lastNonExhaustedBloc = blocsLastState.Count-1;
					//if (blocsLastState[lastNonExhaustedBloc].candidate == candidateForExhausted) {
					//	--lastNonExhaustedBloc;
					//}
					//int location = lastNonExhaustedBloc == 0 ? 0 : blocsLastState[lastNonExhaustedBloc].position + blocsLastState[lastNonExhaustedBloc].voteCount;
					//lastBloc = new VoteBloc(thisBlocName, location, 0);

					//throw new System.Exception($"`{thisBlocName}` did not exist in previous state");
				}
				// if the size is the same, do an easy shift.
				if (delta == 0 && lastBloc != null) {
					lastBloc.migrations = new List<Migration>();
					lastBloc.migrations.Add(new Migration(thisBloc.candidate, lastBloc.voteCount, lastBloc.position, thisBloc.position));
				}
			}

			// the complex shifts were not calculated in the last forloop. but they were calculated in voteMigrationHistory
			Dictionary<Candidate, int> lastStateBlocAcct = new Dictionary<Candidate, int>(); // how much is being transferred from
			Dictionary<Candidate, int> thisStateBlocAcct = new Dictionary<Candidate, int>(); // how much is being transferred to
			foreach (var k in voteMigration) { // from
				VoteBloc lastBloc = blocsLastState[GetBlocIndex(k.Key, blocsLastState)];
				if (!lastStateBlocAcct.TryGetValue(lastBloc.candidate, out _)) {
					lastStateBlocAcct[lastBloc.candidate] = 0;
				}
				foreach (var n in voteMigration[k.Key]) { // to
					int blocIndex = GetBlocIndex(n.Key, blocsLastState);
					VoteBloc thisBloc = blocsThisState[blocIndex];
					if (!thisStateBlocAcct.TryGetValue(thisBloc.candidate, out _)) {
						VoteBloc? lastThisBloc = (blocIndex >= 0) ? blocsLastState[blocIndex] : null;
						if (lastThisBloc != null) {
							thisStateBlocAcct[thisBloc.candidate] = lastThisBloc.voteCount;
						} else {
							thisStateBlocAcct[thisBloc.candidate] = 0;
						}
					}
					List<Ballot> movingVotes = n.Value;
					if (lastBloc.migrations == null) lastBloc.migrations = new List<Migration>();
					lastBloc.migrations.Add(new Migration(thisBloc.candidate, movingVotes.Count,
						lastBloc.position + lastStateBlocAcct[lastBloc.candidate], thisBloc.position + thisStateBlocAcct[thisBloc.candidate]));
					lastStateBlocAcct[lastBloc.candidate] = lastStateBlocAcct[lastBloc.candidate] + movingVotes.Count;
					thisStateBlocAcct[thisBloc.candidate] = thisStateBlocAcct[thisBloc.candidate] + movingVotes.Count;
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

	// TODO if allBallots is very large, use a different algorithm. this is O(n^2). sort the ballots by voterID, then do binary-search?
	//private static int _indexOfVoter(List<Ballot> list, string voterID, int start, int end) {
	//	if (list != null) {
	//		for (int i = start; i < end; ++i) {
	//			if (list[i].id == voterID) {
	//				return i;
	//			}
	//		}
	//	}
	//	return -1;
	//}

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

	public static IEnumerator<Response> Calc(List<Ballot> allBallots, int maxWinnersCalculated = -1, float pluralityPercentage = 1) {
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



	//void IRV_calc(List<Ballot> allBallots, object? outputContainer, int maxWinnersCalculated = -1, WhatToDoWithResults? cb = null) {
	//	List<Ballot> originalBallots = allBallots; // reverence to source data. originalBallots may be marked up.
	//	allBallots = new List<Ballot>(originalBallots);

	//	// if anyone voted more than once...
	//	string votedMoreThanOnce = IRV_whoVotedMoreThanOnce(allBallots);
	//	if (votedMoreThanOnce != null) {
	//		//return irv_error(votedMoreThanOnce+" voted more than once."); // stop the whole process. one bad vote invalidates everything.
	//		Log.e(votedMoreThanOnce + " voted more than once.");
	//		return;
	//		// TODO do some logic to pick which vote is the correct one and remove the others?
	//	}
	//	List<Candidate> candidates = IRV_weightedVoteCalc(allBallots); // do a simple guess of who will win using a weighted vote algorithm
	//	List<Candidate> winners = new List<Candidate>(); // simple list of candidates who have won
	//	List<RunoffResult> results = new List<RunoffResult>(); // detailed results: {r:Number (rank),C:String||Array (winning candidates),v:Number (vote count),showme:String (how the results were developed visual)
	//	int place = 0; // keeps track of which rank is being calculated right now
	//	RunoffResult best = null; // the most recent best candidate(s).

	//	IRV_ensure_EX_code(candidates);
	//	IRV_ColorAssignment(candidates, IRV_colorList); // master color lookup table. will be rebuilt for each visualization
	//	candidates.Insert(0, BasicExhaustedCandidate);

	//	InstantRunoff? calcIteration = null;
	//	calcIteration = (WhatToDoWithResults calcCb) => {
	//		// start with the winners from the system. they can't win again.
	//		HashSet<Candidate> exhastedCandidates = new HashSet<Candidate>(winners);
	//		// how votes move during the instant-runoff-vote
	//		List<Dictionary<Candidate, List<Ballot>>> voteStateHistory = new List<Dictionary<Candidate, List<Ballot>>>();
	//		// array of rounds, each round has an array of shifts, each shift is an array with the voter ID and the choice.
	//		List<Dictionary<Candidate, Dictionary<Candidate, List<Ballot>>>> voteMigrationHistory =
	//			new List<Dictionary<Candidate, Dictionary<Candidate, List<Ballot>>>>();
	//		// do process!
	//		best = IRV_calcBestFrom(exhastedCandidates, allBallots, voteStateHistory, voteMigrationHistory, BasicExhaustedCandidate);

	//		//			Debug.Log(Stringify(best));
	//		//			Debug.Log(Stringify(voteStateHistory));
	//		//			Debug.Log(Stringify(voteMigrationHistory));

	//		if (best != null) {
	//			// array of voting blocs {candidate:id, indexRange:[#,#], color:"#XXXXXX", votes:[]}
	//			List<List<VoteBloc>> visBlocs = new List<List<VoteBloc>>();
	//			// create serializable easily expression of the Instant Run-off Vote
	//			IRV_calculateVisualizationModel(visBlocs, voteStateHistory, voteMigrationHistory);

	//			RunoffHistory serialized =
	//				IRV_serializeVisualizationBlocData(visBlocs, candidates, allBallots.Count, "rank" + place);

	//			// IRV_out(place+ "> "+best.winner);
	//			best.rank = place;
	//			best.showme = serialized;
	//			results.Add(best);
	//			if (best.winner.Count > 1) {
	//				place += best.winner.Count - 1; // the -1 is because place gets an automatic ++ in the main loop
	//				winners.AddRange(best.winner); //winners = winners.concat(best.winner);
	//			} else {
	//				winners.Add(best.winner[0]); //winners.push(best.winner);
	//			}
	//		}
	//		place++;
	//		if (best != null && (maxWinnersCalculated < 0 || place < maxWinnersCalculated)) {
	//			//NS.Timer.setTimeout(() => { calcIteration(calcCb); }, 1); // TODO set timer to 0
	//			throw new Exception("standard output not implemented");
	//		} else {
	//			if (calcCb != null) { calcCb(results); }
	//		}
	//	};
	//	if (cb == null) {
	//		cb = (List<RunoffResult> r) => {
	//			//IRV_standardOutput(r, outputContainer);
	//			throw new Exception("standard output not implemented");
	//		};
	//	}
	//	//NS.Timer.setTimeout(() => { calcIteration(cb); }, 1);
	//}

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
		public RunoffHistory serialized;

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

	/// <returns>[countOfVotes, winner(could be an array if tied)]</returns>
	/// <param name="exhastedCandidates">who is not allowed to be counted as a winner (because they're already ranked as winners, or they currently have no chance)</param>
	/// <param name="allBallots">all of those votes, as an array of ballots. It's a list of votes, where each vote is a voter [id], the ranked [vote] (another list). ballot:{id:String, vote:Array}</param>
	/// <param name="out_voteState">if not null, make it a list of vote states, where each state is "the name of the choice":"the votes for that choice"</param>
	/// <param name="out_voteMigrationHistory">if not null, make a list of voting rounds, where each round has a table of vote shifts, and each vote shift is a {[key] choice that was displaced and [value] a table of {[key] choices that votes moved to and [value] votes that made it there}}</param>
	static RunoffResult? IRV_calcBestFrom(HashSet<Candidate> exhastedCandidates, List<Ballot> allBallots, List<VotesPerCandidate> out_voteState,
		List<Dictionary<Candidate, VotesPerCandidate>> out_voteMigrationHistory, Candidate fullyExhausted) {
		bool doHtmlOutput = true;
		string htmlOutput = "";
		VotesPerCandidate tally = new VotesPerCandidate(); // the table of votes per candidate, do an initial count, to find out how things rank
		IRV_tallyVotes(allBallots, exhastedCandidates, tally, fullyExhausted);
		int iterations = 0;
		List<Candidate> winner = new List<Candidate>();
		int mostVotes = 0;
		int expectedMaxVoteCount = -1;
		if (out_voteState != null) {
			out_voteState.Add(CloneVotesPerCandidate(tally));
		}
		do {
			// find out how many total relevant votes there are (to determine majority)
			int sumVotes = 0;
			foreach (var k in tally) {
				if (k.Key == fullyExhausted) continue; // exhausted votes are no longer relevant for decision making
				sumVotes += tally[k.Key].Count; // TODO k.Value.Count
			}
			if (expectedMaxVoteCount >= 0 && sumVotes > expectedMaxVoteCount) {
				Log.e("votes added? ... was " + expectedMaxVoteCount + ", and is now " + sumVotes);
			}
			expectedMaxVoteCount = sumVotes;
			// if there are no votes to count, stop!
			if (sumVotes == 0) { break; }
			// if majority is set to sumVotes, the algorithm will exhaust all votes to determine total support
			// TODO make this a parameter.
			int majority = sumVotes; // (sumVotes / 2) +1; //

			// check if any unexhausted choice got a clear majority
			foreach (var k in tally) {
				if (k.Key == fullyExhausted) continue; // ignore exhausted ballots
				if (tally[k.Key].Count >= majority) { // TODO k.Value
					winner.Add(k.Key);
					mostVotes = tally[k.Key].Count; // TODO k.Value.Count
				}
			}

			// if there no clear winner, we about to drop some logic.
			if (winner.Count == 0) {
				// see who has the least votes
				int leastVotes = int.MaxValue; // how many votes the fewest vote candidate has
				mostVotes = 0;       // how many votes the leader has (used to check for tie)
				foreach (var k in tally) {
					if (k.Key == fullyExhausted) continue; // ignore exhausted ballots
					int len = tally[k.Key].Count; // TODO k.Value.Count
					if (len > 0) {
						if (len < leastVotes) { leastVotes = len; }
						if (len > mostVotes) { mostVotes = len; }
					}
				}
				// check for ties, which are a tricky thing in instant-runoff-voting. ties are when *every* candidate has the same number of votes
				List<Candidate> tie = null;
				if (mostVotes == leastVotes) { tie = new List<Candidate>(); }

				// find out which candidate gets exhausted this round
				// identify which ballots need to be recalculated
				List<Candidate> losers = new List<Candidate>(); // the list of losing candidates
				foreach (var k in tally) {
					if (k.Key == BasicExhaustedCandidate) continue; // the exhausted candidate is already lost, no need to use them in logic
					if (tally[k.Key].Count == leastVotes) { // TODO k.Value.Count
						if (tie != null) { tie.Add(k.Key); }
						losers.Add(k.Key);
						if (k.Key == null) { Log.e("why is null losing?... how is null a valid key?"); return null; }
					}
				}
				VotesPerCandidate displacedVotes = new VotesPerCandidate();
				if (losers.Count > 0) {
					losers = IRV_untie(losers, (Candidate c) => { return c.tieWeight; }, //tieBreakerData, 
						true);
					// disqualify candidate and displace the candidate's ballots
					for (int i = losers.Count - 1; i >= 0; --i) {
						Candidate losingCandidate = losers[i];
						exhastedCandidates.Add(losingCandidate);
						displacedVotes[losingCandidate] = tally[losingCandidate];
						tally[losingCandidate] = new List<Ballot>(); // clear the votes for this disqualified candidate
					}

					// if there was a tie, but not all of them were losers
					if (tie != null && tie.Count != losers.Count) {
						tie = null; // there is no tie, because ties can only exist with complete equality
					}
				}
				// in the rare case that all of the remaining candidates have the exact same score, even after weight calculations
				if (tie != null) {
					winner.AddRange(tie);
				} else {
					// if there is no tie, reassign votes.
					Dictionary<Candidate, VotesPerCandidate> votingRoundAdjust = null;
					if (out_voteMigrationHistory != null) {
						votingRoundAdjust = new Dictionary<Candidate, VotesPerCandidate>();
					}
					foreach (var k in displacedVotes) {
						// do standard logic to find out where to put displaced votes, who's current best choices have been disqualified
						VotesPerCandidate reassignedVotes = new VotesPerCandidate();
						IRV_tallyVotes(displacedVotes[k.Key], exhastedCandidates, reassignedVotes, fullyExhausted); // TODO k.Value

						if (doHtmlOutput) htmlOutput += ("moved " + displacedVotes[k.Key].Count + " votes from " + k.Key + " (" +//tieBreakerData[k.Key]
							k.Key.tieWeight + ") to: ");
						if (out_voteMigrationHistory != null) {
							votingRoundAdjust[k.Key] = reassignedVotes;
						}
						// move the displaced votes to their new tally location
						foreach (var newchoice in reassignedVotes) {
							if (doHtmlOutput) htmlOutput += (reassignedVotes[newchoice.Key].Count + ": " + newchoice.Key + ", ");
							if (!tally.ContainsKey(newchoice.Key) || tally[newchoice.Key] == null) {
								tally[newchoice.Key] = new List<Ballot>();
							}
							tally[newchoice.Key].AddRange(reassignedVotes[newchoice.Key]); // TODO newchoice.Value
						}
						if (doHtmlOutput) htmlOutput += ("\n");
					}
					if (out_voteMigrationHistory != null) {
						out_voteMigrationHistory.Add(votingRoundAdjust);
					}
				}
				if (out_voteState != null) {
					out_voteState.Add(IRV_cloneTableOfLists(tally));
				}
			} // if(!winner)
			iterations++;
			if (iterations > 150) { // TODO iterations > tieBreakerData.Count+1
				Log.e("too many iterations!");
				break;
			}
		} while (winner.Count == 0);
		if (doHtmlOutput) Log.Write(htmlOutput);
		if (winner.Count != 0) {
			return new RunoffResult(-1, winner, mostVotes, null);
		}
		return null;
	}

	private delegate float Scorer<TYPE>(TYPE toScore);
	/// <returns>The true lowest/highest from the tied list</returns>
	/// <param name="tied">the list of tied candidates</param>
	/// <param name="tieBreakerData">a table giving a score to compare for each tied member</param>
	/// <param name="wantLowest">if false, will return lowest-scoring-member(s) of the tie. otherwise, returns highest.</param>
	static List<Candidate> IRV_untie(List<Candidate> tied, //Dictionary<Candidate,float> tieBreakerData, 
		Scorer<Candidate> scoreCandidate,
		bool wantLowest) {
		List<Candidate> setApart = new List<Candidate>(); // who has broken the tie
		float dividingScore = scoreCandidate(tied[0]);//tieBreakerData[tied[0]];
																									// find out what the differentiating score is in the group
		for (int i = 1; i < tied.Count; ++i) {
			float score = scoreCandidate(tied[i]);//tieBreakerData[tied[i]];
																						// TODO XOR would simplify this if statement to: (wantLoser ^ tieBreakerData[tied[i]] > dividingScore)
			if ((wantLowest && score < dividingScore) || (!wantLowest && score > dividingScore)) {
				dividingScore = score;
			}
		}
		// once the superaltive score is known (lowest or highest, based on wantLowest), add the member(s) to the setApart list
		for (int i = 0; i < tied.Count; ++i) {
			if (scoreCandidate(tied[i])//tieBreakerData[tied[i]]
				== dividingScore) setApart.Add(tied[i]);
		}
		return setApart;
	}

	/// <returns>the index of the highest priority choice from list, with choices eliminated if they are in the exhastedCandidates list. -1 if no valid choices exist, identifying an exhausted ballot.</returns>
	/// <param name="ballot">Ballot.</param>
	/// <param name="exhastedCandidates">which choices are disqualified, prompting the next choice to be taken</param>
	static int IRV_getBestChoice(Ballot ballot, HashSet<Candidate> exhastedCandidates) {
		Candidate[] list = ballot.vote;
		if (list != null) {
			for (int i = 0; i < list.Length; ++i) {
				if (!exhastedCandidates.Contains(list[i])) {
					return i;
				}
				//if (exhastedCandidates.IndexOf(list[i]) < 0) {
				//	return i;
				//}
			}
		}
		return -1;
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
					}
					Console.WriteLine("------------------");
				}
			}
			Log.WriteLine($"{passed} {iter.Current.CommandState} {typeLabel}");
		}

	}

	//void MakeVisualization(List<RunoffResult> r) {
	//	for (int rank = 0; rank < 1/*r.Count*/; ++rank) {
	//		RunoffHistory d = r[rank].showme;
	//		GameObject rankObject = new GameObject("rank " + rank);
	//		rankObject.transform.position = new Vector3(0, 0, rank * 3);

	//		IRV_vis.VisualComponents vc = new IRV_vis.VisualComponents();
	//		IRV_vis.IRV_createVisualizationView(d.data, r[rank].showme.candidates, d.numVotes, 0, 0, r[rank].voteCount, 100, rankObject.transform, vc);

	//		for (int round = 0; round < d.data.Count; ++round) {
	//			for (int b = 0; b < d.data[round].Count; b++) {
	//				VoteBloc bloc = d.data[round][b];
	//				GameObject cube = Instantiate(this.basicBar);
	//				cube.name = bloc.candidate.name;//d.candidates [int.Parse (bloc.C)];
	//				cube.transform.SetParent(rankObject.transform);
	//				//					Debug.Log (bloc.C);
	//				cube.GetComponent<Renderer>().material.color = bloc.candidate.coloration;//d.colors[int.Parse(bloc.C)];//d.colorMap [bloc.C];
	//				int width = bloc.voteCount, start = bloc.startPosition;
	//				cube.transform.localScale = new Vector3(width, cube.transform.localScale.y, cube.transform.localScale.z);
	//				cube.transform.localPosition = new Vector3(start + (width / 2.0f), -round * 2, 0);
	//				TMPro.TextMeshPro tmpro = cube.GetComponentInChildren<TMPro.TextMeshPro>();
	//				if (tmpro != null) {
	//					//						if (b != d.data [round].Count - 1) {
	//					//							Destroy (tmpro.gameObject);
	//					//						} else {
	//					tmpro.text = cube.name;
	//					tmpro.transform.SetParent(null);
	//					tmpro.transform.localScale = Vector3.one;
	//					tmpro.transform.SetParent(cube.transform);
	//					float f = tmpro.transform.localPosition.z;
	//					tmpro.transform.localPosition = Vector3.zero + tmpro.transform.forward * f;
	//					//						}
	//				}
	//			}
	//		}
	//	}
	//}
}
