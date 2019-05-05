// Copyright (c) 2019 saltmine.de - https://github.com/saltminede

using System;
using System.Collections.Generic;
using System.Linq;

namespace SaltyServer
{
    public class Voice : GTANetworkAPI.Script
    {
        #region Properties
        public static string ServerUniqueIdentifier { get; private set; }
        public static string RequiredUpdateBranch { get; private set; }
        public static string MinimumPluginVersion { get; private set; }
        public static string SoundPack { get; private set; }
        public static string IngameChannel { get; private set; }
        public static string IngameChannelPassword { get; private set; }

        private static Dictionary<string, List<GTANetworkAPI.Client>> RadioChannels = new Dictionary<string, List<GTANetworkAPI.Client>>();
        private static Dictionary<string, List<GTANetworkAPI.Client>> PlayersTalkingOnRadioChannels = new Dictionary<string, List<GTANetworkAPI.Client>>();
        #endregion

        #region Server Events
        [GTANetworkAPI.ServerEvent(GTANetworkAPI.Event.ResourceStart)]
        public void OnResourceStart()
        {
            Voice.ServerUniqueIdentifier = GTANetworkAPI.NAPI.Resource.GetSetting<string>(this, "ServerUniqueIdentifier");
            Voice.RequiredUpdateBranch = GTANetworkAPI.NAPI.Resource.GetSetting<string>(this, "RequiredUpdateBranch");
            Voice.MinimumPluginVersion = GTANetworkAPI.NAPI.Resource.GetSetting<string>(this, "MinimumPluginVersion");
            Voice.SoundPack = GTANetworkAPI.NAPI.Resource.GetSetting<string>(this, "SoundPack");
            Voice.IngameChannel = GTANetworkAPI.NAPI.Resource.GetSetting<string>(this, "IngameChannel");
            Voice.IngameChannelPassword = GTANetworkAPI.NAPI.Resource.GetSetting<string>(this, "IngameChannelPassword");
        }

        [GTANetworkAPI.ServerEvent(GTANetworkAPI.Event.PlayerConnected)]
        public void OnPlayerConnected(GTANetworkAPI.Client client)
        {
            client.SetSharedData(SaltyShared.SharedData.Voice_TeamSpeakName, Voice.GetTeamSpeakName());
            client.SetSharedData(SaltyShared.SharedData.Voice_VoiceRange, SaltyShared.SharedData.VoiceRanges[1]);

            client.TriggerEvent(SaltyShared.Event.Voice_Initialize, Voice.ServerUniqueIdentifier, Voice.RequiredUpdateBranch, Voice.MinimumPluginVersion, Voice.SoundPack, Voice.IngameChannel, Voice.IngameChannelPassword);
        }

        [GTANetworkAPI.ServerEvent(GTANetworkAPI.Event.PlayerDisconnected)]
        public void OnPlayerDisconnected(GTANetworkAPI.Client client, GTANetworkAPI.DisconnectionType disconnectionType, string reason)
        {
            Voice.RemovePlayerRadioChannel(client);

            if (!client.TryGetSharedData(SaltyShared.SharedData.Voice_TeamSpeakName, out string tsName))
                return;

            // Broken on some systems
            //GTANetworkAPI.NAPI.ClientEvent.TriggerClientEventForAll(SaltyShared.Event.Player_Disconnected, tsName);

            foreach (GTANetworkAPI.Client cl in GTANetworkAPI.NAPI.Pools.GetAllPlayers())
            {
                cl.TriggerEvent(SaltyShared.Event.Player_Disconnected, tsName);
            }
        }
        #endregion

