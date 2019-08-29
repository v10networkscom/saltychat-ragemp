# Salty Chat for [RAGEMP](https://rage.mp/)
An example implementation of Salty Chat for [RAGEMP](https://rage.mp/).

You can report bugs or make sugguestions via issues, or contribute via pull requests - we appreciate any contribution.

Join our [Discord](https://discord.gg/MBCnqSf) and start with [Salty Chat](https://www.saltmine.de/)!

# Setup Steps
1. Setup [RAGEMP Bridge](https://wiki.gtanet.work/index.php?title=Setting_up_the_Bridge_on_Linux/Windows)
2. Create a new resource in `server-files\bridge\resources` with the contents of the `Server`-folder
3. Edit the `meta.xml` and fill in server UID, Sound Pack and Ingame Channel ID (password is optional)
4. Add `<resource src="VoiceResourceName" />` (replace `VoiceResourceName` accordingly) to `server-files\bridge\settings.xml`
5. Copy the contents of the `Client`-folder into your client resources `server-files\client_packages`
6. Make sure C# client resources are enabled by placing a file named `enable-clientside-cs.txt` in your RAGEMP folder, where `ragemp_v.exe` and `updater.exe` is located
