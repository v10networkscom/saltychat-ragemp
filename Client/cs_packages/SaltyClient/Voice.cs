// Copyright (c) 2019 saltmine.de - https://github.com/saltminede

using System;
using System.Collections.Generic;

namespace SaltyClient
{
    public class Voice : RAGE.Events.Script
    {
        #region Props/Fields
        private static RAGE.Ui.HtmlWindow _htmlWindow = default;

        private static bool _isConnected { get; set; }
        private static string _serverUniqueIdentifier { get; set; }
        private static string _requiredBranch { get; set; }
        private static string _minimumVersion { get; set; }
        private static string _soundPack { get; set; }
        private static ulong _ingameChannel { get; set; }
        private static string _ingameChannelPassword { get; set; }
        private static bool _isIngame { get; set; }
        private static DateTime _nextUpdate = DateTime.Now;

        private static List<string> _deadPlayers = new List<string>();
        private static List<string> _callPartner = new List<string>();
        private static List<string> _radioSender = new List<string>();
        private static string _radioChannel { get; set; }

        public static bool IsEnabled { get; private set; } = false;
        public static bool IsConnected => Voice._htmlWindow != default && Voice._isConnected;
        public static bool IsReady => Voice.IsConnected && Voice._isIngame;

        public static bool IsTalking { get; private set; }
        public static bool IsMicrophoneMuted { get; private set; }
        public static bool IsSoundMuted { get; private set; }
        #endregion

        #region Voice Events
        public static event OnSoundStateChangeDelegate OnSoundStateChange;
        public delegate void OnSoundStateChangeDelegate(SoundEventArgs soundEventArgs);

        public static event OnTalkingStateChangeDelegate OnTalkingStateChange;
        public delegate void OnTalkingStateChangeDelegate(SoundEventArgs soundEventArgs);

        public static event OnMicrophoneMuteStateChangeDelegate OnMicrophoneMuteStateChange;
        public delegate void OnMicrophoneMuteStateChangeDelegate(SoundEventArgs soundEventArgs);

        public static event OnSoundMuteStateChangeDelegate OnSoundMuteStateChange;
        public delegate void OnSoundMuteStateChangeDelegate(SoundEventArgs soundEventArgs);
        #endregion

        #region CTOR
        public Voice()
        {
            // RAGEMP Events
            RAGE.Events.Tick += Voice.OnTick;

            // Project Events
            RAGE.Events.Add(SaltyShared.Event.Voice_Initialize, Voice.OnInitialize);
            RAGE.Events.Add(SaltyShared.Event.Player_Disconnected, Voice.OnPlayerDisconnect);
            RAGE.Events.Add(SaltyShared.Event.Voice_IsTalking, Voice.OnPlayerTalking);
            RAGE.Events.Add(SaltyShared.Event.Voice_EstablishedCall, Voice.OnEstablishCall);
            RAGE.Events.Add(SaltyShared.Event.Voice_EndCall, Voice.OnEndCall);
            RAGE.Events.Add(SaltyShared.Event.Voice_SetRadioChannel, Voice.OnSetRadioChannel);
            RAGE.Events.Add(SaltyShared.Event.Voice_TalkingOnRadio, Voice.OnPlayerTalkingOnRadio);
            RAGE.Events.Add(SaltyShared.Event.Player_Died, Voice.OnPlayerDied);
            RAGE.Events.Add(SaltyShared.Event.Player_Revived, Voice.OnPlayerRevived);

            // Salty Chat Events
            RAGE.Events.Add("SaltyChat_OnConnected", Voice.OnPluginConnected);
            RAGE.Events.Add("SaltyChat_OnDisconnected", Voice.OnPluginDisconnected);
            RAGE.Events.Add("SaltyChat_OnMessage", Voice.OnPluginMessage);
            RAGE.Events.Add("SaltyChat_OnError", Voice.OnPluginError);
        }
        #endregion

