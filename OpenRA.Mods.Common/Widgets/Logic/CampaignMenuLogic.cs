#region Copyright & License Information
/*
 * Copyright 2007-2014 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

using OpenRA.Graphics;
using OpenRA.Network;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public class CampaignMenuLogic
	{
		[ObjectCreator.UseCtor]
		public CampaignMenuLogic(Widget widget, Action onStart, Action onExit)
		{
			var continueButton1 = widget.Get<ButtonWidget>("CONTINUE_FACTION1_BUTTON");
			var continueButton2 = widget.Get<ButtonWidget>("CONTINUE_FACTION2_BUTTON");
			var newButton = widget.Get<ButtonWidget>("NEW_BUTTON");
			var backButton = widget.Get<ButtonWidget>("BACK_BUTTON");

			var factionList = new List<String>();

			foreach (var p in CampaignProgress.players)
			{
				if (!factionList.Contains(p.Faction.Name))
				{
					factionList.Add(p.Faction.Name);
				}
				if (CampaignProgress.GetMission(p.Faction.Name).Length == 0)
				{
					if (factionList.Count == 1)
						continueButton1.Disabled = true;
					if (factionList.Count == 2)
						continueButton2.Disabled = true;
				}
			}

			backButton.OnClick = () =>
			{
				Game.Disconnect();
				Ui.CloseWindow();
				onExit();
			};

			newButton.OnClick = () =>
			{
				Game.OpenWindow("CAMPAIGN_FACTION", new WidgetArgs
				{
					{ "onExit", () => { } },
					{ "onStart", () => { widget.Parent.RemoveChild(widget); } }
				});
			};

			continueButton1.OnClick = () =>
			{
				CampaignWorldLogic.Campaign = "GDI Campaign";
				Game.OpenWindow("CAMPAIGN_WORLD", new WidgetArgs
					{
						{ "onExit", () => { } },
						{ "onStart", () => widget.Parent.RemoveChild(widget) }
					});
			};

			continueButton2.OnClick = () =>
			{
				CampaignWorldLogic.Campaign = "Nod Campaign";
				Game.OpenWindow("CAMPAIGN_WORLD", new WidgetArgs
					{
						{ "onExit", () => { } },
						{ "onStart", () => widget.Parent.RemoveChild(widget) }
					});
			};
		}
	}
}
