using System;
using System.Collections;
using System.Collections.Generic;

namespace Pulsus.Gameplay
{
	public class EventPlayerGraph : IEnumerable<EventPlayer>
	{
		List<EventPlayer> players = new List<EventPlayer>();

		bool pendingStop;

		public void Add(EventPlayer player)
		{
			players.Add(player);
		}

		public void SetStartPosition(long startPulse)
		{
			foreach (EventPlayer player in players)
				player.startPulse = startPulse;
		}

		public void AdjustTimeline(double offset)
		{
			double globalAdjust = 0.0;
			foreach (EventPlayer player in players)
				globalAdjust = Math.Max(player.startOffset, globalAdjust);

			globalAdjust = -globalAdjust + offset;
			foreach (EventPlayer player in players)
				player.startOffset += globalAdjust;
		}

		public void Start()
		{
			foreach (EventPlayer player in players)
				player.StartPlayer();
		}

		public void Update(double deltaTime)
		{
			foreach (EventPlayer player in players)
				player.Update(deltaTime);

			if (pendingStop)
				TryStop();
		}

		public void Stop()
		{
			pendingStop = true;
			TryStop();
		}

		public void TryStop()
		{
			bool allStopped = true;
			foreach (EventPlayer player in players)
			{
				player.StopPlayer();
				if (!player.stopping)
					allStopped = false;
			}

			if (allStopped)
			{
				pendingStop = false;
			}
		}

		public IEnumerator<EventPlayer> GetEnumerator()
		{
			return players.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return players.GetEnumerator();
		}
	}
}