        #region Events
        /// <summary>
        /// Trigger if plugin should be initialized
        /// </summary>
        /// <param name="args">args[0] - server unique identifier | args[1] - required branch | args[2] - minimum version | args[3] - sound pack | args[4] - channelName | args[5] - channelPassword</param>
        public static void OnInitialize(object[] args)
        {
            Voice._serverUniqueIdentifier = (string)args[0];
            Voice._requiredBranch = (string)args[1];
            Voice._minimumVersion = (string)args[2];
            Voice._soundPack = (string)args[3];
            Voice._ingameChannel = Convert.ToUInt64((string)args[4]);
            Voice._ingameChannelPassword = (string)args[5];

            Voice.IsEnabled = true;

            Voice._htmlWindow = new RAGE.Ui.HtmlWindow("package://Voice/SaltyWebSocket.html");
            //Voice._htmlWindow.Active = false;
        }

        /// <summary>
        /// Remove a disconnected player
        /// </summary>
        /// <param name="args">args[0] - playerName</param>
        private static void OnPlayerDisconnect(object[] args)
        {
            string playerName = (string)args[0];

            if (Voice._deadPlayers.Contains(playerName))
                Voice._deadPlayers.Remove(playerName);

            if (Voice._callPartner.Contains(playerName))
                Voice._callPartner.Remove(playerName);

            if (Voice._radioSender.Contains(playerName))
                Voice._radioSender.Remove(playerName);

            Voice.ExecuteCommand(new PluginCommand(Command.RemovePlayer, Voice._serverUniqueIdentifier, new PlayerState(playerName)));
        }

        /// <summary>
        /// A player starts/stops talking
        /// </summary>
        /// <param name="args">args[0] - <see cref="RAGE.Elements.Player"/> | args[1] - <see cref="bool"/> isTalking</param>
        private static void OnPlayerTalking(object[] args)
        {
#warning There seems to be an issue where the "client"-object is not correctly referenced on the client, remove workaround if the issue is resolved

            string playerName = (string)args[0];
            bool isTalking = (bool)args[1];

            foreach (RAGE.Elements.Player player in RAGE.Elements.Entities.Players.All)
            {
                if (!player.TryGetSharedData(SaltyShared.SharedData.Voice_TeamSpeakName, out string tsName) || tsName != playerName)
                    continue;

                if (isTalking)
                    player.PlayFacialAnim("mic_chatter", "mp_facial");
                else
                    player.PlayFacialAnim("mood_normal_1", "facials@gen_male@variations@normal");

                break;
            }

            /*
            RAGE.Elements.Player player = (RAGE.Elements.Player)args[0];
            bool isTalking = (bool)args[1];
            
            if (!player.Exists)
                return;

            if (isTalking)
                player.PlayFacialAnim("mic_chatter", "mp_facial");
            else
                player.PlayFacialAnim("mood_normal_1", "facials@gen_male@variations@normal");
            */
        }

        /// <summary>
        /// Tell the plugin we have a new call partner
        /// </summary>
        /// <param name="args">args[0] - playerName</param>
        private static void OnEstablishCall(object[] args)
        {
            string playerName = (string)args[0];

            Voice._callPartner.Add(playerName);
        }

        /// <summary>
        /// Tell the plugin to end the call
        /// </summary>
        /// <param name="args">if args[0] doesn't specify a playerName, it will end the call to all players</param>
        private static void OnEndCall(object[] args)
        {
            if (args != default && args.Length == 1)
            {
                string playerName = (string)args[0];

                if (!Voice._callPartner.Contains(playerName))
                    return;

                Voice._callPartner.Remove(playerName);
            }
            else
            {
                Voice._callPartner.Clear();
            }
        }

        /// <summary>
        /// Sets players radio channel
        /// </summary>
        /// <param name="args">args[0] - radio channel</param>
        private static void OnSetRadioChannel(object[] args)
        {
            string radioChannel = (string)args[0];

            if (String.IsNullOrWhiteSpace(radioChannel))
            {
                Voice._radioChannel = default;
                Voice.PlaySound("leaveRadioChannel", false, "radio");
            }
            else
            {
                Voice._radioChannel = radioChannel;
                Voice.PlaySound("enterRadioChannel", false, "radio");
            }   
        }

