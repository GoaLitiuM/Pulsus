using System;
using System.Collections.Generic;

namespace Pulsus.Gameplay
{
	public abstract class Judge : EventPlayer
	{
		protected List<NoteScore> noteScores;
		protected List<NoteScore> pendingNoteScores;

		protected double judgeTime;
		protected double processAheadTime;
		protected double missWindow;

		protected bool seeking;

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
			seeking = true;
			base.StartPlayer();
			seeking = false;

			startTime += processAheadTime;
			base.StartPlayer();
		}

		public override void AdvanceTime(double deltaTime)
		{
			base.AdvanceTime(deltaTime);
			judgeTime = currentTime - processAheadTime;
		}

		public override void UpdateSong()
		{
			base.UpdateSong();

			for (int i = 0; i < pendingNoteScores.Count; i++)
			{
				NoteScore noteScore = pendingNoteScores[i];

				if (judgeTime > noteScore.timestamp + missWindow)
					JudgeNote(judgeTime, noteScore);
				else
					break;
			}
		}

		public override void OnSongEnd()
		{
			// prevent judge from stopping prematurely, as the processAheadTime
			// affects the time how much ahead the judge is from other players.
		}

		public override void OnPlayerKey(NoteEvent noteEvent)
		{
			if (seeking)
				return;

			pendingNoteScores.Add(new NoteScore(noteEvent, noteEvent.timestamp, NoteJudgeType.JudgePress));
		}

		public override void OnPlayerKeyLong(LongNoteEvent noteEvent)
		{
			if (seeking)
				return;

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
