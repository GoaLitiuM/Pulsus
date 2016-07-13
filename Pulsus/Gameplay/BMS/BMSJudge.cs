using System;

namespace Pulsus.Gameplay
{
	public class BMSJudge : Judge
	{
		bool useLR2timing = true;

		// truncate off sub-millisecond values from judge timing
		public bool truncateTiming = false;

		public static readonly double[] timingWindowDefault =
		{
			//PGreat, Great, Good, Bad, Poor (miss), Early Miss
			.018, .040, .100, .200, .200, 1.000,	// same as LR2Normal
		};

		public static readonly double[][] timingWindowLR2 =
		{
			// source for timing windows used in LR2:
			// http://hitkey.nekokan.dyndns.info/diary1501.php#D150119

			new double[] { .008, .024, .040, .200, .200, 1.000 },	// LR2VeryHard
			new double[] { .015, .030, .060, .200, .200, 1.000 },	// LR2Hard
			new double[] { .018, .040, .100, .200, .200, 1.000 },	// LR2Normal
			new double[] { .021, .060, .120, .200, .200, 1.000 },	// LR2Easy
		};

		public static readonly double[] rankTimingMultipliers =
		{
			0.5,	// #RANK 0
			0.75,	// #RANK 1
			1.0,	// #RANK 2
			1.5,	// #RANK 3
			2.0,	// #RANK 4
		};

		protected double missEarlyWindow = 1.0;

		readonly int scoreExMax;

		public double[] timingWindow { get; private set; } = timingWindowDefault;	
		
		public int combo { get; private set; }
		public int scorePGreatCount = 0;
		public int scoreGreatCount = 0;
		public int scoreGoodCount = 0;
		public int scoreBadCount = 0;
		public int scorePoorCount = 0;
		public int scoreLargestCombo = 0;
		public int delayFastCount = 0;
		public int delaySlowCount = 0;
		public int delayFastCount2 = 0;
		public int delaySlowCount2 = 0;
		public double gaugeHealth = 0.0;

		double judgeTimingMulti = rankTimingMultipliers[2];

		double gaugeMinHealth = 0.0;
		double gaugeFailThreshold = 0.0;
		bool gaugeFailInstant = false;
		double gaugeGreat = 0.0;
		double gaugeGood = 0.0;
		double gaugeBadPoor = 0.0;
		double gaugeMiss = 0.0;

		public double totalDifference = 0.0;
		public int judgedKeyCount = 0;

		public delegate void OnNoteJudgedDelegate(NoteScore noteScore);
		public OnNoteJudgedDelegate OnNoteJudged;

		public BMSJudge(Song song)
			: base(song)
		{
			if (song == null || chart == null)
				return;

			Settings settings = SettingsManager.instance;

			// setup gauge

			double total = chart.gaugeTotal;
			double totalMultiplier = chart.gaugeMultiplier;
			int notes = chart.noteCount;
			if (total <= 0.0)
				total = Math.Max(7.605 * notes / (0.01 * notes + 6.5), 260.0);

			if (totalMultiplier >= 0.0)
				total *= totalMultiplier;
			else
				throw new ApplicationException("Negative total multiplier not supported");

			switch (settings.gameplay.gaugeMode)
			{
				case GaugeMode.Default:
					gaugeHealth = 0.2;
					gaugeMinHealth = 0.02;
					gaugeFailThreshold = 0.80; 

					gaugeGreat = total / notes / 100.0;
					gaugeGood = gaugeGreat * 0.5;
					gaugeBadPoor = -0.02;
					gaugeMiss = -0.06;
					break;
				case GaugeMode.Easy:
					gaugeHealth = 0.2;
					gaugeMinHealth = 0.02;
					gaugeFailThreshold = 0.80; 

					gaugeGreat = total / notes / 100.0;
					gaugeGood = gaugeGreat * 0.5;
					gaugeBadPoor = -0.016;
					gaugeMiss = -0.048;
					break;
				case GaugeMode.Hard:
					gaugeHealth = 1.0;
					gaugeFailThreshold = 0.02;
					gaugeFailInstant = true;

					gaugeHealth = 1.0;
					gaugeGreat = 0.0016;
					gaugeGood = 0.0;
					gaugeBadPoor = -0.05;
					gaugeMiss = -0.09;
					break;
				default:
					break;
			}

			// adjust rank timing
			int rank = chart.rank;
			if (!useLR2timing)
			{
				if (chart.rankMultiplier != 0.0)
					judgeTimingMulti = rankTimingMultipliers[2] * chart.rankMultiplier;
				else if (rank >= 0 && rank < rankTimingMultipliers.Length)
					judgeTimingMulti = rankTimingMultipliers[rank];
				else
					Log.Warning("Unsupported #RANK " + rank.ToString() + ", falling back to #RANK 2");
			}
			else
			{
				timingWindow = timingWindowLR2[2];
				if (rank >= 0 && rank < timingWindowLR2.Length)
					timingWindow = timingWindowLR2[rank];
				else
					Log.Warning("Unsupported #RANK " + rank.ToString() + ", falling back to #RANK 2");

				if (chart.rankMultiplier != 0.0)
					judgeTimingMulti = rankTimingMultipliers[2] * chart.rankMultiplier;
			}
			for (int i = 0; i < timingWindow.Length - 1; i++)
				timingWindow[i] *= judgeTimingMulti;

			foreach (double time in timingWindow)
				if (time > processAheadTime)
					processAheadTime = time;

			missWindow = timingWindow[4];

			scoreExMax = chart.noteCount * 2;
		}

