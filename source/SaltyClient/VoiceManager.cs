// Copyright (c) 2019 saltmine.de - https://github.com/saltminede

using System;
using System.Linq;
using System.Collections.Generic;
using RAGE.Elements;

namespace SaltyClient
{
    public class VoiceManager : RAGE.Events.Script
    {
        #region Props/Fields
        public static string ServerUniqueIdentifier { get; private set; }
        public static string SoundPack { get; private set; }
        public static ulong IngameChannel { get; private set; }
        public static string IngameChannelPassword { get; private set; }
        public static string TeamSpeakName { get; private set; }
        public static float VoiceRange { get; private set; }
        public static string RadioChannel { get; private set; }

        private static RAGE.Ui.HtmlWindow _htmlWindow = default;
        private static bool _isConnected { get; set; }
        private static bool _isIngame { get; set; }
        private static DateTime _nextUpdate = DateTime.Now;

        public static VoiceClient[] VoiceClients => VoiceManager._voiceClients.Values.ToArray();
        private static Dictionary<ushort, VoiceClient> _voiceClients = new Dictionary<ushort, VoiceClient>();

        public static bool IsEnabled { get; private set; } = false;
        public static bool IsConnected => VoiceManager._htmlWindow != default && VoiceManager._isConnected;
        public static bool IsReady => VoiceManager.IsConnected && VoiceManager._isIngame;

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
        public VoiceManager()
        {
            // RAGEMP Events
            RAGE.Events.Tick += VoiceManager.OnTick;

            // Project Events
            RAGE.Events.Add(SaltyShared.Event.SaltyChat_Initialize, VoiceManager.OnInitialize);
            RAGE.Events.Add(SaltyShared.Event.SaltyChat_UpdateClient, VoiceManager.OnUpdateVoiceClient);
            RAGE.Events.Add(SaltyShared.Event.SaltyChat_Disconnected, VoiceManager.OnPlayerDisconnect);
            RAGE.Events.Add(SaltyShared.Event.SaltyChat_IsTalking, VoiceManager.OnPlayerTalking);

            RAGE.Events.Add(SaltyShared.Event.SaltyChat_PlayerDied, VoiceManager.OnPlayerDied);
            RAGE.Events.Add(SaltyShared.Event.SaltyChat_PlayerRevived, VoiceManager.OnPlayerRevived);

            RAGE.Events.Add(SaltyShared.Event.SaltyChat_EstablishedCall, VoiceManager.OnEstablishCall);
            RAGE.Events.Add(SaltyShared.Event.SaltyChat_EstablishedCallRelayed, VoiceManager.OnEstablishCallRelayed);
            RAGE.Events.Add(SaltyShared.Event.SaltyChat_EndCall, VoiceManager.OnEndCall);

            RAGE.Events.Add(SaltyShared.Event.SaltyChat_SetRadioChannel, VoiceManager.OnSetRadioChannel);
            RAGE.Events.Add(SaltyShared.Event.SaltyChat_IsSending, VoiceManager.OnPlayerIsSending);
            RAGE.Events.Add(SaltyShared.Event.SaltyChat_IsSendingRelayed, VoiceManager.OnPlayerIsSendingRelayed);
            RAGE.Events.Add(SaltyShared.Event.SaltyChat_UpdateRadioTowers, VoiceManager.OnUpdateRadioTowers);

            // Salty Chat Events
            RAGE.Events.Add("SaltyChat_OnConnected", VoiceManager.OnPluginConnected);
            RAGE.Events.Add("SaltyChat_OnDisconnected", VoiceManager.OnPluginDisconnected);
            RAGE.Events.Add("SaltyChat_OnMessage", VoiceManager.OnPluginMessage);
            RAGE.Events.Add("SaltyChat_OnError", VoiceManager.OnPluginError);
        }
        #endregion

