﻿// <eddie_source_header>
// This file is part of Eddie/AirVPN software.
// Copyright (C)2014-2016 AirVPN (support@airvpn.org) / https://airvpn.org )
//
// Eddie is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Eddie is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Eddie. If not, see <http://www.gnu.org/licenses/>.
// </eddie_source_header>

using System;

using Foundation;
using AppKit;
using Eddie.Core;
using Eddie.Common;

namespace Eddie.UI.Cocoa.Osx
{
	public partial class WindowCommandController : NSWindowController
	{
		public string Command = "";

		public WindowCommandController(IntPtr handle) : base(handle)
		{
		}

		[Export("initWithCoder:")]
		public WindowCommandController(NSCoder coder) : base(coder)
		{
		}

		public WindowCommandController() : base("WindowCommand")
		{
		}

		public override void AwakeFromNib()
		{
			base.AwakeFromNib();

			Window.Title = Constants.Name + " - " + LanguageManager.GetText("WindowsCommandTitle");

			GuiUtils.SetButtonCancel(Window, CmdCancel);
            GuiUtils.SetButtonDefault(Window, CmdOk);

			CmdOk.Activated += (object sender, EventArgs e) =>
			{
				Command = TxtCommand.StringValue;

				Window.Close();
				NSApplication.SharedApplication.StopModal();
			};

			CmdCancel.Activated += (object sender, EventArgs e) =>
			{
				Command = "";

				Window.Close();
				NSApplication.SharedApplication.StopModal();
			};

			LnkHelp.Activated += (object sender, EventArgs e) =>
			{
                GuiUtils.OpenUrl(UiClient.Instance.Data["links"]["help"]["openvpn-management"].Value as string);
			};
		}

		public new WindowCommand Window
		{
			get { return (WindowCommand)base.Window; }
		}
	}
}
