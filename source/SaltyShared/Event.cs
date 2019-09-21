// Copyright (c) 2019 saltmine.de - https://github.com/saltminede

using System;
using System.Collections.Generic;
using System.Text;

namespace SaltyShared
{
    public static class Event
    {
        public const string SaltyChat_Initialize = "SaltyChat_Initialize";
        public const string SaltyChat_CheckVersion = "SaltyChat_CheckVersion";
        public const string SaltyChat_UpdateClient = "SaltyChat_UpdateClient";
        public const string SaltyChat_Disconnected = "SaltyChat_Disconnected";

        public const string SaltyChat_PlayerDied = "SaltyChat_PlayerDied";
        public const string SaltyChat_PlayerRevived = "SaltyChat_PlayerRevived";

        public const string SaltyChat_IsTalking = "SaltyChat_IsTalking";
        public const string SaltyChat_SetVoiceRange = "SaltyChat_SetVoiceRange";

        public const string SaltyChat_EstablishedCall = "SaltyChat_EstablishedCall";
        public const string SaltyChat_EstablishedCallRelayed = "SaltyChat_EstablishedCallRelayed";
        public const string SaltyChat_EndCall = "SaltyChat_EndCall";

        public const string SaltyChat_SetRadioChannel = "SaltyChat_SetRadioChannel";
        public const string SaltyChat_IsSending = "SaltyChat_IsSending";
        public const string SaltyChat_IsSendingRelayed = "SaltyChat_IsSendingRelayed";
        public const string SaltyChat_UpdateRadioTowers = "SaltyChat_UpdateRadioTowers";
    }
}
