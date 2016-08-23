using System;
using System.Collections;
using System.Collections.Generic;

namespace Pulsus.Gameplay
{
	public class EventPlayerGraph : IEnumerable<EventPlayer>
	{
		List<EventPlayer> players = new List<EventPlayer>();

		public void Add(EventPlayer player)
		{
			players.Add(player);
		}

		public void SetStartPosition(double startTime)
		{
			foreach (EventPlayer player in players)
				player.startTime = startTime;
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
		}

		public void Stop()
		{
			foreach (EventPlayer player in players)
				player.StopPlayer();
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