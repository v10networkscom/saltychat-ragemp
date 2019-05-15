// Copyright (c) 2019 saltmine.de - https://github.com/saltminede

using System;
using System.Collections.Generic;
using System.Text;

namespace SaltyShared
{
    public static class Event
    {
        public const string Voice_Initialize = "Voice_Initialize";
        public const string Voice_RejectedVersion = "Voice_RejectedVersion";
        public const string Voice_IsTalking = "Voice_IsTalking";
        public const string Voice_SetVoiceRange = "Voice_SetVoiceRange";
        public const string Voice_EstablishedCall = "Voice_EstablishedCall";
        public const string Voice_EndCall = "Voice_EndCall";
        public const string Voice_SetRadioChannel = "Voice_SetRadioChannel";
        public const string Voice_TalkingOnRadio = "Voice_TalkingOnRadio";

        public const string Player_Died = "Player_Died";
        public const string Player_Revived = "Player_Revived";
        public const string Player_Disconnected = "Player_Disconnected";
    }
}
