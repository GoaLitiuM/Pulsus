using System;
using System.Collections.Generic;

namespace Pulsus.Gameplay
{
	public abstract class Judge : EventPlayer
	{
		protected List<NoteScore> noteScores;
		protected List<NoteScore> pendingNoteScores;

		protected double processAheadTime = 0.0;
		protected double missWindow;

		protected Dictionary<int, int> lastNote = new Dictionary<int, int>();

		protected double judgeTime { get { return currentTime - processAheadTime; } }

		public Judge(Song song)
			: base(song)
		{
			if (song == null || chart == null)
				return;

			noteScores = new List<NoteScore>(chart.playerEventCount);
			pendingNoteScores = new List<NoteScore>(chart.playerEventCount);
		}

		public override void StartPlayer()
		{
			startOffset += processAheadTime;
			base.StartPlayer();
		}

		public override void UpdateSong()
		{
			base.UpdateSong();

			for (int i = 0; i < pendingNoteScores.Count; i++)
			{
				NoteScore noteScore = pendingNoteScores[i];

				if (judgeTime > noteScore.timestamp + missWindow)
					JudgeNote(judgeTime, noteScore);
			}
		}

		public override void OnSongEnd()
		{
			// prevent judge from stopping prematurely, as the processAheadTime
			// affects the time how much ahead the judge is from other players.
		}

		public override void OnPlayerKey(NoteEvent noteEvent)
		{
			pendingNoteScores.Add(new NoteScore(noteEvent, noteEvent.timestamp, NoteJudgeType.JudgePress));
		}

		public override void OnPlayerKeyLong(LongNoteEvent noteEvent)
		{
			pendingNoteScores.Add(new NoteScore(noteEvent, noteEvent.timestamp, NoteJudgeType.JudgeHold));
			pendingNoteScores.Add(new NoteScore(noteEvent.endNote, noteEvent.endNote.timestamp, NoteJudgeType.JudgeRelease));
		}

		public override void OnBPM(BPMEvent bpmEvent)
		{
			base.OnBPM(bpmEvent);

			if (nextBpm < 0)
				nextBpm = -nextBpm;
		}

		public virtual void NotePlayed(double hitTimestamp, NoteScore noteScore)
		{
			noteScore.hitOffset = hitTimestamp - noteScore.timestamp;

			pendingNoteScores.Remove(noteScore);
			noteScores.Add(noteScore);

			if (noteScore.judgeType == NoteJudgeType.JudgeHold)
			{
				LongNoteEndEvent endEvent = (noteScore.noteEvent as LongNoteEvent).endNote;
				for (int i = 0; i < pendingNoteScores.Count; i++)
				{
					if (pendingNoteScores[i].noteEvent != endEvent)
						continue;

					// judge endpoint after early release
					JudgeNote(hitTimestamp, pendingNoteScores[i]);
					break;
				}
			}
		}

		public bool HasJudged(NoteEvent note)
		{
			foreach (NoteScore noteScore in pendingNoteScores)
			{
				if (noteScore.noteEvent == note)
					return false;
			}
			return true;
		}

		public abstract void JudgeNote(double hitTimestamp, NoteScore noteScore);
	}

	[Flags]
	public enum NoteJudgeType
	{
		JudgePress = 1,
		JudgeHold = 2,
		JudgeRelease = 4,
	}
}
