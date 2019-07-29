// Copyright (c) 2019 saltmine.de - https://github.com/saltminede

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SaltyClient
{
    #region GameInstance
    /// <summary>
    /// Used for <see cref="Command.Initiate"/>
    /// </summary>
    public class GameInstance
    {
        #region Properties
        /// <summary>
        /// Unique id of the server the player must be connected to
        /// </summary>
        public string ServerUniqueIdentifier { get; set; }

        /// <summary>
        /// TeamSpeak name that should be set (max length is 30)
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Id of the TeamSpeak channel the player should be moved to
        /// </summary>
        public ulong ChannelId { get; set; }

        /// <summary>
        /// Password of the TeamSpeak channel
        /// </summary>
        public string ChannelPassword { get; set; }

        /// <summary>
        /// Foldername of the sound pack that will be used (%AppData%\TS3Client\Plugins\SaltyChat\{SoundPack}\)
        /// </summary>
        public string SoundPack { get; set; }
        #endregion

        #region CTOR
        public GameInstance(string serverUniqueIdentifier, string name, ulong channelId)
        {
            this.ServerUniqueIdentifier = serverUniqueIdentifier;
            this.Name = name;
            this.ChannelId = channelId;
            this.ChannelPassword = String.Empty;
            this.SoundPack = "default";
        }

        public GameInstance(string serverUniqueIdentifier, string name, ulong channelId, string channelPassword)
        {
            this.ServerUniqueIdentifier = serverUniqueIdentifier;
            this.Name = name;
            this.ChannelId = channelId;
            this.ChannelPassword = channelPassword;
            this.SoundPack = "default";
        }

        public GameInstance(string serverUniqueIdentifier, string name, ulong channelId, string channelPassword, string soundPack)
        {
            this.ServerUniqueIdentifier = serverUniqueIdentifier;
            this.Name = name;
            this.ChannelId = channelId;
            this.ChannelPassword = channelPassword;
            this.SoundPack = soundPack;
        }
        #endregion
    }
    #endregion

    #region PluginError
    public class PluginError
    {
        public Error Error { get; set; }
        public string Message { get; set; }
        public string ServerIdentifier { get; set; }
    }
    #endregion

    #region PluginState
    /// <summary>
    /// Will be received from the WebSocket if e.g. the mic muted/unmuted
    /// </summary>
    public class PluginState
    {
        public string UpdateBranch { get; set; }
        public string Version { get; set; }
        public bool IsConnectedToServer { get; set; }
        public bool IsReady { get; set; }
        public bool IsTalking { get; set; }
        public bool IsMicrophoneMuted { get; set; }
        public bool IsSoundMuted { get; set; }
    }
    #endregion

    #region PluginCommand
    public class PluginCommand
    {
        #region Properties
        public Command Command { get; set; }
        public string ServerUniqueIdentifier { get; set; }
        public Newtonsoft.Json.Linq.JObject Parameter { get; set; }
        #endregion

        #region CTOR
        /// <summary>
        /// For deserialization only
        /// </summary>
        [Newtonsoft.Json.JsonConstructor]
        internal PluginCommand()
        {

        }

        /// <summary>
        /// Use this for <see cref="Command.Pong"/>
        /// </summary>
        /// <param name="command"></param>
        /// <param name="parameter"></param>
        internal PluginCommand(string serverUniqueIdentifier)
        {
            this.Command = Command.Pong;
            this.ServerUniqueIdentifier = serverUniqueIdentifier;
        }

        /// <summary>
        /// Use this with <see cref="Command.Initiate"/>
        /// </summary>
        /// <param name="command"></param>
        /// <param name="parameter"></param>
        internal PluginCommand(Command command, object parameter)
        {
            this.Command = command;
            this.Parameter = Newtonsoft.Json.Linq.JObject.FromObject(parameter);
        }

        internal PluginCommand(Command command, string serverUniqueIdentifier, object parameter)
        {
            this.Command = command;
            this.ServerUniqueIdentifier = serverUniqueIdentifier;
            this.Parameter = Newtonsoft.Json.Linq.JObject.FromObject(parameter);
        }
        #endregion

        #region Methods
        public string Serialize()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(this);
        }

        public static PluginCommand Deserialize(string json)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<PluginCommand>(json);
        }

        public bool TryGetError(out PluginError pluginError)
        {
            try
            {
                pluginError = this.Parameter.ToObject<PluginError>();

                return true;
            }
            catch
            {
                // do nothing
            }

            pluginError = default;
            return false;
        }

        public bool TryGetState(out PluginState pluginState)
        {
            if (this.Command == Command.StateUpdate)
            {
                try
                {
                    pluginState = this.Parameter.ToObject<PluginState>();

                    return true;
                }
                catch
                {
                    // do nothing
                }
            }

            pluginState = default;
            return false;
        }
        #endregion
    }
    #endregion

    #region PlayerState
    /// <summary>
    /// Used for <see cref="Command.SelfStateUpdate"/> and <see cref="Command.PlayerStateUpdate"/>
    /// </summary>
    public class PlayerState
    {
        #region Properties
        public string Name { get; set; }
        public TSVector Position { get; set; }
        public float? Rotation { get; set; }
        public float? VoiceRange { get; set; }
        public bool IsAlive { get; set; }
        public float? VolumeOverride { get; set; }
        #endregion

        #region CTOR
        /// <summary>
        /// Used for <see cref="Command.RemovePlayer"/>
        /// </summary>
        /// <param name="name"></param>
        public PlayerState(string name)
        {
            this.Name = name;
            this.Position = TSVector.Zero;
        }

        /// <summary>
        /// Used for <see cref="Command.SelfStateUpdate"/>
        /// </summary>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        public PlayerState(RAGE.Vector3 position, float rotation)
        {
            this.Position = new TSVector(position); // Needs to be converted to "SaltyChat.TSVector" due to a bug - client will crash if "RAGE.Vector3" will be serialized with "Newtonsoft.Json.Linq.JObject.FromObject()"
            this.Rotation = rotation;
        }

        /// <summary>
        /// Used for <see cref="Command.PlayerStateUpdate"/>
        /// </summary>
        /// <param name="name"></param>
        /// <param name="position"></param>
        /// <param name="voiceRange"></param>
        /// <param name="isAlive"></param>
        public PlayerState(string name, RAGE.Vector3 position, float voiceRange, bool isAlive)
        {
            this.Name = name;
            this.Position = new TSVector(position); // Needs to be converted to "SaltyChat.TSVector" due to a bug - client will crash if "RAGE.Vector3" will be serialized with "Newtonsoft.Json.Linq.JObject.FromObject()"
            this.VoiceRange = voiceRange;
            this.IsAlive = isAlive;
        }

        /// <summary>
        /// Used for <see cref="Command.PlayerStateUpdate"/> with volume override
        /// </summary>
        /// <param name="name"></param>
        /// <param name="position"></param>
        /// <param name="voiceRange"></param>
        /// <param name="isAlive"></param>
        /// <param name="volumeOverride">Overrides the volume (phone, radio and proximity) - from 0 (0%) to 1.5 (150%)</param>
        public PlayerState(string name, RAGE.Vector3 position, float voiceRange, bool isAlive, float volumeOverride)
        {
            this.Name = name;
            this.Position = new TSVector(position); // Needs to be converted to "SaltyChat.TSVector" due to a bug - client will crash if "RAGE.Vector3" will be serialized with "Newtonsoft.Json.Linq.JObject.FromObject()"
            this.VoiceRange = voiceRange;
            this.IsAlive = isAlive;

            if (volumeOverride > 1.5f)
                this.VolumeOverride = 1.5f;
            else if (volumeOverride < 0f)
                this.VolumeOverride = 0f;
            else
                this.VolumeOverride = volumeOverride;
        }
        #endregion
    }
    #endregion

    #region Phone
    /// <summary>
    /// Used for <see cref="Command.PhoneCommunicationUpdate"/> and <see cref="Command.StopPhoneCommunication"/>
    /// </summary>
    public class PhoneCommunication
    {
        public string Name { get; set; }
        public int? SignalStrength { get; set; }

        public PhoneCommunication(string name)
        {
            this.Name = name;
        }

        public PhoneCommunication(string name, int signalStrength)
        {
            this.Name = name;
            this.SignalStrength = signalStrength;
        }
    }
    #endregion

    #region Radio
    /// <summary>
    /// Used for <see cref="Command.RadioTowerUpdate"/>
    /// </summary>
    public class RadioTower
    {
        public TSVector[] Towers { get; set; }

        public RadioTower(params TSVector[] towers)
        {
            this.Towers = towers;
        }

        public RadioTower(params RAGE.Vector3[] towers)
        {
            this.Towers = towers.Select(t => new TSVector(t)).ToArray();
        }
    }

    /// <summary>
    /// Used for <see cref="Command.RadioCommunicationUpdate"/> and <see cref="Command.StopRadioCommunication"/>
    /// </summary>
    public class RadioCommunication
    {
        public string Name { get; set; }
        public RadioType SenderRadioType { get; set; }
        public RadioType OwnRadioType { get; set; }
        public bool PlayMicClick { get; set; }

        public RadioCommunication(string name, bool playMicClick)
        {
            this.Name = name;
            this.SenderRadioType = RadioType.None;
            this.OwnRadioType = RadioType.None;
            this.PlayMicClick = playMicClick;
        }

        public RadioCommunication(string name, RadioType senderRadioType, RadioType ownRadioType, bool playMicClick)
        {
            this.Name = name;
            this.SenderRadioType = senderRadioType;
            this.OwnRadioType = ownRadioType;
            this.PlayMicClick = playMicClick;
        }
    }

    [Flags]
    public enum RadioType
    {
        /// <summary>
        /// No radio communication
        /// </summary>
        None = 1,

        /// <summary>
        /// Short range radio communication - appx. 3 kilometers
        /// </summary>
        ShortRange = 2,

        /// <summary>
        /// Long range radio communication - appx. 8 kilometers
        /// </summary>
        LongRange = 4,

        /// <summary>
        /// Distributed radio communication, depending on <see cref="RadioTower"/> - appx. 3 (short range) or 8 (long range) kilometers
        /// </summary>
        Distributed = 8,
    }
    #endregion

    #region Sound
    /// <summary>
    /// Used for <see cref="Command.PlaySound"/>
    /// </summary>
    public class Sound
    {
        #region Properties
        public string Filename { get; set; }
        public bool IsLoop { get; set; }
        public string Handle { get; set; }
        #endregion

        #region CTOR
        public Sound(string filename)
        {
            this.Filename = filename;
            this.Handle = filename;
        }

        public Sound(string filename, bool loop)
        {
            this.Filename = filename;
            this.IsLoop = loop;
            this.Handle = filename;
        }

        public Sound(string filename, bool loop, string handle)
        {
            this.Filename = filename;
            this.IsLoop = loop;
            this.Handle = handle;
        }
        #endregion
    }
    #endregion

    #region Command
    public enum Command
    {
        /// <summary>
        /// Use <see cref="GameInstance"/> as parameter
        /// </summary>
        Initiate,

        /// <summary>
        /// Will be sent by the WebSocket and should be answered with a <see cref="Command.Pong"/>
        /// </summary>
        Ping,

        /// <summary>
        /// Answer to a <see cref="Command.Ping"/> request
        /// </summary>
        Pong,

        /// <summary>
        /// Will be sent by the WebSocket on state changes (e.g. mic muted/unmuted) and received by <see cref="Voice.OnPluginMessage(object[])"/> - uses <see cref="PluginState"/> as parameter
        /// </summary>
        StateUpdate,

        /// <summary>
        /// Use <see cref="PlayerState"/> as parameter
        /// </summary>
        SelfStateUpdate,

        /// <summary>
        /// Use <see cref="PlayerState"/> as parameter
        /// </summary>
        PlayerStateUpdate,

        /// <summary>
        /// Use <see cref="PlayerState"/> as parameter
        /// </summary>
        RemovePlayer,

        /// <summary>
        /// Use <see cref="PhoneCommunication"/> as parameter
        /// </summary>
        PhoneCommunicationUpdate,

        /// <summary>
        /// Use <see cref="PhoneCommunication"/> as parameter
        /// </summary>
        StopPhoneCommunication,

        /// <summary>
        /// Use <see cref="RadioTower"/> as parameter
        /// </summary>
        RadioTowerUpdate,

        /// <summary>
        /// Use <see cref="RadioCommunication"/> as parameter
        /// </summary>
        RadioCommunicationUpdate,

        /// <summary>
        /// Use <see cref="RadioCommunication"/> as parameter
        /// </summary>
        StopRadioCommunication,

        /// <summary>
        /// Use <see cref="Sound"/> as parameter
        /// </summary>
        PlaySound,

        /// <summary>
        /// Use <see cref="Sound"/> as parameter
        /// </summary>
        StopSound
    }
    #endregion

    #region Error
    public enum Error
    {
        OK,
        InvalidJson,
        NotConnectedToServer,
        AlreadyInGame,
        ChannelNotAvailable,
        NameNotAvailable
    }
    #endregion

    #region UpdateBranch
    internal enum UpdateBranch
    {
        Stable,
        Testing,
        PreBuild
    }
    #endregion

    #region SoundEventArgs
    public class SoundEventArgs : EventArgs
    {
        public bool IsTalking => Voice.IsTalking;
        public bool IsMicrophoneMuted => Voice.IsMicrophoneMuted;
        public bool IsSoundMuted => Voice.IsSoundMuted;
    }
    #endregion

    #region TSVector
    public class TSVector
    {
        public float X;
        public float Y;
        public float Z;

        public static readonly TSVector Zero = new TSVector(0f, 0f, 0f);

        public TSVector(float x, float y, float z)
        {
            this.X = x;
            this.Y = y;
            this.Z = z;
        }

        public TSVector(RAGE.Vector3 position)
        {
            this.X = position.X;
            this.Y = position.Y;
            this.Z = position.Z;
        }
    }
    #endregion
}
