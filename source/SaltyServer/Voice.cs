// Copyright (c) 2019 saltmine.de - https://github.com/saltminede

using System;
using System.Collections.Generic;

namespace SaltyServer
{
    public class Voice : GTANetworkAPI.Script
    {
        #region Props
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
            Voice.MinimumPluginVersion = GTANetworkAPI.NAPI.Resource.GetSetting<string>(this, "MinimumPluginVersion");
            Voice.SoundPack = GTANetworkAPI.NAPI.Resource.GetSetting<string>(this, "SoundPack");
            Voice.IngameChannel = GTANetworkAPI.NAPI.Resource.GetSetting<string>(this, "IngameChannel");
            Voice.IngameChannelPassword = GTANetworkAPI.NAPI.Resource.GetSetting<string>(this, "IngameChannelPassword");
        }

        [GTANetworkAPI.ServerEvent(GTANetworkAPI.Event.PlayerConnected)]
        public void OnPlayerConnected(GTANetworkAPI.Client client)
        {
            client.SetSharedData(SaltyShared.SharedData.Voice_TeamSpeakName, Guid.NewGuid().ToString());
            client.SetSharedData(SaltyShared.SharedData.Voice_VoiceRange, 10f);

            client.TriggerEvent(SaltyShared.Event.Voice_Initialize, Voice.MinimumPluginVersion, Voice.SoundPack, Voice.IngameChannel, Voice.IngameChannel);
        }

        [GTANetworkAPI.ServerEvent(GTANetworkAPI.Event.PlayerDisconnected)]
        public void OnPlayerDisconnected(GTANetworkAPI.Client client)
        {
            Voice.RemovePlayerRadioChannel(client);
        }
        #endregion

        #region Remote Events
        [GTANetworkAPI.RemoteEvent(SaltyShared.Event.Voice_TalkingOnRadio)]
        public void OnPlayerTalkingOnRadio(GTANetworkAPI.Client client, string radioChannel, bool isSending)
        {
            Voice.SetPlayerSendingOnRadioChannel(client, radioChannel, isSending);
        }
        #endregion

        #region Commands
        [GTANetworkAPI.Command("setvoicerange")]
        public void OnSetVoiceRange(GTANetworkAPI.Client client, float voiceRange)
        {
            client.SetSharedData(SaltyShared.SharedData.Voice_VoiceRange, voiceRange);
        }

        [GTANetworkAPI.Command("setradiochannel")]
        public void OnSetRadioChannel(GTANetworkAPI.Client client, string radioChannel)
        {
            if (String.IsNullOrWhiteSpace(radioChannel))
                return;

            Voice.RemovePlayerRadioChannel(client);
            Voice.AddPlayerRadioChannel(client, radioChannel);

            client.TriggerEvent(SaltyShared.Event.Voice_SetRadioChannel, radioChannel);
        }
        #endregion

        #region Methods
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

                foreach (GTANetworkAPI.Client radioClient in Voice.RadioChannels[radioChannel])
                {
                    radioClient.TriggerEvent(SaltyShared.Event.Voice_TalkingOnRadio, client.GetSharedData(SaltyShared.SharedData.Voice_TeamSpeakName), false);
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
            if (!Voice.RadioChannels.ContainsKey(radioChannel) || !Voice.RadioChannels[radioChannel].Contains(client))
                return;

            if (isSending && !Voice.PlayersTalkingOnRadioChannels[radioChannel].Contains(client))
            {
                Voice.PlayersTalkingOnRadioChannels[radioChannel].Add(client);

                foreach (GTANetworkAPI.Client radioClient in Voice.RadioChannels[radioChannel])
                {
                    radioClient.TriggerEvent(SaltyShared.Event.Voice_TalkingOnRadio, client.GetSharedData(SaltyShared.SharedData.Voice_TeamSpeakName), true);
                }
            }
            else if (!isSending && Voice.PlayersTalkingOnRadioChannels[radioChannel].Contains(client))
            {
                Voice.PlayersTalkingOnRadioChannels[radioChannel].Remove(client);

                foreach (GTANetworkAPI.Client radioClient in Voice.RadioChannels[radioChannel])
                {
                    radioClient.TriggerEvent(SaltyShared.Event.Voice_TalkingOnRadio, client.GetSharedData(SaltyShared.SharedData.Voice_TeamSpeakName), false);
                }
            }
        }
        #endregion
    }
}
