// Copyright (c) 2019 saltmine.de - https://github.com/saltminede

using System;
using System.Collections.Generic;
using System.Linq;

namespace SaltyServer
{
    public class VoiceManager : GTANetworkAPI.Script
    {
        #region Properties
        public static string ServerUniqueIdentifier { get; private set; }
        public static string RequiredUpdateBranch { get; private set; }
        public static string MinimumPluginVersion { get; private set; }
        public static string SoundPack { get; private set; }
        public static string IngameChannel { get; private set; }
        public static string IngameChannelPassword { get; private set; }

        public static GTANetworkAPI.Vector3[] RadioTowers { get; private set; } = new GTANetworkAPI.Vector3[]
        {
            new GTANetworkAPI.Vector3(552.8169f, -27.8083f, 94.87936f),
            new GTANetworkAPI.Vector3(758.5276f, 1273.74f, 360.2965f),
            new GTANetworkAPI.Vector3(1857.389f, 3694.529f, 38.9618f),
            new GTANetworkAPI.Vector3(-448.2019f, 6019.807f, 36.62916f)
        };

        public static VoiceClient[] VoiceClients => VoiceManager._voiceClients.Values.ToArray();
        private static Dictionary<GTANetworkAPI.Client, VoiceClient> _voiceClients = new Dictionary<GTANetworkAPI.Client, VoiceClient>();

        public static RadioChannel[] RadioChannels => VoiceManager._radioChannels.ToArray();
        private static List<RadioChannel> _radioChannels = new List<RadioChannel>();
        #endregion

        #region Server Events
        [GTANetworkAPI.ServerEvent(GTANetworkAPI.Event.ResourceStart)]
        public void OnResourceStart()
        {
            VoiceManager.ServerUniqueIdentifier = GTANetworkAPI.NAPI.Resource.GetSetting<string>(this, "ServerUniqueIdentifier");
            VoiceManager.RequiredUpdateBranch = GTANetworkAPI.NAPI.Resource.GetSetting<string>(this, "RequiredUpdateBranch");
            VoiceManager.MinimumPluginVersion = GTANetworkAPI.NAPI.Resource.GetSetting<string>(this, "MinimumPluginVersion");
            VoiceManager.SoundPack = GTANetworkAPI.NAPI.Resource.GetSetting<string>(this, "SoundPack");
            VoiceManager.IngameChannel = GTANetworkAPI.NAPI.Resource.GetSetting<string>(this, "IngameChannel");
            VoiceManager.IngameChannelPassword = GTANetworkAPI.NAPI.Resource.GetSetting<string>(this, "IngameChannelPassword");
        }

        [GTANetworkAPI.ServerEvent(GTANetworkAPI.Event.PlayerConnected)]
        public void OnPlayerConnected(GTANetworkAPI.Client client)
        {
            VoiceClient voiceClient = new VoiceClient(client, VoiceManager.GetTeamSpeakName(), SaltyShared.SharedData.VoiceRanges[1]);

            lock (VoiceManager._voiceClients)
            {
                VoiceManager._voiceClients.Add(client, voiceClient);
            }

            client.TriggerEvent(SaltyShared.Event.SaltyChat_Initialize, VoiceManager.ServerUniqueIdentifier, VoiceManager.RequiredUpdateBranch, VoiceManager.MinimumPluginVersion, VoiceManager.SoundPack, VoiceManager.IngameChannel, VoiceManager.IngameChannelPassword, voiceClient.TeamSpeakName);
        }

        [GTANetworkAPI.ServerEvent(GTANetworkAPI.Event.PlayerDisconnected)]
        public void OnPlayerDisconnected(GTANetworkAPI.Client client, GTANetworkAPI.DisconnectionType disconnectionType, string reason)
        {
            VoiceClient voiceClient;

            lock (VoiceManager._voiceClients)
            {
                if (!VoiceManager._voiceClients.TryGetValue(client, out voiceClient))
                    return;

                VoiceManager._voiceClients.Remove(client);
            }

            foreach (RadioChannel radioChannel in VoiceManager.RadioChannels.Where(c => c.IsMember(voiceClient)))
            {
                radioChannel.RemoveMember(voiceClient);
            }

            foreach (VoiceClient cl in VoiceManager.VoiceClients)
            {
                cl.Player.TriggerEvent(SaltyShared.Event.SaltyChat_Disconnected, voiceClient.Player.Handle.Value);
            }
        }
        #endregion

