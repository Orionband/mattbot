﻿/*
 * Copyright 2023-2025 Matthew Ring
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Discord.Interactions;
using Discord.WebSocket;

namespace mattbot.modules.moderation;

[CyberPatriot]
[DefaultMemberPermissions(GuildPermission.BanMembers)]
[Group("lockdown", "Set the server lockdown status")]
public class LockdownModule : InteractionModuleBase<SocketInteractionContext>
{
    private static readonly string LOCK = "\uD83D\uDD12"; // 🔒
    private static readonly string UNLOCK = "\uD83D\uDD13"; // 🔓

    // lockdown toggle
    [SlashCommand("toggle", "Toggle the server lockdown status")]
    public async Task ToggleLockdownAsync([Choice("OFF", 0),
                                           Choice("NORMAL", 1),
                                           Choice("STRICT", 2)]
                                          [Summary("setting", "The setting of lockdown mode")] int lockdownStatusValue)
    {
        switch (lockdownStatusValue)
        {
            case 0:
                StringBuilder sb = new StringBuilder($"{SUCCESS} Disabling lockdown mode!\n");
                await RespondAsync($"{sb} {LOADING} Unhiding channels...");
                try
                {
                    // Unhide channels
                    SocketRole everyone = Context.Guild.GetRole(301768361136750592);
                    await everyone.ModifyAsync(role => role.Permissions = everyone.Permissions.Modify(viewChannel: true));
                    sb.Append($"{SUCCESS} Channels unhidden!\n");
                    await ModifyOriginalResponseAsync(msg => msg.Content = $"{sb} {LOADING} Hoisting roles...\n");

                    // Hoist roles
                    SocketRole windowsHelper = Context.Guild.GetRole(514289040242114561);
                    await windowsHelper.ModifyAsync(role => role.Hoist = true);
                    SocketRole linuxHelper = Context.Guild.GetRole(514289361177673728);
                    await linuxHelper.ModifyAsync(role => role.Hoist = true);
                    SocketRole ciscoHelper = Context.Guild.GetRole(514289524612923393);
                    await ciscoHelper.ModifyAsync(role => role.Hoist = true);
                    SocketRole eventWinner = Context.Guild.GetRole(1042648973950849065);
                    await eventWinner.ModifyAsync(role => role.Hoist = true);
                    SocketRole star = Context.Guild.GetRole(1031999100033450014);
                    await star.ModifyAsync(role => role.Hoist = true);
                    SocketRole formerStaff = Context.Guild.GetRole(847962114337275915);
                    await formerStaff.ModifyAsync(role => role.Hoist = true);
                    sb.Append($"{SUCCESS} Roles hoisted!\n");
                    await ModifyOriginalResponseAsync(msg => msg.Content = $"{sb} {LOADING} Restoring default permissions...\n");

                    // Restore default permissions
                    await everyone.ModifyAsync(role => role.Permissions = everyone.Permissions.Modify(sendMessagesInThreads: true,
                                                                                                    createPublicThreads: true,
                                                                                                    createPrivateThreads: true));
                    await windowsHelper.ModifyAsync(role => role.Mentionable = true);
                    await linuxHelper.ModifyAsync(role => role.Mentionable = true);
                    await ciscoHelper.ModifyAsync(role => role.Mentionable = true);
                    await ModifyOriginalResponseAsync(msg => msg.Content = $"{sb}{SUCCESS} Default permissions restored!\n\n{UNLOCK} Lockdown mode disabled!");
                }
                catch (Exception)
                {
                    await ModifyOriginalResponseAsync(msg => msg.Content = $"{sb} {ERROR} {ERROR_MESSAGE}");
                }
                break;
            case 1:
                StringBuilder sb2 = new StringBuilder($"{SUCCESS} Enabling lockdown mode!\n");
                await RespondAsync($"{sb2} {LOADING} Hiding channels...");
                try
                {
                    // Hide channels
                    SocketRole everyone = Context.Guild.GetRole(301768361136750592);
                    await everyone.ModifyAsync(role => role.Permissions = everyone.Permissions.Modify(viewChannel: false));
                    sb2.Append($"{SUCCESS} Channels hidden!\n");
                    await ModifyOriginalResponseAsync(msg => msg.Content = $"{sb2} {LOADING} Dehoisting roles...\n");

                    // Dehoist roles
                    SocketRole windowsHelper = Context.Guild.GetRole(514289040242114561);
                    await windowsHelper.ModifyAsync(role => role.Hoist = false);
                    SocketRole linuxHelper = Context.Guild.GetRole(514289361177673728);
                    await linuxHelper.ModifyAsync(role => role.Hoist = false);
                    SocketRole ciscoHelper = Context.Guild.GetRole(514289524612923393);
                    await ciscoHelper.ModifyAsync(role => role.Hoist = false);
                    SocketRole eventWinner = Context.Guild.GetRole(1042648973950849065);
                    await eventWinner.ModifyAsync(role => role.Hoist = false);
                    SocketRole star = Context.Guild.GetRole(1031999100033450014);
                    await star.ModifyAsync(role => role.Hoist = false);
                    SocketRole formerStaff = Context.Guild.GetRole(847962114337275915);
                    await formerStaff.ModifyAsync(role => role.Hoist = false);
                    sb2.Append($"{SUCCESS} Roles dehoisted!\n");
                    await ModifyOriginalResponseAsync(msg => msg.Content = $"{sb2} {LOADING} Removing default permissions...\n");

                    // Remove default permissions
                    await everyone.ModifyAsync(role => role.Permissions = everyone.Permissions.Modify(sendMessagesInThreads: false,
                                                                                                    createPublicThreads: false,
                                                                                                    createPrivateThreads: false));
                    await windowsHelper.ModifyAsync(role => role.Mentionable = false);
                    await linuxHelper.ModifyAsync(role => role.Mentionable = false);
                    await ciscoHelper.ModifyAsync(role => role.Mentionable = false);
                    await ModifyOriginalResponseAsync(msg => msg.Content = $"{sb2}{SUCCESS} Default permissions removed!\n\n{LOCK} Lockdown mode enabled!");
                }
                catch (Exception)
                {
                    await ModifyOriginalResponseAsync(msg => msg.Content = $"{sb2} {ERROR} {ERROR_MESSAGE}");
                }
                break;
            case 2:
                // Slowmode
                // Increase Discord verification level
                // Dehoist all roles
                // Lock more channels
                // Remove embed links and image permissions
                goto case 1;
            default:
                break;
        }
    }
}