        #region Remote Events
        [GTANetworkAPI.RemoteEvent(SaltyShared.Event.Voice_RejectedVersion)]
        public void OnRejectedVersion(GTANetworkAPI.Client client, string updateBranch, string version)
        {
            if (String.IsNullOrWhiteSpace(Voice.RequiredUpdateBranch) && String.IsNullOrWhiteSpace(Voice.MinimumPluginVersion))
                return;

            if (!String.IsNullOrWhiteSpace(Voice.RequiredUpdateBranch) && updateBranch != Voice.RequiredUpdateBranch)
                client.SendNotification($"[Salty Chat] Required update branch: {Voice.RequiredUpdateBranch} | Your update branch: {updateBranch}");
            else
                client.SendNotification($"[Salty Chat] Required version: {Voice.MinimumPluginVersion} | Your version: {version}");

            client.Kick();
        }

        [GTANetworkAPI.RemoteEvent(SaltyShared.Event.Voice_IsTalking)]
        public void OnPlayerTalking(GTANetworkAPI.Client client, bool isTalking)
        {
#warning There seems to be an issue where the "client"-object is not correctly referenced on the client, remove workaround if the issue is resolved

            if (!client.TryGetSharedData(SaltyShared.SharedData.Voice_TeamSpeakName, out string tsName))
                return;

            foreach (GTANetworkAPI.Client cl in GTANetworkAPI.NAPI.Pools.GetAllPlayers())
            {
                cl.TriggerEvent(SaltyShared.Event.Voice_IsTalking, tsName, isTalking);
            }

            //GTANetworkAPI.NAPI.ClientEvent.TriggerClientEventForAll(SaltyShared.Event.Voice_IsTalking, client, isTalking)
        }

        [GTANetworkAPI.RemoteEvent(SaltyShared.Event.Voice_SetVoiceRange)]
        public void OnSetVoiceRange(GTANetworkAPI.Client client, float voiceRange)
        {
            client.SetSharedData(SaltyShared.SharedData.Voice_VoiceRange, voiceRange);
        }

        [GTANetworkAPI.RemoteEvent(SaltyShared.Event.Voice_TalkingOnRadio)]
        public void OnPlayerTalkingOnRadio(GTANetworkAPI.Client client, string radioChannel, bool isSending)
        {
            Voice.SetPlayerSendingOnRadioChannel(client, radioChannel, isSending);
        }
        #endregion

        #region Commands
        [GTANetworkAPI.Command("setradiochannel")]
        public void OnSetRadioChannel(GTANetworkAPI.Client client, string radioChannel)
        {
            if (String.IsNullOrWhiteSpace(radioChannel))
                return;

            Voice.RemovePlayerRadioChannel(client);
            Voice.AddPlayerRadioChannel(client, radioChannel);

            client.TriggerEvent(SaltyShared.Event.Voice_SetRadioChannel, radioChannel);
        }

        [GTANetworkAPI.Command("leaveradiochannel")]
        public void OnLeaveRadioChannel(GTANetworkAPI.Client client)
        {
            Voice.RemovePlayerRadioChannel(client);

            client.TriggerEvent(SaltyShared.Event.Voice_SetRadioChannel, String.Empty);
        }
        #endregion

        #region Methods
        internal static string GetTeamSpeakName()
        {
            string name;
            List<GTANetworkAPI.Client> playerList = GTANetworkAPI.NAPI.Pools.GetAllPlayers();

            do
            {
                name = Guid.NewGuid().ToString().Replace("-", "");

                if (name.Length > 30)
                {
                    name = name.Remove(29, name.Length - 30);
                }
            }
            while (playerList.Any(p => p.TryGetSharedData(SaltyShared.SharedData.Voice_TeamSpeakName, out string tsName) && tsName == name));

            return name;
        }

        /// <summary>
        /// Returns all radio channels the client is currently in
        /// </summary>
        internal static List<string> GetRadioChannels(GTANetworkAPI.Client client)
        {
            List<string> radioChannels = new List<string>();

            foreach (KeyValuePair<string, List<GTANetworkAPI.Client>> radioChannel in Voice.RadioChannels)
            {
                if (radioChannel.Value.Contains(client))
                    radioChannels.Add(radioChannel.Key);
            }

            return radioChannels;
        }