		public void OnKeyRelease(int lane)
		{
			double hitTimestamp = judgeTime;

			NoteScore closestNote = GetClosestNote(hitTimestamp, lane, NoteJudgeType.JudgeRelease);
			if (closestNote == null)
				return;

			if (!HasJudged((closestNote.noteEvent as LongNoteEndEvent).startNote))
			{
				// ignore key releases before long note starting point
				return;
			}

			JudgeNote(hitTimestamp, closestNote);
		}

		public void OnKeyPress(int lane)
		{
			double hitTimestamp = judgeTime;

			NoteScore closestNote = GetClosestNote(hitTimestamp, lane, NoteJudgeType.JudgePress | NoteJudgeType.JudgeHold);
			if (closestNote == null)
				return;

			JudgeNote(hitTimestamp, closestNote);
		}

		private NoteScore GetClosestNote(double hitTimestamp, int lane, NoteJudgeType judgeType)
		{
			NoteScore closestNote = null;
			double closestDiff = double.MaxValue;
			foreach (NoteScore noteScore in pendingNoteScores)
			{
				if (!judgeType.HasFlag(noteScore.judgeType))
					continue;

				if (noteScore.noteEvent.lane != lane)
					continue;

				double diff = Math.Abs(noteScore.timestamp - hitTimestamp);
				if (diff >= closestDiff)
					continue;
			
				closestNote = noteScore;
				closestDiff = diff;
			}

			return closestNote;
		}

		public override void JudgeNote(double hitTimestamp, NoteScore noteScore)
		{
			double difference;
			if (truncateTiming) // truncate to 1ms accuracy
				difference = ((int)(hitTimestamp * 1000) - (int)(noteScore.timestamp * 1000)) / 1000.0;
			else
				difference = hitTimestamp - noteScore.timestamp;

			bool fast = difference < 0;
			bool judged = true;
			bool release = false;

			if (Math.Abs(difference) <= timingWindow[0])
			{
				// P.Great
				combo++;
				scorePGreatCount++;
			}
			else if (Math.Abs(difference) <= timingWindow[1])
			{
				// Great
				if (fast)
					delayFastCount++;
				else
					delaySlowCount++;

				combo++;
				scoreGreatCount++;
			}
			else if (Math.Abs(difference) <= timingWindow[2])
			{
				// Good
				if (fast)
					delayFastCount2++;
				else
					delaySlowCount2++;

				combo++;
				scoreGoodCount++;
			}
			else if (Math.Abs(difference) <= timingWindow[3])
			{
				// Bad
				if (fast)
					delayFastCount2++;
				else
					delaySlowCount2++;

				combo = 0;
				scoreBadCount++;
			}
			else
			{
				if (noteScore.judgeType != NoteJudgeType.JudgeRelease)
				{
					if (difference > timingWindow[4])
					{
						// Poor, late miss
						combo = 0;
						scorePoorCount++;
						release = true;
					}
					else if (difference > -timingWindow[5] && difference <= -timingWindow[4])
					{
						// Poor, early miss, apply penalty but no combo reset and judge
						scorePoorCount++;
						judged = false;
					}
				}
				else
				{
					// Long note early release
					combo = 0;
					scorePoorCount++;
				}
			}

			if (combo > scoreLargestCombo)
				scoreLargestCombo = combo;

			if (Math.Abs(difference) <= timingWindow[3])
			{
				// ignore missed notes from delay calculations
				totalDifference += difference;
				judgedKeyCount++;
			}

			if (judged)
				NotePlayed(hitTimestamp, noteScore);

			if (release && noteScore.judgeType == NoteJudgeType.JudgeHold)
			{
				// judge endpoint after early release
				LongNoteEndEvent endEvent = (noteScore.noteEvent as LongNoteEvent).endNote;
				for (int i = 0; i < pendingNoteScores.Count; i++)
				{
					if (pendingNoteScores[i].noteEvent != endEvent)
						continue;

					JudgeNote(hitTimestamp, pendingNoteScores[i]);
					break;
				}
			}
			

			if (OnNoteJudged != null)
				OnNoteJudged(noteScore);

			AdjustGauge(difference);
		}

