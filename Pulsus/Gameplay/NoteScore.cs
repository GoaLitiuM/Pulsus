namespace Pulsus.Gameplay
{
	public class NoteScore
	{
		public NoteEvent noteEvent { get; }
		public double timestamp { get; }
		public NoteJudgeType judgeType { get; }
		public double hitOffset = double.MinValue;

		public NoteScore(NoteEvent noteEvent, double timestamp, NoteJudgeType judgeType)
		{
			this.noteEvent = noteEvent;
			this.timestamp = timestamp;
			this.judgeType = judgeType;
		}
	}
}
