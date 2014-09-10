﻿// <airvpn_source_header>
// This file is part of AirVPN Client software.
// Copyright (C)2014-2014 AirVPN (support@airvpn.org) / https://airvpn.org )
//
// AirVPN Client is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// AirVPN Client is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with AirVPN Client. If not, see <http://www.gnu.org/licenses/>.
// </airvpn_source_header>

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using AirVPN.Core;
using Microsoft.Win32;

namespace AirVPN.Platforms
{
	public class NetworkLockWindowsFirewallProfile
	{
		public string id;
		public bool State = false;
		public string Inbound = "";
		public string Outbound = "";
		public bool Notifications = false;

		public NetworkLockWindowsFirewallProfile(string name)
		{
			id = name;
		}

		public void Fetch()
		{
			string report = Platform.Instance.ShellCmd("netsh advfirewall show " + id);

			State = report.IndexOf("ON") != -1;

			if (report.IndexOf("AllowInbound") != -1)
				Inbound = "AllowInbound";
			else if (report.IndexOf("BlockInboundAlways") != -1)
				Inbound = "BlockInboundAlways";
			else if (report.IndexOf("BlockInbound") != -1)
				Inbound = "BlockInbound";

			if (report.IndexOf("AllowOutbound") != -1)
				Outbound = "AllowOutbound";
			else if (report.IndexOf("BlockOutbound") != -1)
				Outbound = "BlockOutbound";

			string regkey = GetNotificationRegPath();
			object objNotifications = Registry.GetValue(regkey, "DisableNotifications", 0);
			Notifications = (objNotifications.ToString() == "0");
		}

		public string GetNotificationRegPath()
		{
			string notificationsProfileName = "";
			if(id == "domain")
				notificationsProfileName = "DomainProfile";
			else if(id == "private")
				notificationsProfileName = "StandardProfile";
			else if(id == "public")
				notificationsProfileName = "PublicProfile";
			else
			{
				notificationsProfileName = "StandardProfile";
			}

			return "HKEY_LOCAL_MACHINE\\System\\CurrentControlSet\\services\\SharedAccess\\Parameters\\FirewallPolicy\\" + notificationsProfileName;
		}

		public string GetOldFirewallProfileName()
		{
			if (id == "domain")
				return "DOMAIN";
			else if (id == "private")
				return "STANDARD";
			else if (id == "public")
				return "CURRENT";
			else
				return "ALL";
		}

		public void StateOn()
		{
			Platform.Instance.ShellCmd("netsh advfirewall set " + id + " state on");
		}

		public void StateOff()
		{
			Platform.Instance.ShellCmd("netsh advfirewall set " + id + " state off");
		}

		public void NotifyOn()
		{			
			//Registry.SetValue(GetNotificationRegPath(), "DisableNotifications", 0, RegistryValueKind.DWord);
			Platform.Instance.ShellCmd("netsh firewall set notifications mode=enable profile=" + GetOldFirewallProfileName());
		}

		public void NotifyOff()
		{
			//Registry.SetValue(GetNotificationRegPath(), "DisableNotifications", 1, RegistryValueKind.DWord);
			Platform.Instance.ShellCmd("netsh firewall set notifications mode=disable profile=" + GetOldFirewallProfileName());
		}

		public void RestorePolicy()
		{
			Platform.Instance.ShellCmd("netsh advfirewall set " + id + " firewallpolicy " + Inbound + "," + Outbound);
		}

		public void ReadXML(XmlElement node)
		{
			State = (node.GetAttribute("state") == "1");
			Inbound = node.GetAttribute("inbound");
			Outbound = node.GetAttribute("outbound");
		}

		public void WriteXML(XmlElement node)
		{
			node.SetAttribute("state", State ? "1" : "0");
			node.SetAttribute("inbound", Inbound);
			node.SetAttribute("outbound", Outbound);
		}
	}

	public class NetworkLockWindowsFirewall : NetworkLockPlugin
	{
		private List<NetworkLockWindowsFirewallProfile> Profiles = new List<NetworkLockWindowsFirewallProfile>();
		private bool m_activated;
		private string m_lastestIpList;

		public override string GetCode()
		{
			return "windows_firewall";
		}

		public override string GetName()
		{
			return "Windows Firewall";
		}

		public override void Init()
		{
			base.Init();

			Profiles.Clear();
			Profiles.Add(new NetworkLockWindowsFirewallProfile("domain"));
			Profiles.Add(new NetworkLockWindowsFirewallProfile("private"));
			Profiles.Add(new NetworkLockWindowsFirewallProfile("public"));
		}
			