		public void AdjustGauge(double difference)
		{
			if (Math.Abs(difference) <= timingWindow[0])
				gaugeHealth += gaugeGreat;
			else if (Math.Abs(difference) <= timingWindow[1])
				gaugeHealth += gaugeGreat;
			else if (Math.Abs(difference) <= timingWindow[2])
				gaugeHealth += gaugeGood;
			else if (Math.Abs(difference) <= timingWindow[3])
				gaugeHealth += gaugeBadPoor;
			else if (difference > -timingWindow[5] && difference <= -timingWindow[4])
				gaugeHealth += gaugeBadPoor;
			else if (difference > timingWindow[4])
				gaugeHealth += gaugeMiss;
			else // long note early release
				gaugeHealth += gaugeMiss;
			
			// clamp gauge health
			gaugeHealth = Math.Min(Math.Max(gaugeMinHealth, gaugeHealth), 1.0);

			if (gaugeFailInstant && gaugeHealth < gaugeFailThreshold)
			{
				// TODO: fail here
			}
		}

		public int GetScoreEx()
		{
			return (scorePGreatCount * 2) + scoreGreatCount;
		}

		public int GetGrade()
		{
			double percentage = (double)GetScoreEx() / scoreExMax;
			if (percentage >= 8.0 / 9.0)
				return 7;   // AAA
			else if (percentage >= 7.0 / 9.0)
				return 6;   // AA
			else if (percentage >= 6.0 / 9.0)
				return 5;   // A
			else if (percentage >= 5.0 / 9.0)
				return 4;   // B
			else if (percentage >= 4.0 / 9.0)
				return 3;   // C
			else if (percentage >= 3.0 / 9.0)
				return 2;   // D
			else if (percentage >= 2.0 / 9.0)
				return 1;   // E
			else
				return 0;   // F
		}

		public double GetAverageDelay()
		{
			return totalDifference / judgedKeyCount;
		}

		public double GetCurrentPercentage()
		{
			return (double)GetScoreEx() / scoreExMax;
		}
	}

	public enum ScrollMode : int
	{
		Default = 0,	// scroll speed scales based on song's initial BPM
		MaxFix = 1,		// scroll speed scales based on song's maximum BPM
		MinFix = 2,		// scroll speed scales based on song's minimum BPM
		Average = 3,	// scroll speed scales based on song's average BPM
		Constant = 4,	// scroll speed is constant
		Common = 5,		// scroll speed scales based on song's most common BPM
	}

	public enum PlayMode : int
	{
		Single = 0,
		Battle = 1,
		DoubleBattle = 2,   // couple play?
		SP2DP = 3,          // ?
		GBattle = 4,		// ?
	}

	public enum LaneMode : int
	{
		Default = 0,
		Hidden = 1,
		Sudden = 2,
		SuddenHidden = 3,
	}

	public enum AssistMode : int
	{
		Disabled = 0,
		AutoScratch = 1,
		Autoplay = 2,
		Legacy = 3,			// LN notes converted to standard notes
	}

	public enum GaugeMode : int
	{
		Default = 0,		// Groove
		Hard = 1,
		Death = 2,          // Hazard mode, Instant fail on combo break
		Easy = 3,
		PerfectAttack = 4,  // All PGreat
		GoodAttack = 5,     // All Good
	}

	public enum RandomMode : int
	{
		Disabled = 0,
		Mirror = 1,     // mirrored keys
		Random = 2,     // randomized keys
		SRandom = 3,    // randomized notes, scratch ignored
		HRandom = 4,    // randomized notes, no jacks
		AllScratch = 5, // all notes moved to scratch
	}
}