        /// <summary>
        /// When someone is talking on our radio channel
        /// </summary>
        /// <param name="args">args[0] - playerName | args[1] - isOnRadio</param>
        private static void OnPlayerTalkingOnRadio(object[] args)
        {
            string playerName = (string)args[0];
            bool isOnRadio = (bool)args[1];

            if (RAGE.Elements.Player.LocalPlayer.TryGetSharedData(SaltyShared.SharedData.Voice_TeamSpeakName, out string tsName) && tsName == playerName)
            {
                Voice.PlaySound("selfMicClick", false, "radio");
            }
            else
            {
                if (isOnRadio && !Voice._radioSender.Contains(playerName))
                {
                    Voice._radioSender.Add(playerName);
                    Voice.PlaySound("onMicClick", false, "radio");
                }
                else if (!isOnRadio && Voice._radioSender.Contains(playerName))
                {
                    Voice._radioSender.Remove(playerName);
                    Voice.PlaySound("offMicClick", false, "radio");
                }
            }
        }

        /// <summary>
        /// Tell plugin the player is dead, so we don't hear him anymore
        /// </summary>
        /// <param name="args">[0] - playerName</param>
        private static void OnPlayerDied(object[] args)
        {
            string playerName = (string)args[0];

            if (!Voice._deadPlayers.Contains(playerName))
                Voice._deadPlayers.Add(playerName);
        }

        /// <summary>
        /// Tell plugin the player is alive again, se we can hear him
        /// </summary>
        /// <param name="args">[0] - playerName</param>
        public static void OnPlayerRevived(object[] args)
        {
            string playerName = (string)args[0];

            if (Voice._deadPlayers.Contains(playerName))
                Voice._deadPlayers.Remove(playerName);
        }

        private static void OnTick(List<RAGE.Events.TickNametagData> nametags)
        {
            // Calculate player states
            if (Voice.IsReady && DateTime.Now > Voice._nextUpdate)
            {
                Voice.PlayerStateUpdate();

                Voice._nextUpdate = DateTime.Now.AddMilliseconds(300);
            }

            // Lets the player talk on his radio channel with "N"
            if (Voice._radioChannel != default)
            {
                if (RAGE.Game.Pad.IsControlJustPressed(1, (int)RAGE.Game.Control.PushToTalk))
                {
                    RAGE.Events.CallRemote(SaltyShared.Event.Voice_TalkingOnRadio, Voice._radioChannel, true);
                }
                else if (RAGE.Game.Pad.IsControlJustReleased(1, (int)RAGE.Game.Control.PushToTalk))
                {
                    RAGE.Events.CallRemote(SaltyShared.Event.Voice_TalkingOnRadio, Voice._radioChannel, false);
                }
            }

            // Lets the player change his voice range with "^"
            if (RAGE.Game.Pad.IsControlJustPressed(1, (int)RAGE.Game.Control.EnterCheatCode))
            {
                Voice.ToggleVoiceRange();
            }
        }
        #endregion

        #region Plugin Events
        /// <summary>
        /// Plugin connected to WebSocket
        /// </summary>
        /// <param name="args"></param>
        public static void OnPluginConnected(object[] args)
        {
            Voice._isConnected = true;

            Voice.InitiatePlugin();
        }

        /// <summary>
        /// Plugin disconnected from WebSocket
        /// </summary>
        /// <param name="args"></param>
        public static void OnPluginDisconnected(object[] args)
        {
            Voice._isConnected = false;
        }