		public override void Activation()
		{
			base.Activation();

			// If 'backup.wfw' doesn't exists, create it. It's a general backup of the first time.
			string rulesBackupFirstTime = Storage.DataPath + Platform.Instance.DirSep + "winfirewallrulesorig.wfw";
			if (File.Exists(rulesBackupFirstTime) == false)
				Exec("netsh advfirewall export \"" + rulesBackupFirstTime + "\"");

			string rulesBackupSession = Storage.DataPath + Platform.Instance.DirSep + "winfirewallrules.wfw";
			if (File.Exists(rulesBackupSession))
				File.Delete(rulesBackupSession);
			Exec("netsh advfirewall export \"" + rulesBackupSession + "\"");
			if (File.Exists(rulesBackupSession) == false)
				throw new Exception(Messages.NetworkLockWindowsFirewallBackupFailed);

			foreach (NetworkLockWindowsFirewallProfile profile in Profiles)
				profile.Fetch();

			foreach (NetworkLockWindowsFirewallProfile profile in Profiles)
			{
				if (profile.State == false)
				{
					profile.StateOn();					
				}

				if (profile.Notifications == true)
				{
					profile.NotifyOff();
				}
			}

			Exec("netsh advfirewall firewall delete rule name=all");
			
			Exec("netsh advfirewall firewall add rule name=\"AirVPN - Out - AllowLocal\" dir=out action=allow remoteip=LocalSubnet");
			Exec("netsh advfirewall firewall add rule name=\"AirVPN - Out - AllowVPN\" dir=out action=allow localip=10.4.0.0-10.9.255.255");

			Exec("netsh advfirewall set allprofiles firewallpolicy BlockInbound,BlockOutbound");

			m_activated = true; // To avoid OnUpdateIps before this moment

			OnUpdateIps();
		}

		public override void Deactivation()
		{
			base.Deactivation();

			foreach (NetworkLockWindowsFirewallProfile profile in Profiles)
				profile.RestorePolicy();

			Exec("netsh advfirewall firewall delete rule name=\"AirVPN - Out - AllowLocal\"");
			Exec("netsh advfirewall firewall delete rule name=\"AirVPN - Out - AllowVPN\"");
			Exec("netsh advfirewall firewall delete rule name=\"AirVPN - Out - AllowAirIPS\"");

			string rulesBackupSession = Storage.DataPath + Platform.Instance.DirSep + "winfirewallrules.wfw";
			if (File.Exists(rulesBackupSession))
			{
				Exec("netsh advfirewall import \"" + rulesBackupSession + "\"");
				File.Delete(rulesBackupSession);
			}

			foreach (NetworkLockWindowsFirewallProfile profile in Profiles)
			{
				if (profile.State == false)
					profile.StateOff();
				/* Not need, already restored in below import
				if (profile.Notifications == true)
					profile.NotifyOn();
				*/
			}

			m_lastestIpList = "";
		}

		public override void OnUpdateIps()
		{
			if (m_activated == false)
				return;

			List<IpAddressRange> ipsFirewalled = GetAllIps();
			string ipList = "";
			foreach (IpAddressRange ip in ipsFirewalled)
			{
				if (ipList != "")
					ipList += ",";
				ipList += ip.ToCIDR();
			}

			if (ipList != m_lastestIpList)
			{
				m_lastestIpList = ipList;

				if(m_lastestIpList != "")
					Exec("netsh advfirewall firewall delete rule name=\"AirVPN - Out - AllowAirIPS\"");
				Exec("netsh advfirewall firewall add rule name=\"AirVPN - Out - AllowAirIPS\" dir=out action=allow remoteip=" + ipList);
			}
		}

		public override void OnRecoveryLoad(XmlElement root)
		{
			base.OnRecoveryLoad(root);

			foreach (NetworkLockWindowsFirewallProfile profile in Profiles)
			{
				XmlElement node = Utils.XmlGetFirstElementByTagName(root, profile.id);
				if (node != null)
				{
					profile.ReadXML(node);
				}
			}
		}

		public override void OnRecoverySave(XmlElement root)
		{
			base.OnRecoverySave(root);

			foreach (NetworkLockWindowsFirewallProfile profile in Profiles)
			{
				XmlElement el = (XmlElement) root.AppendChild(root.OwnerDocument.CreateElement(profile.id));
				profile.WriteXML(el);
			}
		}
	}
}
