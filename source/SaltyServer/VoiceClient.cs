using System;
using System.Collections.Generic;
using System.Text;

namespace SaltyServer
{
    public class VoiceClient
    {
        public GTANetworkAPI.Player Player { get; set; }
        public string TeamSpeakName { get; set; }
        public float VoiceRange { get; set; }
        public bool PhoneSpeaker { get; set; }
        public bool RadioSpeaker { get; set; }

        public VoiceClient(GTANetworkAPI.Player player, string teamSpeakName, float voiceRange)
        {
            this.Player = player;
            this.TeamSpeakName = teamSpeakName;
            this.VoiceRange = voiceRange;
        }
    }
}
