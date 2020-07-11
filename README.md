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