        #region Events
        /// <summary>
        /// Trigger if plugin should be initialized
        /// </summary>
        /// <param name="args">args[0] - teamSpeakName | args[1] - serverUniqueIdentifier | args[2] - soundPack | args[3] - channelId | args[4] - channelPassword | </param>
        public static void OnInitialize(object[] args)
        {
            VoiceManager.TeamSpeakName = (string)args[0];
            VoiceManager.ServerUniqueIdentifier = (string)args[1];
            VoiceManager.SoundPack = (string)args[2];
            VoiceManager.IngameChannel = Convert.ToUInt64((string)args[3]);
            VoiceManager.IngameChannelPassword = (string)args[4];

            VoiceManager.IsEnabled = true;

            VoiceManager._htmlWindow = new RAGE.Ui.HtmlWindow("package://Voice/SaltyWebSocket.html");
            //VoiceManager._htmlWindow.Active = false;
        }

        /// <summary>
        /// Update Voice Client
        /// </summary>
        /// <param name="args">args[0] - handle | args[1] - teamSpeakName | args[2] voiceRange</param>
        private static void OnUpdateVoiceClient(object[] args)
        {
            ushort handle = Convert.ToUInt16(args[0]);
            string teamSpeakName = (string)args[1];
            float voiceRange = (float)args[2];

            Player player = Entities.Players.GetAtRemote(handle);

            if (player == null)
                return;

            if (Player.LocalPlayer == player)
            {
                VoiceManager.VoiceRange = voiceRange;
            }
            else
            {
                lock (VoiceManager._voiceClients)
                {
                    if (VoiceManager._voiceClients.TryGetValue(handle, out VoiceClient voiceClient))
                    {
                        voiceClient.TeamSpeakName = teamSpeakName;
                        voiceClient.VoiceRange = voiceRange;
                    }
                    else
                    {
                        VoiceManager._voiceClients.Add(handle, new VoiceClient(player, teamSpeakName, voiceRange));
                    }
                }
            }
        }

        /// <summary>
        /// Remove a disconnected player
        /// </summary>
        /// <param name="args">args[0] - handle</param>
        private static void OnPlayerDisconnect(object[] args)
        {
            ushort handle = Convert.ToUInt16(args[0]);

            lock (VoiceManager._voiceClients)
            {
                if (VoiceManager._voiceClients.TryGetValue(handle, out VoiceClient voiceClient))
                {
                    VoiceManager._voiceClients.Remove(handle);

                    VoiceManager.ExecuteCommand(new PluginCommand(Command.RemovePlayer, VoiceManager.ServerUniqueIdentifier, new PlayerState(voiceClient.TeamSpeakName)));
                }
            }
        }

        /// <summary>
        /// Tell plugin the player is dead, so we don't hear him anymore
        /// </summary>
        /// <param name="args">args[0] - handle</param>
        private static void OnPlayerDied(object[] args)
        {
            ushort handle = Convert.ToUInt16(args[0]);

            Player player = Entities.Players.GetAtRemote(handle);

            if (player == null || !VoiceManager.TryGetVoiceClient(handle, out VoiceClient voiceClient))
                return;

            voiceClient.IsAlive = false;
        }

        /// <summary>
        /// Tell plugin the player is alive again, se we can hear him
        /// </summary>
        /// <param name="args">[0] - handle</param>
        public static void OnPlayerRevived(object[] args)
        {
            ushort handle = Convert.ToUInt16(args[0]);

            Player player = Entities.Players.GetAtRemote(handle);

            if (player == null || !VoiceManager.TryGetVoiceClient(handle, out VoiceClient voiceClient))
                return;

            voiceClient.IsAlive = true;
        }

        /// <summary>
        /// A player starts/stops talking
        /// </summary>
        /// <param name="args">args[0] - handle | args[1] - <see cref="bool"/> isTalking</param>
        private static void OnPlayerTalking(object[] args)
        {
            ushort handle = Convert.ToUInt16(args[0]);
            bool isTalking = (bool)args[1];

            Player player = Entities.Players.GetAtRemote(handle);

            if (player == null)
                return;

            if (isTalking)
                player.PlayFacialAnim("mic_chatter", "mp_facial");
            else
                player.PlayFacialAnim("mood_normal_1", "facials@gen_male@variations@normal");
        }