        /// <summary>
        /// Plugin state update
        /// </summary>
        /// <param name="args">[0] - <see cref="PluginCommand"/> as json</param>
        public static void OnPluginMessage(object[] args)
        {
            PluginCommand pluginCommand = PluginCommand.Deserialize((string)args[0]);

            if (pluginCommand.Command == Command.Ping && Voice._nextUpdate.AddSeconds(1) > DateTime.Now)
            {
                Voice.ExecuteCommand(new PluginCommand(Voice._serverUniqueIdentifier));
                return;
            }

            if (!pluginCommand.TryGetState(out PluginState pluginState))
                return;

            if (pluginState.IsReady != Voice._isIngame)
            {
                if (!Voice.IsVersionAccepted(pluginState.UpdateBranch, pluginState.Version))
                {
                    RAGE.Events.CallRemote(SaltyShared.Event.Voice_RejectedVersion, pluginState.UpdateBranch, pluginState.Version);
                    return;
                }

                Voice._isIngame = pluginState.IsReady;
            }

            bool hasTalkingChanged = false;
            bool hasMicMutedChanged = false;
            bool hasSoundMutedChanged = false;

            if (pluginState.IsTalking != Voice.IsTalking)
            {
                Voice.IsTalking = pluginState.IsTalking;
                hasTalkingChanged = true;

                RAGE.Events.CallRemote(SaltyShared.Event.Voice_IsTalking, Voice.IsTalking);
            }

            if (pluginState.IsMicrophoneMuted != Voice.IsMicrophoneMuted)
            {
                Voice.IsMicrophoneMuted = pluginState.IsMicrophoneMuted;
                hasMicMutedChanged = true;
            }

            if (pluginState.IsSoundMuted != Voice.IsSoundMuted)
            {
                Voice.IsSoundMuted = pluginState.IsSoundMuted;
                hasSoundMutedChanged = true;
            }

            if (hasTalkingChanged)
                Voice.OnTalkingStateChange?.Invoke(new SoundEventArgs());

            if (hasMicMutedChanged)
                Voice.OnMicrophoneMuteStateChange?.Invoke(new SoundEventArgs());

            if (hasSoundMutedChanged)
                Voice.OnSoundMuteStateChange?.Invoke(new SoundEventArgs());

            if (hasTalkingChanged || hasMicMutedChanged || hasSoundMutedChanged)
                Voice.OnSoundStateChange?.Invoke(new SoundEventArgs());
        }

        /// <summary>
        /// Plugin error
        /// </summary>
        /// <param name="args">[0] - <see cref="PluginCommand"/> as json</param>
        public static void OnPluginError(object[] args)
        {
            PluginCommand pluginCommand = PluginCommand.Deserialize((string)args[0]);
            
            if (pluginCommand.TryGetError(out PluginError pluginError))
                RAGE.Chat.Output($"Error: {pluginError.Error} - Message: {pluginError.Message}");
        }
        #endregion

        #region Methods
        /// <summary>
        /// Initiates the plugin
        /// </summary>
        private static void InitiatePlugin()
        {
            if (!RAGE.Elements.Player.LocalPlayer.TryGetSharedData(SaltyShared.SharedData.Voice_TeamSpeakName, out string tsName) || Voice._ingameChannel == default)
                return;
            
            Voice.ExecuteCommand(new PluginCommand(Command.Initiate, new GameInstance(Voice._serverUniqueIdentifier, tsName, Voice._ingameChannel, Voice._ingameChannelPassword == default ? String.Empty : Voice._ingameChannelPassword, Voice._soundPack)));
        }

        /// <summary>
        /// Plays a file from soundpack specified in <see cref="Voice._soundPack"/>
        /// </summary>
        /// <param name="fileName">filename (without .wav) of the soundfile</param>
        /// <param name="loop">use <see cref="true"/> to let the plugin loop the sound</param>
        /// <param name="handle">use your own handle instead of the filename, so you can play the sound multiple times</param>
        public static void PlaySound(string fileName, bool loop = false, string handle = null)
        {
            if (String.IsNullOrWhiteSpace(handle))
                handle = fileName;

            Voice.ExecuteCommand(new PluginCommand(Command.PlaySound, Voice._serverUniqueIdentifier, new Sound(fileName, loop, handle)));
        }

        /// <summary>
        /// Stops and dispose the sound
        /// </summary>
        /// <param name="handle">filename or handle of the sound</param>
        public static void StopSound(string handle)
        {
            Voice.ExecuteCommand(new PluginCommand(Command.StopSound, Voice._serverUniqueIdentifier, new Sound(handle)));
        }

