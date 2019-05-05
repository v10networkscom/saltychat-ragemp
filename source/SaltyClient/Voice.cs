// Copyright (c) 2019 saltmine.de - https://github.com/saltminede

using System;
using System.Collections.Generic;

namespace SaltyClient
{
    public class Voice : RAGE.Events.Script
    {
        #region Props/Fields
        public static readonly float[] VolumeLevels = { 1.5f, 4f, 10f, 25f };

        private static RAGE.Ui.HtmlWindow _htmlWindow = default;

        private static bool _isConnected { get; set; }
        private static string _minimumVersion { get; set; }
        private static string _soundPack { get; set; }
        private static string _ingameChannel { get; set; }
        private static string _ingameChannelPassword { get; set; }
        private static bool _isIngame { get; set; }
        private static DateTime _nextUpdate = DateTime.Now;

        private static List<string> _playersInRange = new List<string>();
        private static List<string> _callPartner = new List<string>();
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
            RAGE.Events.Add(SaltyShared.Event.Voice_EstablishedCall, Voice.OnEstablishCall);
            RAGE.Events.Add(SaltyShared.Event.Voice_EndCall, Voice.OnEndCall);
            RAGE.Events.Add(SaltyShared.Event.Voice_SetRadioChannel, Voice.OnSetRadioChannel);
            RAGE.Events.Add(SaltyShared.Event.Voice_TalkingOnRadio, Voice.OnPlayerTalkingOnRadio);
            RAGE.Events.Add(SaltyShared.Event.Player_Died, Voice.OnPlayerDied);
            RAGE.Events.Add(SaltyShared.Event.Player_Revived, Voice.OnPlayerRevived);

            // Salty Chat Events
            RAGE.Events.Add("onTsPluginStateUpdate", Voice.OnPluginStateUpdate);
            RAGE.Events.Add("onTsPluginIngameUpdate", Voice.OnPluginIngameUpdate);
            RAGE.Events.Add("onTsPluginTalkingUpdate", Voice.OnPluginTalkingUpdate);
            RAGE.Events.Add("onTsPluginMicMutedUpdate", Voice.OnPluginMicMutedUpdate);
            RAGE.Events.Add("onTsPluginSoundMutedUpdate", Voice.OnPluginSoundMutedUpdate);
        }
        #endregion

        #region Events
        /// <summary>
        /// Trigger if plugin should be initialized
        /// </summary>
        /// <param name="args">args[0] - minimum version | args[1] - sound pack | args[2] - channelName | args[3] - channelPassword</param>
        public static void OnInitialize(object[] args)
        {
            Voice._minimumVersion = (string)args[0];
            Voice._soundPack = (string)args[1];
            Voice._ingameChannel = (string)args[2];
            Voice._ingameChannelPassword = (string)args[3];

            Voice._htmlWindow = new RAGE.Ui.HtmlWindow("package://Voice/SaltyAjax.html");

            Voice.IsEnabled = true;
        }