        /// <summary>
        /// Tell the plugin we have a new call partner
        /// </summary>
        /// <param name="args">args[0] - handle</param>
        private static void OnEstablishCall(object[] args)
        {
            ushort handle = Convert.ToUInt16(args[0]);

            Player player = Entities.Players.GetAtRemote(handle);

            if (player == null || !VoiceManager.TryGetVoiceClient(handle, out VoiceClient voiceClient))
                return;

            RAGE.Vector3 ownPosition = RAGE.Elements.Player.LocalPlayer.Position;
            RAGE.Vector3 playerPosition = player.Position;

            VoiceManager.ExecuteCommand(
                new PluginCommand(
                    Command.PhoneCommunicationUpdate,
                    VoiceManager.ServerUniqueIdentifier,
                    new PhoneCommunication(
                        voiceClient.TeamSpeakName,
                        RAGE.Game.Zone.GetZoneScumminess(RAGE.Game.Zone.GetZoneAtCoords(ownPosition.X, ownPosition.Y, ownPosition.Z)) +
                        RAGE.Game.Zone.GetZoneScumminess(RAGE.Game.Zone.GetZoneAtCoords(playerPosition.X, playerPosition.Y, playerPosition.Z))
                    )
                )
            );
        }

        /// <summary>
        /// Tell the plugin we have a new call partner
        /// </summary>
        /// <param name="args">args[0] - handle | args[1] - bool | args[2] - string[]</param>
        private static void OnEstablishCallRelayed(object[] args)
        {
            ushort handle = Convert.ToUInt16(args[0]);
            bool direct = (bool)args[1];
            string[] relays = Newtonsoft.Json.JsonConvert.DeserializeObject<string[]>((string)args[2]);

            Player player = Entities.Players.GetAtRemote(handle);

            if (player == null || !VoiceManager.TryGetVoiceClient(handle, out VoiceClient voiceClient))
                return;

            RAGE.Vector3 ownPosition = RAGE.Elements.Player.LocalPlayer.Position;
            RAGE.Vector3 playerPosition = player.Position;

            VoiceManager.ExecuteCommand(
                new PluginCommand(
                    Command.PhoneCommunicationUpdate,
                    VoiceManager.ServerUniqueIdentifier,
                    new PhoneCommunication(
                        voiceClient.TeamSpeakName,
                        RAGE.Game.Zone.GetZoneScumminess(RAGE.Game.Zone.GetZoneAtCoords(ownPosition.X, ownPosition.Y, ownPosition.Z)) +
                        RAGE.Game.Zone.GetZoneScumminess(RAGE.Game.Zone.GetZoneAtCoords(playerPosition.X, playerPosition.Y, playerPosition.Z)),
                        direct,
                        relays
                    )
                )
            );
        }

        /// <summary>
        /// Tell the plugin to end the call
        /// </summary>
        /// <param name="args">args[0] - handle</param>
        private static void OnEndCall(object[] args)
        {
            ushort handle = Convert.ToUInt16(args[0]);

            Player player = Entities.Players.GetAtRemote(handle);

            if (player == null || !VoiceManager.TryGetVoiceClient(handle, out VoiceClient voiceClient))
                return;

            VoiceManager.ExecuteCommand(
                new PluginCommand(
                    Command.StopPhoneCommunication,
                    VoiceManager.ServerUniqueIdentifier,
                    new PhoneCommunication(
                        voiceClient.TeamSpeakName
                    )
                )
            );
        }

        /// <summary>
        /// Sets players radio channel
        /// </summary>
        /// <param name="args">args[0] - radioChannel</param>
        private static void OnSetRadioChannel(object[] args)
        {
            string radioChannel = (string)args[0];

            if (String.IsNullOrWhiteSpace(radioChannel))
            {
                VoiceManager.RadioChannel = null;
                VoiceManager.PlaySound("leaveRadioChannel", false, "radio");
            }
            else
            {
                VoiceManager.RadioChannel = radioChannel;
                VoiceManager.PlaySound("enterRadioChannel", false, "radio");
            }
        }

