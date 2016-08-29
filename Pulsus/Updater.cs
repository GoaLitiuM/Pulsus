using Jil;
using Pulsus.FFmpeg;
using System.IO.Compression;
using System.IO;
using System.Net;
using System.Reflection;
using System;

namespace Pulsus
{
	public static class Updater
	{
		// TODO: support async operations with updater

		// check updates from this repository
		private const string gitRepository = "GoaLitiuM/Pulsus";

		public static UpdateInfo GetLatestUpdate()
		{
			Log.Info("Updater: Checking for updates...");

			UpdateInfo info = new UpdateInfo();
			info.version = new Version(0, 0, 0);

			string apiUrl = string.Format("https://api.github.com/repos/{0}/releases/latest", gitRepository);

			HttpWebRequest request = (HttpWebRequest)WebRequest.Create(apiUrl);
			request.UserAgent = Program.name;
			request.Timeout = 2000;

			try
			{
				using (var response = request.GetResponse())
				{
					using (StreamReader stream = new StreamReader(response.GetResponseStream()))
					{
						var result = JSON.DeserializeDynamic(stream.ReadToEnd());

						info = new UpdateInfo()
						{
							changelog = (string)result.body,
							version = Version.Parse((string)result.tag_name),
							date = (string)result.published_at,
						};

						// having undefined revision messes up equality comparison between two versions
						if (info.version.Revision == -1)
							info.version = new Version(info.version.Major, info.version.Minor, info.version.Build, 0);

						foreach (var asset in result.assets)
						{
							string filename = (string)asset.name;
							string[] tokens = filename.Split(new char[] { '_' });

							string fileVersion = tokens[1];
							string filePlatform = tokens[2].Replace(".zip", "");
							if (tokens.Length >= 4)
							{
								// old release format: Program_Version_OS_Arch
								filePlatform += "-" + tokens[3].Replace(".zip", "");
							}

							if (filePlatform.Equals(Program.platformId, StringComparison.OrdinalIgnoreCase))
							{
								info.downloadUrl = (string)asset.browser_download_url;
								break;
							}
						}
					}
				}
			}
			catch (Exception e)
			{
				Log.Warning("Updater: Failed to retrieve latest update from GitHub: " + e.Message);
			}

			return info;
		}

		public static void CleanUpdateFiles()
		{
			foreach (string file in Directory.EnumerateFiles(Program.basePath, "*.tmp", SearchOption.TopDirectoryOnly))
				File.Delete(file);

			foreach (string file in Directory.EnumerateFiles(FFmpegHelper.ffmpegPath, "*.tmp", SearchOption.TopDirectoryOnly))
				File.Delete(file);
		}

		public static string DownloadUpdate(UpdateInfo updateInfo)
		{
			string filename = Path.GetFileName(updateInfo.downloadUrl);
			string outputDir = Path.Combine(Path.GetTempPath(), Program.name + "Cache");
			string outputFile = Path.Combine(outputDir, filename);

			if (!File.Exists(outputFile))
			{
				Log.Info("Updater: Downloading update " + Utility.GetVersionString(updateInfo.version) + "...");
				if (!Directory.Exists(outputDir))
					Directory.CreateDirectory(outputDir);

				WebClient webClient = new WebClient();
				webClient.DownloadFile(updateInfo.downloadUrl, outputFile);
			}
			else
				Log.Warning("Updater: Update files already present at: " + outputFile);

			return outputFile;
		}

		public static void ApplyUpdate(string updateFile, UpdateInfo updateInfo)
		{
			Log.Info("Updater: Applying update " + Utility.GetVersionString(updateInfo.version));

			string exePath = Assembly.GetExecutingAssembly().Location;
			try
			{
				using (ZipArchive archive = ZipFile.OpenRead(updateFile))
				{
					foreach (ZipArchiveEntry entry in archive.Entries)
					{
						string entryOutPath = Path.Combine(Program.basePath, entry.FullName);
						if (!entry.FullName.EndsWith("/"))
						{
							try
							{
								// save the old executable so it can be restored in case of failure
								if (entryOutPath.Equals(exePath, StringComparison.Ordinal))
									File.Move(entryOutPath, entryOutPath + ".tmp");

								entry.ExtractToFile(entryOutPath, true);
							}
							catch (IOException)
							{
								// file is in use, rename it and delete it during next launch
								File.Move(entryOutPath, entryOutPath + ".tmp");
								entry.ExtractToFile(entryOutPath, true);
							}
						}
						else
							Directory.CreateDirectory(entryOutPath);
					}
				}
			}
			catch (Exception e)
			{
				// restore old executable back
				if (File.Exists(exePath + ".tmp"))
				{
					if (File.Exists(exePath))
						File.Delete(exePath);

					File.Move(exePath + ".tmp", exePath);
				}

				throw new ApplicationException("Updater: Failed to install update: " + e.Message);
			}

			File.Delete(updateFile);
		}
	}

	public struct UpdateInfo
	{
		public Version version;
		public string date;
		public string changelog;
		public string downloadUrl;
	}
}