        /// <summary>
        /// Tell the plugin we have a new call partner
        /// </summary>
        /// <param name="args">args[0] - playerName</param>
        private static void OnEstablishCall(object[] args)
        {
            string playerName = (string)args[0];

            Voice._callPartner.Add(playerName);

            Voice.ExecuteCommand("onPhone", playerName);
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

                Voice.ExecuteCommand("offPhone", playerName);

                Voice._callPartner.Remove(playerName);
            }
            else
            {
                foreach (string playerName in Voice._callPartner)
                {
                    Voice.ExecuteCommand("offPhone", playerName);
                }

                Voice._callPartner.Clear();
            }
        }

        /// <summary>
        /// Sets players radio channel
        /// </summary>
        /// <param name="args">args[0] - radio channel</param>
        private static void OnSetRadioChannel(object[] args)
        {
            Voice._radioChannel = (string)args[0];
        }

        /// <summary>
        /// When someone is talking on our radio channel
        /// </summary>
        /// <param name="args">args[0] - playerName | args[1] - isOnRadio</param>
        private static void OnPlayerTalkingOnRadio(object[] args)
        {
            string playerName = (string)args[0];
            bool isOnRadio = (bool)args[1];

            if (RAGE.Elements.Player.LocalPlayer.TryGetSharedData(SaltyShared.SharedData.Voice_TeamSpeakName, out string tsName))
            {
                if (isOnRadio)
                    Voice.ExecuteCommand("onRadio", playerName);
                else
                    Voice.ExecuteCommand("offRadio", playerName);
            }
        }

        /// <summary>
        /// Tell plugin the player is dead, so we don't hear him anymore
        /// </summary>
        /// <param name="args">[0] - playerName</param>
        private static void OnPlayerDied(object[] args)
        {
            Voice.ExecuteCommand("isDead", args[0]);
        }

        /// <summary>
        /// Tell plugin the player is alive again, se we can hear him
        /// </summary>
        /// <param name="args">[0] - playerName</param>
        public static void OnPlayerRevived(object[] args)
        {
            Voice.ExecuteCommand("isAlive", args[0]);
        }

        private static void OnTick(List<RAGE.Events.TickNametagData> nametags)
        {
            if (Voice.IsReady && DateTime.Now > Voice._nextUpdate)
            {
                Voice.CalculateProximityVoice();

                Voice._nextUpdate = DateTime.Now.AddMilliseconds(300);
            }

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
        }
        #endregion

        #region Plugin Events
        /// <summary>
        /// Plugin state changed
        /// </summary>
        /// <param name="args">[0] - state | [1] - version</param>
        public static void OnPluginStateUpdate(object[] args)
        {
            Voice._isConnected = (bool)args[0];
            string version = (string)args[1];

            if (Voice._isConnected)
            {
                if (Voice.IsVersionAccepted(version))
                {
                    Voice.InitiatePlugin();
                }
                else
                {
                    RAGE.Events.CallRemote("notSupportedPluginVersion");
                }
            }
        }

        /// <summary>
        /// Plugin ingame state update
        /// </summary>
        /// <param name="args">[0] - state</param>
        public static void OnPluginIngameUpdate(object[] args)
        {
            Voice._isIngame = (bool)args[0];

            if (!Voice._isIngame)
            {
                Voice._playersInRange.Clear();

                Voice.InitiatePlugin();
            }
        }

        /// <summary>
        /// Player is talking
        /// </summary>
        /// <param name="args">[0] - isTalking</param>
        public static void OnPluginTalkingUpdate(object[] args)
        {
            Voice.IsTalking = (bool)args[0];

            Voice.OnTalkingStateChange?.Invoke(new SoundEventArgs());
            Voice.OnSoundStateChange?.Invoke(new SoundEventArgs());
        }

        /// <summary>
        /// If player mutes/unmutes mic
        /// </summary>
        /// <param name="args">[0] - isMicMuted</param>
        public static void OnPluginMicMutedUpdate(object[] args)
        {
            Voice.IsMicrophoneMuted = (bool)args[0];

            Voice.OnMicrophoneMuteStateChange?.Invoke(new SoundEventArgs());
            Voice.OnSoundStateChange?.Invoke(new SoundEventArgs());
        }

        /// <summary>
        /// If player is talking
        /// </summary>
        /// <param name="args">[0] - isSoundMuted</param>
        public static void OnPluginSoundMutedUpdate(object[] args)
        {
            Voice.IsSoundMuted = (bool)args[0];

            Voice.OnSoundMuteStateChange?.Invoke(new SoundEventArgs());
            Voice.OnSoundStateChange?.Invoke(new SoundEventArgs());
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

            if (Voice._ingameChannelPassword == default)
            {
                Voice.ExecuteCommand("initiate", tsName, Voice._soundPack, Voice._ingameChannel);
            }
            else
            {
                Voice.ExecuteCommand("initiate", tsName, Voice._soundPack, Voice._ingameChannel, Voice._ingameChannelPassword);
            }
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

            Voice.ExecuteCommand("playSound", fileName, loop, handle);
        }

        /// <summary>
        /// Stop and dispose the sound
        /// </summary>
        /// <param name="handle">filename or handle of the sound</param>
        public static void StopSound(string handle)
        {
            Voice.ExecuteCommand("stopSound", handle);
        }

        private static void CalculateProximityVoice()
        {
            RAGE.Vector3 lPlayerPosition = RAGE.Elements.Player.LocalPlayer.Position;
            RAGE.Vector3 lPlayerCamPosition = RAGE.Game.Cam.GetGameplayCamRot(0);
            double camRotation = Math.PI / 180 * (lPlayerCamPosition.Z * -1);

            Voice.ExecuteCommand("keepalive");

            foreach (var nPlayer in RAGE.Elements.Entities.Players.All)
            {
                if (nPlayer == RAGE.Elements.Player.LocalPlayer ||
                    !nPlayer.TryGetSharedData(SaltyShared.SharedData.Voice_TeamSpeakName, out string nPlayerName))
                    continue;

                if (!nPlayer.TryGetSharedData(SaltyShared.SharedData.Voice_VoiceRange, out float nPlayerVoiceRange))
                    nPlayerVoiceRange = Voice.VolumeLevels[2];

                RAGE.Vector3 nPlayerPosition = nPlayer.Position;
                float distanceToPlayer = RAGE.Game.Utils.Vdist(lPlayerPosition.X, lPlayerPosition.Y, lPlayerPosition.Z, nPlayerPosition.X, nPlayerPosition.Y, nPlayerPosition.Z);

                if (distanceToPlayer <= nPlayerVoiceRange)
                {
                    if (!Voice._playersInRange.Contains(nPlayerName))
                    {
                        Voice._playersInRange.Add(nPlayerName);
                        Voice.ExecuteCommand("inRange", nPlayerName);
                    }

                    RAGE.Vector3 subtractedPosition = new RAGE.Vector3(nPlayerPosition.X - lPlayerPosition.X, nPlayerPosition.Y - lPlayerPosition.Y, nPlayerPosition.Z - lPlayerPosition.Z);

                    double x = (subtractedPosition.X * Math.Cos(camRotation) - subtractedPosition.Y * Math.Sin(camRotation)) * 10 / nPlayerVoiceRange;
                    double y = (subtractedPosition.X * Math.Sin(camRotation) + subtractedPosition.Y * Math.Cos(camRotation)) * 10 / nPlayerVoiceRange;

                    Voice.ExecuteCommand("updatePosition", nPlayerName, Math.Round(x * 1000) / 1000, Math.Round(y * 1000) / 1000, 0);
                }
                else if (_playersInRange.Contains(nPlayerName))
                {
                    Voice._playersInRange.Remove(nPlayerName);
                    Voice.ExecuteCommand("outRange", nPlayerName);
                }

            }
        }

        /// <summary>
        /// Checks if given version is the same or higher then <see cref="Voice._minimumVersion"/>
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        public static bool IsVersionAccepted(string version)
        {
            if (String.IsNullOrWhiteSpace(Voice._minimumVersion))
                return true;

            try
            {
                string[] minimumVersionArray = Voice._minimumVersion.Split('.');
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
        private static void ExecuteCommand(string command, params object[] parameters)
        {
            if (!Voice.IsEnabled || !Voice.IsReady)
                return;

            string parameterString = String.Empty;

            for (int i = 0; i < parameters.Length; i++)
            {
                if (i + 1 < parameters.Length)
                    parameterString += $"{parameters[i]}&";
                else
                    parameterString += $"{parameters[i]}/";
            }

            Voice._htmlWindow.ExecuteJs($"addCommand('{command}/{parameterString}')");
        }
        #endregion
    }
}
