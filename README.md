# Salty Chat for [RAGEMP](https://rage.mp/)
An example implementation of Salty Chat for [RAGEMP](https://rage.mp/).

You can report bugs or make sugguestions via issues, or contribute via pull requests - we appreciate any contribution.

Join our [Discord](https://discord.gg/MBCnqSf) and start with [Salty Chat](https://www.saltmine.de/)!

# Setup Steps
1. [Build the solution](https://github.com/saltminede/saltychat-docs/blob/master/installing-vs.md#installing-visual-studio) (`source\SaltyChat-RAGEMP.sln`) with Visual Studio 2019
2. Copy contents of `dist`-folder into `server-files`
3. Open `dotnet\resources\SaltyChat\meta.xml` and adjust the [variables](https://github.com/saltminede/saltychat-docs/blob/master/setup.md#config-variables)
4. Add `<resource src="SaltyChat" />` to `server-files\dotnet\settings.xml`
5. Make sure `csharp` in `server-files\conf.json` is enabled

# Keybinds
Description | Control | Default QWERTY
:---: | :---: | :---:
Toggle voice range | EnterCheatCode | ~ / `
Talk on radio | PushToTalk | N

# Events
## Client
### SaltyChat_TalkStateChanged
Parameter | Type | Description
------------ | ------------- | -------------
isTalking | `bool` | `true` when player starts talking, `false` when the player stops talking

### SaltyChat_MicStateChanged
Parameter | Type | Description
------------ | ------------- | -------------
isMicrophoneMuted | `bool` | `true` when player mutes mic, `false` when the player unmutes mic

### SaltyChat_MicEnabledChanged
Parameter | Type | Description
------------ | ------------- | -------------
isMicrophoneEnabled | `bool` | `false` when player disabled mic, `true` when the player enabled mic

### SaltyChat_SoundStateChanged
Parameter | Type | Description
------------ | ------------- | -------------
isSoundMuted | `bool` | `true` when player mutes sound, `false` when the player unmutes sound

### SaltyChat_SoundEnabledChanged
Parameter | Type | Description
------------ | ------------- | -------------
isSoundEnabled | `bool` | `false` when player disabled sound, `true` when the player enabled sound
