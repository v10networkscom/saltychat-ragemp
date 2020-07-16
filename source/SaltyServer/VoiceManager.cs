using System;
using System.Collections.Generic;
using System.Linq;

namespace SaltyServer
{
    public class VoiceManager : GTANetworkAPI.Script
    {
        #region Properties
        public static VoiceManager Instance { get; private set; }

        public string ServerUniqueIdentifier { get; private set; }
        public string MinimumPluginVersion { get; private set; }
        public string SoundPack { get; private set; }
        public string IngameChannel { get; private set; }
        public string IngameChannelPassword { get; private set; }
        public ulong[] SwissChannels { get; private set; } = new ulong[0];

        public GTANetworkAPI.Vector3[] RadioTowers { get; private set; } = new GTANetworkAPI.Vector3[]
        {
            new GTANetworkAPI.Vector3(552.8169f, -27.8083f, 94.87936f),
            new GTANetworkAPI.Vector3(758.5276f, 1273.74f, 360.2965f),
            new GTANetworkAPI.Vector3(1857.389f, 3694.529f, 38.9618f),
            new GTANetworkAPI.Vector3(-448.2019f, 6019.807f, 36.62916f)
        };

        public VoiceClient[] VoiceClients => this._voiceClients.Values.ToArray();
        private Dictionary<GTANetworkAPI.Player, VoiceClient> _voiceClients = new Dictionary<GTANetworkAPI.Player, VoiceClient>();

        public RadioChannel[] RadioChannels => this._radioChannels.ToArray();
        private List<RadioChannel> _radioChannels = new List<RadioChannel>();
        #endregion

        #region CTOR
        public VoiceManager()
        {
            VoiceManager.Instance = this;
        }
        #endregion

        #region Server Events
        [GTANetworkAPI.ServerEvent(GTANetworkAPI.Event.ResourceStart)]
        public void OnResourceStart()
        {
            this.ServerUniqueIdentifier = GTANetworkAPI.NAPI.Resource.GetSetting<string>(this, "ServerUniqueIdentifier");
            this.MinimumPluginVersion = GTANetworkAPI.NAPI.Resource.GetSetting<string>(this, "MinimumPluginVersion");
            this.SoundPack = GTANetworkAPI.NAPI.Resource.GetSetting<string>(this, "SoundPack");
            this.IngameChannel = GTANetworkAPI.NAPI.Resource.GetSetting<string>(this, "IngameChannel");
            this.IngameChannelPassword = GTANetworkAPI.NAPI.Resource.GetSetting<string>(this, "IngameChannelPassword");

            string swissChannelIds = GTANetworkAPI.NAPI.Resource.GetSetting<string>(this, "SwissChannelIds");

            if (!String.IsNullOrEmpty(swissChannelIds))
            {
                this.SwissChannels = swissChannelIds.Split(',').Select(s => UInt64.Parse(s.Trim())).ToArray();
            }
        }

        [GTANetworkAPI.ServerEvent(GTANetworkAPI.Event.PlayerConnected)]
        public void OnPlayerConnected(GTANetworkAPI.Player client)
        {
            VoiceClient voiceClient;

            lock (this._voiceClients)
            {
                voiceClient = new VoiceClient(client, this.GetTeamSpeakName(), SaltyShared.SharedData.VoiceRanges[1]);

                this._voiceClients.Add(client, voiceClient);
            }

            client.TriggerEvent(
                SaltyShared.Event.SaltyChat_Initialize,
                voiceClient.TeamSpeakName,
                this.ServerUniqueIdentifier,
                this.SoundPack,
                this.IngameChannel,
                this.IngameChannelPassword,
                Newtonsoft.Json.JsonConvert.SerializeObject(this.SwissChannels),
                Newtonsoft.Json.JsonConvert.SerializeObject(this.RadioTowers)
            );

            foreach (VoiceClient cl in this.VoiceClients)
            {
                client.TriggerEvent(SaltyShared.Event.SaltyChat_UpdateClient, cl.Player.Handle.Value, cl.TeamSpeakName, cl.VoiceRange);

                cl.Player.TriggerEvent(SaltyShared.Event.SaltyChat_UpdateClient, voiceClient.Player.Handle.Value, voiceClient.TeamSpeakName, voiceClient.VoiceRange);
            }
        }

        [GTANetworkAPI.ServerEvent(GTANetworkAPI.Event.PlayerDisconnected)]
        public void OnPlayerDisconnected(GTANetworkAPI.Player client, GTANetworkAPI.DisconnectionType disconnectionType, string reason)
        {
            VoiceClient voiceClient;

            lock (this._voiceClients)
            {
                if (!this._voiceClients.TryGetValue(client, out voiceClient))
                    return;

                this._voiceClients.Remove(client);
            }

            foreach (RadioChannel radioChannel in this.RadioChannels.Where(c => c.IsMember(voiceClient)))
            {
                radioChannel.RemoveMember(voiceClient);
            }

            foreach (VoiceClient cl in this.VoiceClients)
            {
                cl.Player.TriggerEvent(SaltyShared.Event.SaltyChat_Disconnected, voiceClient.Player.Handle.Value);
            }
        }
        #endregion

        #region Remote Events
        [GTANetworkAPI.RemoteEvent(SaltyShared.Event.SaltyChat_CheckVersion)]
        public void OnCheckVersion(GTANetworkAPI.Player player, string version)
        {
            if (!this.TryGetVoiceClient(player, out VoiceClient voiceClient))
                return;

            if (!this.IsVersionAccepted(version))
            {
                player.Kick($"[Salty Chat] Required Version: {this.MinimumPluginVersion}");
                return;
            }
        }