        /// <summary>
        /// When someone is talking on our radio channel
        /// </summary>
        /// <param name="args">args[0] - handle | args[1] - isOnRadio</param>
        private static void OnPlayerIsSending(object[] args)
        {
            ushort handle = Convert.ToUInt16(args[0]);
            bool isOnRadio = (bool)args[1];

            Player player = Entities.Players.GetAtRemote(handle);

            if (player == null)
                return;

            if (Player.LocalPlayer == player)
            {
                VoiceManager.PlaySound("selfMicClick", false, "MicClick");
            }
            else
            {
                if (!VoiceManager.TryGetVoiceClient(handle, out VoiceClient voiceClient))
                    return;

                if (isOnRadio)
                {
                    VoiceManager.ExecuteCommand(
                        new PluginCommand(
                            Command.RadioCommunicationUpdate,
                            VoiceManager.ServerUniqueIdentifier,
                            new RadioCommunication(
                                voiceClient.TeamSpeakName,
                                RadioType.LongRange | RadioType.Distributed,
                                RadioType.LongRange | RadioType.Distributed,
                                true
                            )
                        )
                    );
                }
                else
                {
                    VoiceManager.ExecuteCommand(
                        new PluginCommand(
                            Command.StopRadioCommunication,
                            VoiceManager.ServerUniqueIdentifier,
                            new RadioCommunication(
                                voiceClient.TeamSpeakName,
                                true
                            )
                        )
                    );
                }
            }
        }

        /// <summary>
        /// When someone is talking on our radio channel
        /// </summary>
        /// <param name="args">args[0] - handle | args[1] - isOnRadio | args[2] - stateChange | args[3] - direct | args[4] - relays</param>
        private static void OnPlayerIsSendingRelayed(object[] args)
        {
            ushort handle = Convert.ToUInt16(args[0]);
            bool isOnRadio = (bool)args[1];
            bool stateChange = (bool)args[2];
            bool direct = (bool)args[3];
            string[] relays = Newtonsoft.Json.JsonConvert.DeserializeObject<string[]>((string)args[4]);

            Player player = Entities.Players.GetAtRemote(handle);

            if (player == null)
                return;

            if (Player.LocalPlayer == player)
            {
                VoiceManager.PlaySound("selfMicClick", false, "MicClick");
            }
            else
            {
                if (!VoiceManager.TryGetVoiceClient(handle, out VoiceClient voiceClient))
                    return;

                if (isOnRadio)
                {
                    VoiceManager.ExecuteCommand(
                        new PluginCommand(
                            Command.RadioCommunicationUpdate,
                            VoiceManager.ServerUniqueIdentifier,
                            new RadioCommunication(
                                voiceClient.TeamSpeakName,
                                RadioType.LongRange | RadioType.Distributed,
                                RadioType.LongRange | RadioType.Distributed,
                                stateChange,
                                direct,
                                relays
                            )
                        )
                    );
                }
                else
                {
                    VoiceManager.ExecuteCommand(
                        new PluginCommand(
                            Command.StopRadioCommunication,
                            VoiceManager.ServerUniqueIdentifier,
                            new RadioCommunication(
                                voiceClient.TeamSpeakName,
                                stateChange
                            )
                        )
                    );
                }
            }
        }

        /// <summary>
        /// Tell plugin where all radio towers are
        /// </summary>
        /// <param name="args">[0] - towerPositions</param>
        private static void OnUpdateRadioTowers(object[] args)
        {
            TSVector[] towerPositions = Newtonsoft.Json.JsonConvert.DeserializeObject<TSVector[]>((string)args[0]);

            VoiceManager.ExecuteCommand(
                new PluginCommand(
                    Command.RadioTowerUpdate,
                    VoiceManager.ServerUniqueIdentifier,
                    new RadioTower(
                        towerPositions
                    )
                )
            );
        }