        /// <summary>
        /// Sends the plugin an update on all players
        /// </summary>
        private static void PlayerStateUpdate()
        {
            RAGE.Vector3 playerPosition = RAGE.Elements.Player.LocalPlayer.Position;

            Voice.ExecuteCommand(
                new PluginCommand(
                    Command.SelfStateUpdate,
                    Voice._serverUniqueIdentifier,
                    new PlayerState(
                        playerPosition,
                        RAGE.Game.Cam.GetGameplayCamRot(0).Z,
                        RAGE.Game.Zone.GetZoneScumminess(RAGE.Game.Zone.GetZoneAtCoords(playerPosition.X, playerPosition.Y, playerPosition.Z))
                    )
                )
            );

            foreach (var nPlayer in RAGE.Elements.Entities.Players.All)
            {
                if (nPlayer == RAGE.Elements.Player.LocalPlayer ||
                    !nPlayer.TryGetSharedData(SaltyShared.SharedData.Voice_TeamSpeakName, out string nPlayerName))
                    continue;

                if (!nPlayer.TryGetSharedData(SaltyShared.SharedData.Voice_VoiceRange, out float nPlayerVoiceRange))
                    nPlayerVoiceRange = SaltyShared.SharedData.VoiceRanges[2];

                RAGE.Vector3 nPlayerPosition = nPlayer.Position;

                Voice.ExecuteCommand(
                    new PluginCommand(
                        Command.PlayerStateUpdate,
                        Voice._serverUniqueIdentifier,
                        new PlayerState(
                            nPlayerName,
                            nPlayerPosition,
                            nPlayerVoiceRange,
                            RAGE.Game.Zone.GetZoneScumminess(RAGE.Game.Zone.GetZoneAtCoords(nPlayerPosition.X, nPlayerPosition.Y, nPlayerPosition.Z)),
                            Voice._callPartner.Contains(nPlayerName),
                            Voice._radioSender.Contains(nPlayerName),
                            !Voice._deadPlayers.Contains(nPlayerName)
                        )
                    )
                );
            }
        }

        /// <summary>
        /// Toggles voice range through <see cref="Voice.VoiceRanges"/>
        /// </summary>
        public static void ToggleVoiceRange()
        {
            if (!RAGE.Elements.Player.LocalPlayer.TryGetSharedData(SaltyShared.SharedData.Voice_VoiceRange, out float voiceRange))
            {
                RAGE.Events.CallRemote(SaltyShared.Event.Voice_SetVoiceRange, SaltyShared.SharedData.VoiceRanges[1]);
                return;
            }

            int index = Array.IndexOf(SaltyShared.SharedData.VoiceRanges, voiceRange);

            if (index < 0)
            {
                RAGE.Events.CallRemote(SaltyShared.Event.Voice_SetVoiceRange, SaltyShared.SharedData.VoiceRanges[1]);
            }
            else if (index + 1 >= SaltyShared.SharedData.VoiceRanges.Length)
            {
                RAGE.Events.CallRemote(SaltyShared.Event.Voice_SetVoiceRange, SaltyShared.SharedData.VoiceRanges[0]);
            }
            else
            {
                RAGE.Events.CallRemote(SaltyShared.Event.Voice_SetVoiceRange, SaltyShared.SharedData.VoiceRanges[index + 1]);
            }
        }

        /// <summary>
        /// Checks if given version is the same or higher then <see cref="Voice._minimumVersion"/>
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        public static bool IsVersionAccepted(string branch, string version)
        {
            if (String.IsNullOrWhiteSpace(Voice._requiredBranch) && String.IsNullOrWhiteSpace(Voice._minimumVersion))
                return true;
            else if (Voice._requiredBranch != branch)
                return false;

            try
            {
                string[] minimumVersionArray = Voice._minimumVersion.Split('.');
                string[] versionArray = version.Split('.');

                // If the version contains any additions (like "0.2 Testing"), we can't really compare them > discard
                if (versionArray[versionArray.Length - 1].Contains(' '))
                {
                    versionArray[versionArray.Length - 1] = versionArray[versionArray.Length - 1].Split(' ')[0];
                }

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

                return true;
            }
            catch
            {
                // do nothing
            }

            return false;
        }
        #endregion

        #region Helper
        private static void ExecuteCommand(PluginCommand pluginCommand)
        {
            if (!Voice.IsEnabled || !Voice.IsConnected || pluginCommand == default)
                return;
            
            Voice._htmlWindow.ExecuteJs($"runCommand('{pluginCommand.Serialize()}')");
        }
        #endregion
    }
}