        [GTANetworkAPI.RemoteEvent(SaltyShared.Event.SaltyChat_SetVoiceRange)]
        public void OnSetVoiceRange(GTANetworkAPI.Player player, float voiceRange)
        {
            if (!this.TryGetVoiceClient(player, out VoiceClient voiceClient))
                return;

            if (Array.IndexOf(SaltyShared.SharedData.VoiceRanges, voiceRange) >= 0)
            {
                voiceClient.VoiceRange = voiceRange;

                foreach (VoiceClient client in this.VoiceClients)
                {
                    client.Player.TriggerEvent(SaltyShared.Event.SaltyChat_SetVoiceRange, player.Handle.Value, voiceClient.VoiceRange);
                }
            }
        }
        #endregion

        #region Commands (Radio)
#if DEBUG
        [GTANetworkAPI.Command("speaker")]
        public void OnSetRadioSpeaker(GTANetworkAPI.Player player, string toggleString)
        {
            bool toggle = String.Equals(toggleString, "true", StringComparison.OrdinalIgnoreCase);

            this.SetRadioSpeaker(player, toggle);

            player.SendChatMessage("Speaker", $"The speaker is now {(toggle ? "on" : "off")}.");
        }

        [GTANetworkAPI.Command("joinradio")]
        public void OnJoinRadioChannel(GTANetworkAPI.Player player, string channelName)
        {
            this.JoinRadioChannel(player, channelName);

            player.SendChatMessage("Radio", $"You joined channel \"{channelName}\".");
        }

        [GTANetworkAPI.Command("leaveradio")]
        public void OnLeaveRadioChannel(GTANetworkAPI.Player player, string channelName)
        {
            this.LeaveRadioChannel(player, channelName);

            player.SendChatMessage("Radio", $"You left channel \"{channelName}\".");
        }
#endif
        #endregion

        #region Remote Events (Radio)
        [GTANetworkAPI.RemoteEvent(SaltyShared.Event.SaltyChat_IsSending)]
        public void OnSendingOnRadio(GTANetworkAPI.Player player, string radioChannelName, bool isSending)
        {
            if (!this.TryGetVoiceClient(player, out VoiceClient voiceClient))
                return;

            RadioChannel radioChannel = this.GetRadioChannel(radioChannelName, false);

            if (radioChannel == null || !radioChannel.IsMember(voiceClient))
                return;

            radioChannel.Send(voiceClient, isSending);
        }
        #endregion

        #region Methods (Radio)
        public RadioChannel GetRadioChannel(string name, bool create)
        {
            RadioChannel radioChannel;

            lock (this._radioChannels)
            {
                radioChannel = this.RadioChannels.FirstOrDefault(r => r.Name == name);

                if (radioChannel == null && create)
                {
                    radioChannel = new RadioChannel(name);

                    this._radioChannels.Add(radioChannel);
                }
            }

            return radioChannel;
        }

        public void SetRadioSpeaker(GTANetworkAPI.Player player, bool toggle)
        {
            if (!this.TryGetVoiceClient(player, out VoiceClient voiceClient))
                return;

            voiceClient.RadioSpeaker = toggle;
        }

        public void JoinRadioChannel(GTANetworkAPI.Player player, string radioChannelName)
        {
            if (!this.TryGetVoiceClient(player, out VoiceClient voiceClient))
                return;

            foreach (RadioChannel channel in this.RadioChannels)
            {
                if (channel.IsMember(voiceClient))
                    return;
            }

            RadioChannel radioChannel = this.GetRadioChannel(radioChannelName, true);

            radioChannel.AddMember(voiceClient);
        }

        public void LeaveRadioChannel(GTANetworkAPI.Player player)
        {
            if (!this.TryGetVoiceClient(player, out VoiceClient voiceClient))
                return;

            foreach (RadioChannel radioChannel in this.RadioChannels.Where(r => r.IsMember(voiceClient)))
            {
                this.LeaveRadioChannel(player, radioChannel.Name);
            }
        }

        public void LeaveRadioChannel(GTANetworkAPI.Player player, string radioChannelName)
        {
            if (!this.TryGetVoiceClient(player, out VoiceClient voiceClient))
                return;

            RadioChannel radioChannel = this.GetRadioChannel(radioChannelName, false);

            if (radioChannel != null)
            {
                radioChannel.RemoveMember(voiceClient);

                if (radioChannel.Members.Length == 0)
                {
                    this._radioChannels.Remove(radioChannel);
                }
            }
        }

        public void SendingOnRadio(GTANetworkAPI.Player player, string radioChannelName, bool isSending)
        {
            if (!this.TryGetVoiceClient(player, out VoiceClient voiceClient))
                return;

            RadioChannel radioChannel = this.GetRadioChannel(radioChannelName, false);

            if (radioChannel == null || !radioChannel.IsMember(voiceClient))
                return;

            radioChannel.Send(voiceClient, isSending);
        }
        #endregion

        #region Methods
        internal string GetTeamSpeakName()
        {
            string name;

            do
            {
                name = Guid.NewGuid().ToString().Replace("-", "");

                if (name.Length > 30)
                {
                    name = name.Remove(29, name.Length - 30);
                }
            }
            while (this._voiceClients.Values.Any(c => c.TeamSpeakName == name));

            return name;
        }

        public bool IsVersionAccepted(string version)
        {
            if (!String.IsNullOrWhiteSpace(this.MinimumPluginVersion))
            {
                try
                {
                    string[] minimumVersionArray = this.MinimumPluginVersion.Split('.');
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
        public bool TryGetVoiceClient(GTANetworkAPI.Player client, out VoiceClient voiceClient)
        {
            lock (this._voiceClients)
            {
                if (this._voiceClients.TryGetValue(client, out voiceClient))
                    return true;
            }

            voiceClient = null;
            return false;
        }
        #endregion
    }
}