        #region Remote Events
        [GTANetworkAPI.RemoteEvent(SaltyShared.Event.SaltyChat_CheckVersion)]
        private void OnCheckVersion(GTANetworkAPI.Client player, string branch, string version)
        {
            if (!VoiceManager.TryGetVoiceClient(player, out VoiceClient voiceClient))
                return;

            if (!VoiceManager.IsVersionAccepted(branch, version))
            {
                player.Kick($"[Salty Chat] Required Branch: {VoiceManager.RequiredUpdateBranch} | Required Version: {VoiceManager.MinimumPluginVersion}");
                return;
            }

            foreach (VoiceClient cl in VoiceManager.VoiceClients)
            {
                player.TriggerEvent(SaltyShared.Event.SaltyChat_UpdateClient, cl.Player.Handle.Value, cl.TeamSpeakName, cl.VoiceRange);

                cl.Player.TriggerEvent(SaltyShared.Event.SaltyChat_UpdateClient, voiceClient.Player.Handle.Value, voiceClient.TeamSpeakName, voiceClient.VoiceRange);
            }

            player.TriggerEvent(SaltyShared.Event.SaltyChat_UpdateRadioTowers, Newtonsoft.Json.JsonConvert.SerializeObject(VoiceManager.RadioTowers));
        }

        [GTANetworkAPI.RemoteEvent(SaltyShared.Event.SaltyChat_IsTalking)]
        public void OnIsTalking(GTANetworkAPI.Client player, bool isTalking)
        {
            if (!VoiceManager.TryGetVoiceClient(player, out VoiceClient voiceClient))
                return;

            foreach (VoiceClient client in VoiceManager.VoiceClients)
            {
                client.Player.TriggerEvent(SaltyShared.Event.SaltyChat_IsTalking, player.Handle.Value, isTalking);
            }
        }

        [GTANetworkAPI.RemoteEvent(SaltyShared.Event.SaltyChat_SetVoiceRange)]
        public void OnSetVoiceRange(GTANetworkAPI.Client player, float voiceRange)
        {
            if (!VoiceManager.TryGetVoiceClient(player, out VoiceClient voiceClient))
                return;

            if (Array.IndexOf(SaltyShared.SharedData.VoiceRanges, voiceRange) >= 0)
            {
                voiceClient.VoiceRange = voiceRange;

                foreach (VoiceClient client in VoiceManager.VoiceClients)
                {
                    client.Player.TriggerEvent(SaltyShared.Event.SaltyChat_UpdateClient, player.Handle.Value, voiceClient.TeamSpeakName, voiceClient.VoiceRange);
                }
            }
        }
        #endregion

        #region Commands (Radio)
#if DEBUG
        [GTANetworkAPI.Command("speaker")]
        public void OnSetRadioSpeaker(GTANetworkAPI.Client player, string toggleString)
        {
            bool toggle = String.Equals(toggleString, "true", StringComparison.OrdinalIgnoreCase);

            VoiceManager.SetRadioSpeaker(player, toggle);

            player.SendChatMessage("Speaker", $"The speaker is now {(toggle ? "on" : "off")}.");
        }

        [GTANetworkAPI.Command("joinradio")]
        public void OnJoinRadioChannel(GTANetworkAPI.Client player, string channelName)
        {
            VoiceManager.JoinRadioChannel(player, channelName);

            player.SendChatMessage("Radio", $"You joined channel \"{channelName}\".");
        }

        [GTANetworkAPI.Command("leaveradio")]
        public void OnLeaveRadioChannel(GTANetworkAPI.Client player, string channelName)
        {
            VoiceManager.LeaveRadioChannel(player, channelName);

            player.SendChatMessage("Radio", $"You left channel \"{channelName}\".");
        }
#endif
        #endregion

        #region Remote Events (Radio)
        [GTANetworkAPI.RemoteEvent(SaltyShared.Event.SaltyChat_IsSending)]
        public void OnSendingOnRadio(GTANetworkAPI.Client player, string radioChannelName, bool isSending)
        {
            if (!VoiceManager.TryGetVoiceClient(player, out VoiceClient voiceClient))
                return;

            RadioChannel radioChannel = VoiceManager.GetRadioChannel(radioChannelName, false);

            if (radioChannel == null || !radioChannel.IsMember(voiceClient))
                return;

            radioChannel.Send(voiceClient, isSending);
        }
        #endregion

