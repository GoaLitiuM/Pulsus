using System;
using System.IO;

namespace Pulsus
{
	public static partial class Utility
	{
		static Utility()
		{
			Utility_Platform();

			base36CharValueMap = new int[256];
			for (int i = 0; i < base36CharValueMap.Length; i++)
				base36CharValueMap[i] = short.MaxValue;
			for (int i = '0', j = 0; i <= '9'; i++, j++)
				base36CharValueMap[i] = j;
			for (int i = 'a', j = 10; i <= 'z'; i++, j++)
				base36CharValueMap[i] = j;
			for (int i = 'A', j = 10; i <= 'Z'; i++, j++)
				base36CharValueMap[i] = j;
		}

		/// <summary> Optimized version of StartsWith with StringComparison.Ordinal </summary>
		public static bool StartsWithFast(this string str, string str2)
		{
			if (str2.Length > str.Length)
				return false;

			int len = str2.Length < str.Length ? str2.Length : str.Length;
			for (int i = 0; i < len; ++i)
				if (str[i] != str2[i])
					return false;

			return len > 0;
		}

		/// <summary> Optimized version of StartsWith with StringComparison.OrdinalIgnoreCase </summary>
		public static bool StartsWithFastIgnoreCase(this string str, string str2)
		{
			if (str2.Length > str.Length)
				return false;

			int len = str2.Length < str.Length ? str2.Length : str.Length;
			for (int i = 0; i < len; ++i)
			{
				char c1 = str[i];
				char c2 = str2[i];
				if (c1 == c2)
					continue;

				// convert to lowercase
				if (c1 >= 'A' && c1 <= 'Z')
					c1 = (char)(c1 + 'a' - 'A');
				if (c2 >= 'A' && c2 <= 'Z')
					c2 = (char)(c2 + 'a' - 'A');
				if (c1 == c2)
					continue;

				return false;
			}
			return len > 0;
		}

		private static readonly char[] base36Table = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
		private static readonly int[] base36CharValueMap;

		public static string ToBase36(int index)
		{
			if (index >= 1296)
				throw new ArgumentException("Value too big to encode in base36");

			return new string(new char[2] { base36Table[(index / 36) % 36], base36Table[index % 36] });
		}

		public static int FromBase36(char c)
		{
			int value = base36CharValueMap[c];
			if (value >= 36 * 36)
				throw new ArgumentException("'" + c + "' is not a valid base36 value");

			return value;
		}

		public static int FromBase36(char c1, char c2)
		{
			int value = base36CharValueMap[c1] * 36 + base36CharValueMap[c2];
			if (value >= 36 * 36)
				throw new ArgumentException("'" + c1 + c2 + "' is not a valid base36 value");

			return value;
		}

		public static bool TryFromBase36(char c1, char c2, out int value)
		{
			value = base36CharValueMap[c1] * 36 + base36CharValueMap[c2];
			if (value >= 36 * 36)
			{
				value = 0;
				return false;
			}

			return true;
		}

		public static int FromBase36(params char[] str)
		{
			throw new ArgumentException("Invalid base36 string, string must has length of 1-2");
		}

		public static int FromBase36(string str)
		{
			int value;
			if (str.Length == 2)
				value = base36CharValueMap[str[0]] * 36 + base36CharValueMap[str[1]];
			else if (str.Length == 1)
				value = base36CharValueMap[str[0]];
			else
				throw new ArgumentException("Invalid base36 string, string must has length of 1-2");

			if (value >= 36 * 36)
				throw new ArgumentException("'" + str + "' is not a valid base36 value");

			return value;
		}

		public static int lcm(int a, int b)
		{
			checked
			{
				return (a / gcf(a, b)) * b;
			}
		}

		public static int gcf(int a, int b)
		{
			while (b != 0)
			{
				int temp = b;
				b = a % b;
				a = temp;
			}
			return a;
		}

		public static long lcm(long a, long b)
		{
			checked
			{
				return (a / gcf(a, b)) * b;
			}
		}

		public static string GetVersionString(Version version)
		{
			return string.Format("{0}.{1}{2}",
				version.Major.ToString(), version.Minor.ToString(), version.Build != 0 ? ("." + version.Build.ToString()) : "");
		}

		public static long gcf(long a, long b)
		{
			while (b != 0)
			{
				long temp = b;
				b = a % b;
				a = temp;
			}
			return a;
		}

		// returns value between min and max (exclusive)
		public static int Range(this Random random, int min, int max)
		{
			if (random == null)
				random = new Random();

			return random.Next(min, max);
		}

		// returns value between min and max (inclusive)
		public static double Range(this Random random, double min, double max)
		{
			if (random == null)
				random = new Random();

			// NextDouble upper is exclusive
			return ((random.NextDouble() / 0.99999999999999978) * (max - min)) + min;
		}

		// finds a file that matches the filename from alternative locations and with different file extensions
		public static string FindRealFile(string path, string[] lookupPaths, string[] lookupExtensions)
		{
			string pathParent = Directory.GetParent(path).FullName;
			string filename = Path.GetFileName(path);
			string newFilepath = path;
			foreach (string lookup in lookupPaths)
			{
				newFilepath = Path.Combine(Path.Combine(pathParent, lookup), filename);
				bool exists = File.Exists(newFilepath);
				if (!exists)
				{
					string oldExt = Path.GetExtension(newFilepath).ToLower();

					foreach (string ext in lookupExtensions)
					{
						if (ext == oldExt)
							continue;

						newFilepath = Path.ChangeExtension(newFilepath, ext);
						exists = File.Exists(newFilepath);
						if (exists)
							break;
					}
				}

				if (exists)
					break;
				else
					newFilepath = null;
			}

			if (newFilepath == null)
				return path;

			return newFilepath;
		}
	}
}