        private static void OnTick(List<RAGE.Events.TickNametagData> nametags)
        {
            RAGE.Game.Pad.DisableControlAction(1, (int)RAGE.Game.Control.EnterCheatCode, true);
            RAGE.Game.Pad.DisableControlAction(1, (int)RAGE.Game.Control.PushToTalk, true);

            // Calculate player states
            if (VoiceManager.IsReady && DateTime.Now > VoiceManager._nextUpdate)
            {
                VoiceManager.PlayerStateUpdate();

                VoiceManager._nextUpdate = DateTime.Now.AddMilliseconds(300);
            }

            // Lets the player talk on his radio channel with "N"
            if (!String.IsNullOrWhiteSpace(VoiceManager.RadioChannel))
            {
                if (RAGE.Game.Pad.IsDisabledControlJustPressed(1, (int)RAGE.Game.Control.PushToTalk))
                {
                    RAGE.Events.CallRemote(SaltyShared.Event.SaltyChat_IsSending, VoiceManager.RadioChannel, true);
                }
                else if (RAGE.Game.Pad.IsDisabledControlJustReleased(1, (int)RAGE.Game.Control.PushToTalk))
                {
                    RAGE.Events.CallRemote(SaltyShared.Event.SaltyChat_IsSending, VoiceManager.RadioChannel, false);
                }
            }

            // Lets the player change his voice range with "^"
            if (RAGE.Game.Pad.IsDisabledControlJustPressed(1, (int)RAGE.Game.Control.EnterCheatCode))
            {
                VoiceManager.ToggleVoiceRange();
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
            VoiceManager._isConnected = true;

            VoiceManager.InitiatePlugin();
        }

        /// <summary>
        /// Plugin disconnected from WebSocket
        /// </summary>
        /// <param name="args"></param>
        public static void OnPluginDisconnected(object[] args)
        {
            VoiceManager._isConnected = false;
        }

        /// <summary>
        /// Plugin state update
        /// </summary>
        /// <param name="args">[0] - <see cref="PluginCommand"/> as json</param>
        public static void OnPluginMessage(object[] args)
        {
            PluginCommand pluginCommand = PluginCommand.Deserialize((string)args[0]);

            if (pluginCommand.Command == Command.Ping && VoiceManager._nextUpdate.AddSeconds(1) > DateTime.Now)
            {
                VoiceManager.ExecuteCommand(new PluginCommand(VoiceManager.ServerUniqueIdentifier));
                return;
            }

            if (!pluginCommand.TryGetState(out PluginState pluginState))
                return;

            if (pluginState.IsReady != VoiceManager._isIngame)
            {
                RAGE.Events.CallRemote(SaltyShared.Event.SaltyChat_CheckVersion, pluginState.UpdateBranch, pluginState.Version);

                VoiceManager._isIngame = pluginState.IsReady;
            }

            bool hasTalkingChanged = false;
            bool hasMicMutedChanged = false;
            bool hasSoundMutedChanged = false;

            if (pluginState.IsTalking != VoiceManager.IsTalking)
            {
                VoiceManager.IsTalking = pluginState.IsTalking;
                hasTalkingChanged = true;

                RAGE.Events.CallRemote(SaltyShared.Event.SaltyChat_IsTalking, VoiceManager.IsTalking);
            }

            if (pluginState.IsMicrophoneMuted != VoiceManager.IsMicrophoneMuted)
            {
                VoiceManager.IsMicrophoneMuted = pluginState.IsMicrophoneMuted;
                hasMicMutedChanged = true;
            }

            if (pluginState.IsSoundMuted != VoiceManager.IsSoundMuted)
            {
                VoiceManager.IsSoundMuted = pluginState.IsSoundMuted;
                hasSoundMutedChanged = true;
            }

            if (hasTalkingChanged)
                VoiceManager.OnTalkingStateChange?.Invoke(new SoundEventArgs());

            if (hasMicMutedChanged)
                VoiceManager.OnMicrophoneMuteStateChange?.Invoke(new SoundEventArgs());

            if (hasSoundMutedChanged)
                VoiceManager.OnSoundMuteStateChange?.Invoke(new SoundEventArgs());

            if (hasTalkingChanged || hasMicMutedChanged || hasSoundMutedChanged)
                VoiceManager.OnSoundStateChange?.Invoke(new SoundEventArgs());
        }

        /// <summary>
        /// Plugin error
        /// </summary>
        /// <param name="args">[0] - <see cref="PluginCommand"/> as json</param>
        public static void OnPluginError(object[] args)
        {
            try
            {
                PluginError pluginError = Newtonsoft.Json.JsonConvert.DeserializeObject<PluginError>((string)args[0]);

                if (pluginError.Error == Error.AlreadyInGame)
                    VoiceManager.InitiatePlugin(); // try again an hope that the game instance was reset on plugin side
                else
                    RAGE.Chat.Output($"Salty Chat -- Error: {pluginError.Error} - Message: {pluginError.Message}");
            }
            catch
            {
                RAGE.Chat.Output($"Salty Chat -- We got an error, but couldn't deserialize it...");
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Initiates the plugin
        /// </summary>
        private static void InitiatePlugin()
        {
            if (String.IsNullOrWhiteSpace(VoiceManager.TeamSpeakName))
                return;

            VoiceManager.ExecuteCommand(new PluginCommand(Command.Initiate, new GameInstance(VoiceManager.ServerUniqueIdentifier, VoiceManager.TeamSpeakName, VoiceManager.IngameChannel, VoiceManager.IngameChannelPassword == default ? String.Empty : VoiceManager.IngameChannelPassword, VoiceManager.SoundPack)));
        }

        /// <summary>
        /// Plays a file from soundpack specified in <see cref="VoiceManager.SoundPack"/>
        /// </summary>
        /// <param name="fileName">filename (without .wav) of the soundfile</param>
        /// <param name="loop">use <see cref="true"/> to let the plugin loop the sound</param>
        /// <param name="handle">use your own handle instead of the filename, so you can play the sound multiple times</param>
        public static void PlaySound(string fileName, bool loop = false, string handle = null)
        {
            if (String.IsNullOrWhiteSpace(handle))
                handle = fileName;

            VoiceManager.ExecuteCommand(new PluginCommand(Command.PlaySound, VoiceManager.ServerUniqueIdentifier, new Sound(fileName, loop, handle)));
        }

        /// <summary>
        /// Stops and dispose the sound
        /// </summary>
        /// <param name="handle">filename or handle of the sound</param>
        public static void StopSound(string handle)
        {
            VoiceManager.ExecuteCommand(new PluginCommand(Command.StopSound, VoiceManager.ServerUniqueIdentifier, new Sound(handle)));
        }

        /// <summary>
        /// Sends the plugin an update on all players
        /// </summary>
        private static void PlayerStateUpdate()
        {
            RAGE.Vector3 playerPosition = Player.LocalPlayer.Position;

            foreach (var voiceClient in VoiceManager.VoiceClients)
            {
                RAGE.Vector3 nPlayerPosition = voiceClient.Player.Position;

                VoiceManager.ExecuteCommand(
                    new PluginCommand(
                        Command.PlayerStateUpdate,
                        VoiceManager.ServerUniqueIdentifier,
                        new PlayerState(
                            voiceClient.TeamSpeakName,
                            nPlayerPosition,
                            voiceClient.VoiceRange,
                            voiceClient.IsAlive
                        )
                    )
                );
            }

            VoiceManager.ExecuteCommand(
                new PluginCommand(
                    Command.SelfStateUpdate,
                    VoiceManager.ServerUniqueIdentifier,
                    new PlayerState(
                        playerPosition,
                        RAGE.Game.Cam.GetGameplayCamRot(0).Z
                    )
                )
            );
        }

        /// <summary>
        /// Toggles voice range through <see cref="VoiceManager.VoiceRanges"/>
        /// </summary>
        public static void ToggleVoiceRange()
        {
            int index = Array.IndexOf(SaltyShared.SharedData.VoiceRanges, VoiceManager.VoiceRange);

            if (index < 0)
            {
                RAGE.Events.CallRemote(SaltyShared.Event.SaltyChat_SetVoiceRange, SaltyShared.SharedData.VoiceRanges[1]);
            }
            else if (index + 1 >= SaltyShared.SharedData.VoiceRanges.Length)
            {
                RAGE.Events.CallRemote(SaltyShared.Event.SaltyChat_SetVoiceRange, SaltyShared.SharedData.VoiceRanges[0]);
            }
            else
            {
                RAGE.Events.CallRemote(SaltyShared.Event.SaltyChat_SetVoiceRange, SaltyShared.SharedData.VoiceRanges[index + 1]);
            }
        }
        #endregion

        #region Helper
        private static bool TryGetVoiceClient(ushort handle, out VoiceClient voiceClient)
        {
            lock (VoiceManager._voiceClients)
            {
                if (VoiceManager._voiceClients.TryGetValue(handle, out voiceClient))
                    return true;
            }

            voiceClient = null;
            return false;
        }

        private static void ExecuteCommand(PluginCommand pluginCommand)
        {
            if (!VoiceManager.IsEnabled || !VoiceManager.IsConnected || pluginCommand == default)
                return;

            VoiceManager._htmlWindow.ExecuteJs($"runCommand('{pluginCommand.Serialize()}')");
        }
        #endregion
    }
}
