# Initial Setup

The following instructions are intended for stable builds of the server. If you feel confident in doing everything yourself, you may be interested in [Manual Setup](./ManualSetup.md).

## Setting Up

1. Get a copy of version `1.52.0.1700` of the game client. This is the final released version of Marvel Heroes, so if you still have the game in your Steam library, you can download it from there. If you do not have the game in your Steam library, you may be able to find an archived copy of it on websites like Archive.org.

2. Install .NET Desktop Runtime 6 if you do not have it installed already. You can download it [here](https://dotnet.microsoft.com/en-us/download/dotnet/6.0). Download [this version](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-6.0.33-windows-x64-installer) if you are not sure which one to get.

3. Download the latest [MHServerEmu stable build](https://github.com/Crypto137/MHServerEmu/releases/latest) and extract it.

4. Run the included `SetupSorcererSupreme` tool and point it to your Marvel Heroes game files. If you have the game on Steam, you can find them by right clicking on the game in your library and choosing `Manage` -> `Browse local files`.

## Running the Server

1. Run the included `StartServers.bat` file and wait for MHServerEmu to initialize.

2. (Optional) Open http://localhost:8080/AccountManagement/Create and create an account. Note: this link is going to work only when the servers are running.

3. Run `StartClient.bat` and log in with your created account OR run `StartClientAutoLogin.bat` to play with a default account.

4. When you are done, run the `StopServers.bat` file to stop the servers.

## Updating MHServerEmu

In most cases you can update MHServerEmu simply by downloading the [latest nightly build](https://nightly.link/Crypto137/MHServerEmu/workflows/nightly-release-windows-x64/master?preview) and extracting it into the `MHServerEmu` directory, overwriting all files.

In some cases migrating to a new version may require additional steps. These are going to be posted on our [Discord server](https://discord.gg/hjR8Bj52t3) in the #news channel.
