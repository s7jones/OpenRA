#region Copyright & License Information
/*
 * Copyright 2007-2015 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace OpenRA
{
	public enum PlatformType { Unknown, Windows, OSX, Linux }

	public static class Platform
	{
		public static PlatformType CurrentPlatform { get { return currentPlatform.Value; } }

		static Lazy<PlatformType> currentPlatform = Exts.Lazy(GetCurrentPlatform);

		static PlatformType GetCurrentPlatform()
		{
			if (Environment.OSVersion.Platform == PlatformID.Win32NT)
				return PlatformType.Windows;

			try
			{
				var psi = new ProcessStartInfo("uname", "-s");
				psi.UseShellExecute = false;
				psi.RedirectStandardOutput = true;
				var p = Process.Start(psi);
				var kernelName = p.StandardOutput.ReadToEnd();
				if (kernelName.Contains("Darwin"))
					return PlatformType.OSX;
				else
					return PlatformType.Linux;
			}
			catch { }

			return PlatformType.Unknown;
		}

		public static string RuntimeVersion
		{
			get
			{
				var mono = Type.GetType("Mono.Runtime");
				if (mono == null)
					return ".NET CLR {0}".F(Environment.Version);

				var version = mono.GetMethod("GetDisplayName", BindingFlags.NonPublic | BindingFlags.Static);
				if (version == null)
					return "Mono (unknown version) CLR {0}".F(Environment.Version);

				return "Mono {0} CLR {1}".F(version.Invoke(null, null), Environment.Version);
			}
		}

		public static string SupportDir { get { return supportDir.Value; } }
		static Lazy<string> supportDir = Exts.Lazy(GetSupportDir);

		static string GetSupportDir()
		{
			// Use a local directory in the game root if it exists
			if (Directory.Exists("Support"))
				return "Support" + Path.DirectorySeparatorChar;

			var dir = Environment.GetFolderPath(Environment.SpecialFolder.Personal);

			switch (CurrentPlatform)
			{
				case PlatformType.Windows:
					dir += Path.DirectorySeparatorChar + "OpenRA";
					break;
				case PlatformType.OSX:
					dir += "/Library/Application Support/OpenRA";
					break;
				case PlatformType.Linux:
				default:
					dir += "/.openra";
					break;
			}

			if (!Directory.Exists(dir))
				Directory.CreateDirectory(dir);

			return dir + Path.DirectorySeparatorChar;
		}

		public static string GameDir { get { return AppDomain.CurrentDomain.BaseDirectory; } }

		/// <summary>Replaces special character prefixes with full paths.</summary>
		public static string ResolvePath(string path)
		{
			path = path.TrimEnd(new char[] { ' ', '\t' });

			// If the path contains ':', chances are it is a package path.
			// If it isn't, someone passed an already resolved path, which is wrong.
			if (path.IndexOf(":", StringComparison.Ordinal) > 1)
			{
				var split = path.Split(':');
				return ResolvePath(split[0], split[1]);
			}

			// paths starting with ^ are relative to the support dir
			if (path.StartsWith("^"))
				path = SupportDir + path.Substring(1);

			// paths starting with . are relative to the game dir
			if (path.StartsWith("./") || path.StartsWith(".\\"))
				path = GameDir + path.Substring(2);

			return path;
		}

		/// <summary>Replaces package names with full paths. Avoid using this for non-package paths.</summary>
		public static string ResolvePath(string package, string target)
		{
			// Resolve mod package paths.
			if (ModMetadata.AllMods.ContainsKey(package))
				package = ModMetadata.CandidateModPaths[package];

			return ResolvePath(Path.Combine(package, target));
		}

		/// <summary>Replace special character prefixes with full paths.</summary>
		public static string ResolvePath(params string[] path)
		{
			return ResolvePath(path.Aggregate(Path.Combine));
		}

		/// <summary>Replace the full path prefix with the special notation characters ^ or .</summary>
		public static string UnresolvePath(string path)
		{
			if (path.StartsWith(SupportDir))
				path = "^" + path.Substring(SupportDir.Length);

			if (path.StartsWith(GameDir))
				path = "./" + path.Substring(GameDir.Length);

			return path;
		}
	}
}
