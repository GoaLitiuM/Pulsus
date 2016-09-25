using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Pulsus
{
	public static class Log
	{
		public class LogMessage
		{
			public string message;
			public DateTime timestamp;
			public int repeated;

			public LogMessage(string message, DateTime timestamp)
			{
				this.message = message;
				this.timestamp = timestamp;
			}
		}

		public static string logPath { get; private set; }
		private static StreamWriter logStream;
		public static int warningCount;
		public static int errorCount;

		// clears recent messages from the log view
		public static void Clear()
		{
			logList.Clear();
		}

		public static List<LogMessage> logList = new List<LogMessage>();
		public static DateTime lastMessageTime = DateTime.UtcNow;

		public static void SetLogFile(string path)
		{
			if (logPath != null)
				throw new ApplicationException("Log path already set");

			logPath = path;
			logStream = new StreamWriter(File.Open(logPath,
				FileMode.Create, FileAccess.Write, FileShare.ReadWrite), Encoding.UTF8);
		}

		public static void FlushLog()
		{
			logStream.Flush();
		}

		public static void Text(string str, params object[] args)
		{
			string fstr = string.Format(str, args);

			Console.WriteLine(fstr);

			logStream.WriteLine(fstr);
			FlushLog();
		}

		public static void Info(string str, params object[] args)
		{
			string fstr = string.Format(str, args);
			fstr = "[I] " + fstr;

			Console.WriteLine(fstr);
			Debug.WriteLine(fstr);

			logStream.WriteLine(fstr);
			FlushLog();
		}

		public static void Warning(string str, params object[] args)
		{
			string fstr = string.Format(str, args);
			fstr = "[W] " + fstr;

			Console.WriteLine(fstr);
			Debug.WriteLine(fstr);

			lastMessageTime = DateTime.UtcNow;
			logList.Add(new LogMessage(fstr, lastMessageTime));

			logStream.WriteLine(fstr);
			FlushLog();

			warningCount++;
		}

		public static void Error(string str, params object[] args)
		{
			string fstr = string.Format(str, args);
			fstr = "[E] " + fstr;

			Console.WriteLine(fstr);
			Debug.WriteLine(fstr);

			lastMessageTime = DateTime.UtcNow;
			logList.Add(new LogMessage(fstr, lastMessageTime));

			logStream.WriteLine(fstr);
			FlushLog();

			errorCount++;
		}

		public static void Fatal(string str, params object[] args)
		{
			string fstr = string.Format(str, args);

			fstr = "[F] " + fstr;
			fstr = fstr.Replace("\n", "\n[F] ");

			Console.WriteLine(fstr);
			Debug.WriteLine(fstr);

			lastMessageTime = DateTime.UtcNow;
			logList.Add(new LogMessage(fstr, lastMessageTime));

			logStream.WriteLine(fstr);
			logStream.Flush();
		}
	}
}