        #region Methods (Radio)
        public static RadioChannel GetRadioChannel(string name, bool create)
        {
            RadioChannel radioChannel;

            lock (VoiceManager._radioChannels)
            {
                radioChannel = VoiceManager.RadioChannels.FirstOrDefault(r => r.Name == name);

                if (radioChannel == null && create)
                {
                    radioChannel = new RadioChannel(name);

                    VoiceManager._radioChannels.Add(radioChannel);
                }
            }

            return radioChannel;
        }

        public static void SetRadioSpeaker(GTANetworkAPI.Client player, bool toggle)
        {
            if (!VoiceManager.TryGetVoiceClient(player, out VoiceClient voiceClient))
                return;

            voiceClient.RadioSpeaker = toggle;
        }

        public static void JoinRadioChannel(GTANetworkAPI.Client player, string radioChannelName)
        {
            if (!VoiceManager.TryGetVoiceClient(player, out VoiceClient voiceClient))
                return;

            foreach (RadioChannel channel in VoiceManager.RadioChannels)
            {
                if (channel.IsMember(voiceClient))
                    return;
            }

            RadioChannel radioChannel = VoiceManager.GetRadioChannel(radioChannelName, true);

            radioChannel.AddMember(voiceClient);
        }

        public static void LeaveRadioChannel(GTANetworkAPI.Client player)
        {
            if (!VoiceManager.TryGetVoiceClient(player, out VoiceClient voiceClient))
                return;

            foreach (RadioChannel radioChannel in VoiceManager.RadioChannels.Where(r => r.IsMember(voiceClient)))
            {
                VoiceManager.LeaveRadioChannel(player, radioChannel.Name);
            }
        }

        public static void LeaveRadioChannel(GTANetworkAPI.Client player, string radioChannelName)
        {
            if (!VoiceManager.TryGetVoiceClient(player, out VoiceClient voiceClient))
                return;

            RadioChannel radioChannel = VoiceManager.GetRadioChannel(radioChannelName, false);

            if (radioChannel != null)
            {
                radioChannel.RemoveMember(voiceClient);

                if (radioChannel.Members.Length == 0)
                {
                    VoiceManager._radioChannels.Remove(radioChannel);
                }
            }
        }

        public static void SendingOnRadio(GTANetworkAPI.Client player, string radioChannelName, bool isSending)
        {
            if (!VoiceManager.TryGetVoiceClient(player, out VoiceClient voiceClient))
                return;

            RadioChannel radioChannel = VoiceManager.GetRadioChannel(radioChannelName, false);

            if (radioChannel == null || !radioChannel.IsMember(voiceClient))
                return;

            radioChannel.Send(voiceClient, isSending);
        }
        #endregion

        #region Methods
        internal static string GetTeamSpeakName()
        {
            string name;

            lock (VoiceManager._voiceClients)
            {
                do
                {
                    name = Guid.NewGuid().ToString().Replace("-", "");

                    if (name.Length > 30)
                    {
                        name = name.Remove(29, name.Length - 30);
                    }
                }
                while (VoiceManager._voiceClients.Values.Any(c => c.TeamSpeakName == name));
            }

            return name;
        }

        public static bool IsVersionAccepted(string branch, string version)
        {
            if (!String.IsNullOrWhiteSpace(VoiceManager.RequiredUpdateBranch) && VoiceManager.RequiredUpdateBranch != branch)
            {
                return false;
            }

            if (!String.IsNullOrWhiteSpace(VoiceManager.MinimumPluginVersion))
            {
                try
                {
                    string[] minimumVersionArray = VoiceManager.MinimumPluginVersion.Split('.');
                    string[] versionArray = version.Split('.');

                    int lengthCounter = 0;

                    if (versionArray.Length >= minimumVersionArray.Length)
                    {
                        lengthCounter = minimumVersionArray.Length;
                    }
                    else
                    {
                        lengthCounter = versionArray.Length;
                    }

                    for (int i = 0; i < lengthCounter; i++)
                    {
                        int min = Convert.ToInt32(minimumVersionArray[i]);
                        int cur = Convert.ToInt32(versionArray[i]);

                        if (cur > min)
                        {
                            return true;
                        }
                        else if (min > cur)
                        {
                            return false;
                        }
                    }
                }
                catch
                {
                    return false;
                }
            }

            return true;
        }
        #endregion

        #region Helper
        public static bool TryGetVoiceClient(GTANetworkAPI.Client client, out VoiceClient voiceClient)
        {
            lock (VoiceManager._voiceClients)
            {
                if (VoiceManager._voiceClients.TryGetValue(client, out voiceClient))
                    return true;
            }

            voiceClient = null;
            return false;
        }
        #endregion
    }
}
