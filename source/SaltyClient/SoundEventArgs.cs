// Copyright (c) 2019 saltmine.de - https://github.com/saltminede

using System;
using System.Collections.Generic;
using System.Text;

namespace SaltyClient
{
    public class SoundEventArgs : EventArgs
    {
        public bool IsTalking => Voice.IsTalking;
        public bool IsMicrophoneMuted => Voice.IsMicrophoneMuted;
        public bool IsSoundMuted => Voice.IsSoundMuted;
    }
}
