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
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenRA.Mods.Common
{
	public class MissionProgress
	{
		static string progressFile;

		public static void SaveMissionProgress()	
		{
			// TODO: Triggering when leaving a map; need access to map information
			ExtractInformationFromMap();
			SaveInformationInFile();
		}

		static void ExtractInformationFromMap()
		{
			// Get mod name
			// Get map name
			// Get campaign mode
			// Get win state from player
			// Get objectives with state
			// Get current stats from player
			// Get other information from map
			SaveInformationInFile();
		}

		static void SaveInformationInFile()
		{
			// Create file if not available
			// Save information in file
		}

		static void CreateMissionProgressFile()
		{
			// Create MissionProgress-File <mod><missionName>progress.yaml
		}

	}
}
