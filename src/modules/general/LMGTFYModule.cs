/*
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

using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System; // Added for StringSplitOptions if needed, though not strictly required for current Split usage
using System.Linq;
using System.Net; // Added for WebUtility
using System.Collections.Generic; // Added for List

namespace mattbot.modules.general;

[CommandContextType(InteractionContextType.Guild)]
public class LMGTFYModule : InteractionModuleBase<SocketInteractionContext>
{
    [MessageCommand("LMGTFY")]
    public async Task LMGTFYCommand(IMessage message)
    {
        // Role check
        SocketRole noContextCommands = Context.Guild.Roles.FirstOrDefault(role => role.Name == "No Context Commands");
        if (noContextCommands is not null && Context.User is IGuildUser guildUser && guildUser.RoleIds.Contains(noContextCommands.Id))
        {
            await RespondAsync("You are prohibited from using this command!", ephemeral: true);
            return;
        }

        // Bot/Webhook check
        if (message.Author.IsBot || message.Author.IsWebhook)
        {
            await RespondAsync("Bots know everything already!", ephemeral: true);
            return;
        }

        // Empty/Invalid message check
        if (message is not IUserMessage || string.IsNullOrWhiteSpace(message.Content))
        {
            await RespondAsync("There is nothing to Google!", ephemeral: true);
            return;
        }

        string baseUrl = "https://lmgt.org/?q=";
        // For lmgt.org, spaces are typically replaced with '+'
        string[] words = message.Content.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries); // Robustly split and remove empty entries
        string query = string.Join("+", words);
        string lmgtfyUrl = baseUrl + query;

        AllowedMentions allowedMentions = new AllowedMentions { UserIds = new List<ulong> { message.Author.Id } };
        await RespondAsync($"{message.Author.Mention}, this might help:\n<{lmgtfyUrl}>", allowedMentions: allowedMentions);
    }

    [MessageCommand("LMGPTTFY")]
    public async Task LMGPTTFYCommand(IMessage message)
    {
        // Role check
        SocketRole noContextCommands = Context.Guild.Roles.FirstOrDefault(role => role.Name == "No Context Commands");
        if (noContextCommands is not null && Context.User is IGuildUser guildUser && guildUser.RoleIds.Contains(noContextCommands.Id))
        {
            await RespondAsync("You are prohibited from using this command!", ephemeral: true);
            return;
        }

        // Bot/Webhook check
        if (message.Author.IsBot || message.Author.IsWebhook)
        {
            await RespondAsync("Bots know everything already!", ephemeral: true);
            return;
        }

        // Empty/Invalid message check
        if (message is not IUserMessage || string.IsNullOrWhiteSpace(message.Content))
        {
            await RespondAsync("There is nothing to ask ChatGPT!", ephemeral: true);
            return;
        }

        string baseUrl = "https://chatgpt.com/?q=";
        // For general URL query parameters, use URL encoding
        string encodedQuery = WebUtility.UrlEncode(message.Content);
        string lmgpttfyUrl = baseUrl + encodedQuery;

        AllowedMentions allowedMentions = new AllowedMentions { UserIds = new List<ulong> { message.Author.Id } };
        await RespondAsync($"{message.Author.Mention}, this might help:\n<{lmgpttfyUrl}>", allowedMentions: allowedMentions);
    }
}