        /// <summary>
        /// Adds player to a radio channel
        /// </summary>
        /// <param name="client"></param>
        /// <param name="radioChannel"></param>
        public static void AddPlayerRadioChannel(GTANetworkAPI.Client client, string radioChannel)
        {
            if (Voice.RadioChannels.ContainsKey(radioChannel) && Voice.RadioChannels[radioChannel].Contains(client))
            {
                return;
            }
            else if (Voice.RadioChannels.ContainsKey(radioChannel))
            {
                Voice.RadioChannels[radioChannel].Add(client);
            }
            else
            {
                Voice.RadioChannels.Add(radioChannel, new List<GTANetworkAPI.Client>{ client });
                Voice.PlayersTalkingOnRadioChannels.Add(radioChannel, new List<GTANetworkAPI.Client>());
            }
        }

        /// <summary>
        /// Removes player from all radio channels
        /// </summary>
        /// <param name="client"></param>
        public static void RemovePlayerRadioChannel(GTANetworkAPI.Client client)
        {
            foreach (string radioChannel in Voice.GetRadioChannels(client))
            {
                Voice.RemovePlayerRadioChannel(client, radioChannel);
            }
        }

        /// <summary>
        /// Removes player from a specific radio channel
        /// </summary>
        /// <param name="client"></param>
        /// <param name="radioChannel"></param>
        public static void RemovePlayerRadioChannel(GTANetworkAPI.Client client, string radioChannel)
        {
            if (Voice.PlayersTalkingOnRadioChannels.ContainsKey(radioChannel) && Voice.PlayersTalkingOnRadioChannels[radioChannel].Contains(client))
            {
                Voice.PlayersTalkingOnRadioChannels[radioChannel].Remove(client);

                if (Voice.PlayersTalkingOnRadioChannels[radioChannel].Count == 0)
                    Voice.PlayersTalkingOnRadioChannels.Remove(radioChannel);

                if (client.TryGetSharedData(SaltyShared.SharedData.Voice_TeamSpeakName, out string tsName))
                {
                    foreach (GTANetworkAPI.Client radioClient in Voice.RadioChannels[radioChannel])
                        radioClient.TriggerEvent(SaltyShared.Event.Voice_TalkingOnRadio, tsName, false);
                }
            }

            if (Voice.RadioChannels.ContainsKey(radioChannel) && Voice.RadioChannels[radioChannel].Contains(client))
            {
                Voice.RadioChannels[radioChannel].Remove(client);

                if (Voice.RadioChannels[radioChannel].Count == 0)
                {
                    Voice.RadioChannels.Remove(radioChannel);
                    Voice.PlayersTalkingOnRadioChannels.Remove(radioChannel);
                }   
            }
        }

        public static void SetPlayerSendingOnRadioChannel(GTANetworkAPI.Client client, string radioChannel, bool isSending)
        {
            if (!Voice.RadioChannels.ContainsKey(radioChannel) || !Voice.RadioChannels[radioChannel].Contains(client) || !client.TryGetSharedData(SaltyShared.SharedData.Voice_TeamSpeakName, out string tsName))
                return;

            if (isSending && !Voice.PlayersTalkingOnRadioChannels[radioChannel].Contains(client))
            {
                Voice.PlayersTalkingOnRadioChannels[radioChannel].Add(client);

                foreach (GTANetworkAPI.Client radioClient in Voice.RadioChannels[radioChannel])
                {
                    radioClient.TriggerEvent(SaltyShared.Event.Voice_TalkingOnRadio, tsName, true);
                }
            }
            else if (!isSending && Voice.PlayersTalkingOnRadioChannels[radioChannel].Contains(client))
            {
                Voice.PlayersTalkingOnRadioChannels[radioChannel].Remove(client);

                foreach (GTANetworkAPI.Client radioClient in Voice.RadioChannels[radioChannel])
                {
                    radioClient.TriggerEvent(SaltyShared.Event.Voice_TalkingOnRadio, tsName, false);
                }
            }
        }
        #endregion
    }
}
