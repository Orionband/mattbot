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

using Discord.WebSocket;
using mattbot.utils;

namespace mattbot.automod;

public class MessageReports
{
    private readonly DiscordSocketClient _client;
    private readonly Listener _listener;
    private readonly IConfiguration _configuration;

    public MessageReports(DiscordSocketClient client, Listener listener, IConfiguration configuration)
    {
        _client = client;
        _listener = listener;
        _configuration = configuration;
    }

    public async Task InitializeAsync()
    {
        _listener.ReactionAdded += OnReactionAddedAsync;
    }

    private async Task OnReactionAddedAsync(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
    {
        int.TryParse(_configuration["message_report_threshold"], out int MESSAGE_REPORT_THRESHOLD);
        int.TryParse(_configuration["message_report_duration"], out int MESSAGE_REPORT_DURATION);

        if (!reaction.User.IsSpecified
            || reaction.User.Value.IsBot
            || !channel.HasValue
            || channel.Value is not ITextChannel textChannel
            || MESSAGE_REPORT_THRESHOLD == 0)
        {
            return;
        }

        IGuild guild = textChannel.Guild;

        // Look for a channel called bot_log
        ITextChannel tc = (await guild.GetTextChannelsAsync()).FirstOrDefault(x => x.Name == "bot_log");
        if (tc == null)
            return;

        IUserMessage newMessage;
        if (!message.HasValue)
            newMessage = await message.GetOrDownloadAsync().ConfigureAwait(false);
        else
            newMessage = message.Value;

        // Reaction emoji
        Emoji wastebasket = new("\uD83D\uDDD1\uFE0F"); // 🗑️
        if (!Equals(reaction.Emote, wastebasket))
            return;

        // Ignore system messages
        if (newMessage == null)
            return;

        // Bot perms
        IGuildUser gUser = await guild.GetUserAsync(_client.CurrentUser.Id).ConfigureAwait(false);
        ChannelPermissions botPerms = gUser.GetPermissions(textChannel);
        if (!botPerms.Has(ChannelPermission.ManageMessages))
            return;

        // Ignore moderators & bots
        if ((newMessage.Author is IGuildUser user && user.GuildPermissions.Has(GuildPermission.BanMembers)) || newMessage.Author.IsBot)
            return;

        // Message is within the allowed duration
        if ((newMessage.Timestamp - DateTimeOffset.UtcNow).TotalHours <= -MESSAGE_REPORT_DURATION)
            return;

        // Check if message is replying to someone
        StringBuilder builder = new StringBuilder();
        if (newMessage.Reference is not null)
            builder.Append(newMessage.ReferencedMessage.Author.Mention).Append(" ");

        // Get the contents of the message
        string content;
        string imageurl;

        builder.Append(newMessage.Content);
        imageurl = newMessage.Attachments?.FirstOrDefault()?.ProxyUrl;

        content = builder.ToString();
        if (content is null && imageurl is null)
            return;

        // Look for a role called "No Reports"
        IRole noReports = guild.Roles.FirstOrDefault(role => role.Name == "No Reports");

        // Count all valid reactions
        // A reaction is considered "valid" if the user who reacted is not a bot, the message author, or prohibited from reacting
        int count = 0;
        IAsyncEnumerable<IReadOnlyCollection<IUser>> users = newMessage.GetReactionUsersAsync(reaction.Emote, int.MaxValue);
        StringBuilder rlist = new StringBuilder();
        await foreach (IReadOnlyCollection<IUser> chunk in users)
        {
            foreach (IUser reactuser in chunk)
            {
                IGuildUser guilduser = await guild.GetUserAsync(reactuser.Id);
                if ((noReports is null || !guilduser.RoleIds.Contains(noReports.Id)) && !reactuser.IsBot && reactuser.Id != newMessage.Author.Id)
                {
                    count++;
                    rlist.Append(FormatUtil.formatFullUser(reactuser)).Append(", ");
                }
            }
        }
        rlist.Length -= 2;
        if (count < MESSAGE_REPORT_THRESHOLD)
            return;

        EmbedBuilder eb = new EmbedBuilder().WithColor(0xFF0000).WithDescription(content + $"\n\n[Jump Link]({newMessage.GetJumpUrl()})");

        if (imageurl is not null)
            eb.WithImageUrl(imageurl);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        // TODO add a separate logger for community vote tools
        await Logger.Log(now, tc, WARN, $"{FormatUtil.formatFullUser(newMessage.Author)}'s message was reported in {textChannel.Mention} by:\n\n{rlist}", eb.Build());

        // Delete the message
        await newMessage.DeleteAsync();
    }
}
