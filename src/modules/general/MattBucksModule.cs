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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json; 
using System.Threading.Tasks;
using System.Timers;
using Color = Discord.Color;

namespace mattbot.modules.general
{
    [Group("matt", "matt bucks commands")]
    public class MattBucksModule : InteractionModuleBase<SocketInteractionContext>
    {
        // --- Emojis and Constants ---
        private static readonly string CONSTRUCTION = "\uD83D\uDEA7"; // 🚧
        private static readonly string SWORD = "\u2694\uFE0F"; // ⚔️
        private static readonly string WATER = "\uD83D\uDCA7"; // 💧

        private static readonly string NITRO_EMOJI = "<:nitro:1091272926881390634>";
        private static readonly string PING_EMOJI = "<:ping:1091272927737036953>";
        private static readonly string MATTBUCKS_EMOJI = "<:MattBucks:1091275681024970853>";
        private static readonly string CYBERPATRIOT_SERVER_INVITE = "https://discord.gg/cyberpatriot";
        private static readonly string CCDC_SERVER_INVITE = "https://discord.gg/ccdc";

        // --- Config for Automated Rewards & Notifications ---
        private static readonly List<ulong> _boosterRewardServerIds = new List<ulong>
        {
            301768361136750592, // CyberPatriot Server ID
            1093372273295101992  // CCDC Server ID
        };
        private const int BOOSTER_REWARD_AMOUNT = 1;
        private static readonly TimeSpan _boosterRewardInterval = TimeSpan.FromHours(6);

        private const ulong MATTHEWZRING_USER_ID = 349007194768801792; // matthewzring's User ID

        // --- Data Storage for MattBucks ---
        private static Dictionary<ulong, int> _userMattBucks = new Dictionary<ulong, int>();
        private static Dictionary<ulong, DateTime> _lastBoosterRewardTime = new Dictionary<ulong, DateTime>(); 

        // --- JSON Persistence ---
        private static readonly string _dataFilePath = "mattbucks_data.json";
        private class MattBucksData 
        {
            public Dictionary<ulong, int> UserMattBucks { get; set; } = new Dictionary<ulong, int>();
            public Dictionary<ulong, DateTime> LastBoosterRewardTime { get; set; } = new Dictionary<ulong, DateTime>();
        }

        private static void LoadData()
        {
            lock (_userMattBucks) 
            {
                lock (_lastBoosterRewardTime)
                {
                    if (File.Exists(_dataFilePath))
                    {
                        try
                        {
                            string json = File.ReadAllText(_dataFilePath);
                            var data = JsonSerializer.Deserialize<MattBucksData>(json);
                            if (data != null)
                            {
                                _userMattBucks = data.UserMattBucks ?? new Dictionary<ulong, int>();
                                _lastBoosterRewardTime = data.LastBoosterRewardTime ?? new Dictionary<ulong, DateTime>();
                                Console.WriteLine($"MattBucks: Data loaded successfully from {_dataFilePath}.");
                            }
                            else
                            {
                                Console.WriteLine($"MattBucks: Failed to deserialize data from {_dataFilePath}. Using empty datasets.");
                                _userMattBucks = new Dictionary<ulong, int>();
                                _lastBoosterRewardTime = new Dictionary<ulong, DateTime>();
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"MattBucks: Error loading data from {_dataFilePath}: {ex.Message}. Using empty datasets.");
                            _userMattBucks = new Dictionary<ulong, int>();
                            _lastBoosterRewardTime = new Dictionary<ulong, DateTime>();
                        }
                    }
                    else
                    {
                        Console.WriteLine($"MattBucks: Data file ({_dataFilePath}) not found. Starting with empty datasets.");
                        _userMattBucks = new Dictionary<ulong, int>();
                        _lastBoosterRewardTime = new Dictionary<ulong, DateTime>();
                    }
                }
            }
        }

        private static void SaveData()
        {

            try
            {
                MattBucksData dataToSave;

                lock (_userMattBucks)
                {
                    lock (_lastBoosterRewardTime)
                    {
                        dataToSave = new MattBucksData
                        {
                            UserMattBucks = new Dictionary<ulong, int>(_userMattBucks),
                            LastBoosterRewardTime = new Dictionary<ulong, DateTime>(_lastBoosterRewardTime)
                        };
                    }
                }
                string json = JsonSerializer.Serialize(dataToSave, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_dataFilePath, json);
                // Console.WriteLine($"MattBucks: Data saved successfully to {_dataFilePath}."); // do if you want logs.
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MattBucks: Error saving data to {_dataFilePath}: {ex.Message}");
            }
        }


        // --- Shop Item Definitions ---
        private static readonly Dictionary<string, (string Name, int Price, string Emoji, string Description)> _shopItems = new()
        {
            { "1v1", ("1v1 Matt", 100, SWORD, "1v1 matt in Valorant, League, TFT, Minecraft, Tetris or Chess") },
            { "nitro", ("1 Month Discord Nitro", 1500, NITRO_EMOJI, "One month of Discord Nitro, delivered via Discord") },
            { "water", ("1 Bottle of AFA Water", 50000, WATER, "One bottle of AFA water, shipped directly to you") },
            { "ping", ("Custom @everyone", 100000, PING_EMOJI, "Customized @everyone message in #announcements") }
        };

        // --- Timer for Automated Rewards ---
        private static System.Timers.Timer _boosterRewardTimer;
        private static DiscordSocketClient _client; 


        public static void InitializeService(DiscordSocketClient client)
        {
            _client = client;
            LoadData(); // Load data on startup

            if (_boosterRewardServerIds.Any(id => id != 0))
            {
                _boosterRewardTimer = new System.Timers.Timer(_boosterRewardInterval.TotalMilliseconds);
                _boosterRewardTimer.Elapsed += async (sender, e) =>
                {
                    try
                    {
                        await GrantBoosterRewardsAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] MattBucks: Unhandled exception in booster reward timer: {ex.Message}\n{ex.StackTrace}");
                    }
                };
                _boosterRewardTimer.AutoReset = true;
                _boosterRewardTimer.Enabled = true;
                Console.WriteLine($"MattBucks: Booster reward timer started. Checking servers: {string.Join(", ", _boosterRewardServerIds)}. Interval: {_boosterRewardInterval}.");
            }
            else
            {
                Console.WriteLine("MattBucks: No booster reward server IDs configured. Timer not started.");
            }
        }

        private static async Task GrantBoosterRewardsAsync()
        {
            if (_client == null || _client.LoginState != LoginState.LoggedIn || _client.ConnectionState != ConnectionState.Connected)
            {
                Console.WriteLine($"MattBucks: Client not ready for booster rewards check. LoginState: {_client?.LoginState}, ConnectionState: {_client?.ConnectionState}");
                return;
            }
            Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] MattBucks: Checking for booster rewards...");

            // GatewayIntent.GuildMembers needs to be enabled

            foreach (ulong guildId in _boosterRewardServerIds)
            {
                if (guildId == 0) continue;

                var guild = _client.GetGuild(guildId);
                if (guild == null)
                {
                    Console.WriteLine($"MattBucks: Guild {guildId} not found for booster check. Bot might not be in this guild or guild is unavailable.");
                    continue;
                }

                foreach (var user in guild.Users)
                {
                    if (user.IsBot) continue;

                    if (user.PremiumSince.HasValue) // User is boosting this server
                    {
                        bool shouldReward = true;
                        lock (_lastBoosterRewardTime)
                        {
                            if (_lastBoosterRewardTime.TryGetValue(user.Id, out DateTime lastReward))
                            {
                                if (DateTime.UtcNow - lastReward < _boosterRewardInterval)
                                {
                                    shouldReward = false;
                                }
                            }
                        }

                        if (shouldReward)
                        {
                            ModifyUserBalance(user.Id, BOOSTER_REWARD_AMOUNT); // This now also calls SaveData()
                            lock (_lastBoosterRewardTime)
                            {
                                _lastBoosterRewardTime[user.Id] = DateTime.UtcNow;
                            }
                            SaveData(); // Save changes to _lastBoosterRewardTime
                            Console.WriteLine($"MattBucks: Awarded {BOOSTER_REWARD_AMOUNT} MattBucks to {user.Username} ({user.Id}) for boosting {guild.Name}.");
                            // try { await user.SendMessageAsync($"You've received {BOOSTER_REWARD_AMOUNT} MattBucks {MATTBUCKS_EMOJI} for boosting {guild.Name}!"); } catch { /* Ignore DM failures */ }
                        }
                    }
                }
            }
            Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] MattBucks: Booster rewards check complete.");
        }


        // --- Helper method to get user balance ---
        private int GetUserBalance(ulong userId)
        {
            lock (_userMattBucks)
            {
                _userMattBucks.TryGetValue(userId, out int balance);
                return balance;
            }
        }

        // --- Helper method to modify user balance ---
        public static void ModifyUserBalance(ulong userId, int amount)
        {
            lock (_userMattBucks)
            {
                if (!_userMattBucks.ContainsKey(userId))
                {
                    _userMattBucks[userId] = 0;
                }
                _userMattBucks[userId] += amount;


                if (_userMattBucks[userId] < 0)
                {
                    _userMattBucks[userId] = 0;
                }
            }
            SaveData(); 
        }

        [SlashCommand("explanation", "explains what MattBucks are")]
        public async Task ExplanationAsync()
        {
            string cyberPatriotServerName = _client.GetGuild(_boosterRewardServerIds[0])?.Name ?? "CyberPatriot";
            string ccdcServerName = _client.GetGuild(_boosterRewardServerIds[1])?.Name ?? "CCDC";

            await RespondAsync($"{Context.User.Mention}, MattBucks {MATTBUCKS_EMOJI} are a virtual currency that can be earned and redeemed!\n\n" +
                $"**Commands:**\n" +
                $"`/matt bucks` - shows how many MattBucks you have\n" +
                $"`/matt richest` - shows the richest users\n" +
                $"`/matt shop` - shows the available redeemables\n" +
                $"`/matt purchase <item>` - buys an item from the shop\n\n" +
                $"**Ways to earn MattBucks:**\n" +
                $"`1.` __Nitro Boosting__ ~ Every {_boosterRewardInterval.TotalHours} hours, anyone boosting any of the following servers gets {BOOSTER_REWARD_AMOUNT} MattBuck\n" +
                $"\t\t\\- *{cyberPatriotServerName}* (<{CYBERPATRIOT_SERVER_INVITE}>)\n" +
                $"\t\t\\- *{ccdcServerName}* (<{CCDC_SERVER_INVITE}>)\n" +
                $"`2.` __Twitch__ ~ Every 6 hours, anyone subscribed to matt's Twitch channel gets 1 MattBuck (Note: Twitch integration not implemented in this snippet)\n" +
                $"`3.` __Giveaways__ ~ Sometimes matt will just give away MattBucks randomly\n" +
                $"Note: All the above methods stack; if you're boosting in Discord and subscribed on Twitch, you'll earn more MattBucks.", ephemeral: true);
        }

        [SlashCommand("shop", "shows the available redeemables")]
        public async Task GetShopAsync()
        {
            IEnumerable<SocketRole> filterOutDefault = Context.Guild.CurrentUser.Roles.Where(r => r.Color != Color.Default);
            Color botHighestRoleColor = Color.Default;
            if (filterOutDefault.Any())
                botHighestRoleColor = filterOutDefault.MaxBy(r => r.Position)!.Color;

            EmbedBuilder eb = new EmbedBuilder().WithTitle($"MattBucks Shop Inventory {MATTBUCKS_EMOJI}")
                                        .WithColor(botHighestRoleColor)
                                        .WithDescription("Use `/matt purchase <item_code>` to buy an item.")
                                        .WithFooter("For full help, use /matt explanation");

            foreach (var item in _shopItems)
            {
                eb.AddField($"{item.Value.Emoji} {item.Value.Name}",
                            $"MattBucks Price: `{item.Value.Price}`\nPurchase Code: `{item.Key}`\n{item.Value.Description}",
                            true);
            }
            await RespondAsync(embed: eb.Build(), allowedMentions: AllowedMentions.None, ephemeral: true);
        }

        [SlashCommand("bucks", "shows how many MattBucks you have")]
        public async Task CheckCoinsAsync()
        {
            int balance = GetUserBalance(Context.User.Id);
            await RespondAsync($"{Context.User.Mention}, you have `{balance}` MattBucks! {MATTBUCKS_EMOJI}", ephemeral: true);
        }

        [SlashCommand("purchase", "buys an item from the shop")]
        public async Task PurchaseAsync(
            [Summary("item", "The item you want to purchase from the shop")]
            [Choice("1v1 Matt (100 MB)", "1v1")]
            [Choice("1 Month Discord Nitro (1500 MB)", "nitro")]
            [Choice("1 Bottle of AFA Water (50000 MB)", "water")]
            [Choice("Custom @everyone (100000 MB)", "ping")]
            string itemCode)
        {
            if (!_shopItems.TryGetValue(itemCode, out var itemToPurchase))
            {
                await RespondAsync("That item code is invalid. Please check the `/matt shop` for available items.", ephemeral: true);
                return;
            }

            int userBalance = GetUserBalance(Context.User.Id);

            if (userBalance < itemToPurchase.Price)
            {
                await RespondAsync($"{Context.User.Mention}, you don't have enough MattBucks {MATTBUCKS_EMOJI} to purchase **{itemToPurchase.Name}**.\n" +
                                   $"You need `{itemToPurchase.Price}` but only have `{userBalance}`.", ephemeral: true);
                return;
            }

            ModifyUserBalance(Context.User.Id, -itemToPurchase.Price); 

            string purchaseConfirmation = $"{Context.User.Mention}, you have successfully purchased **{itemToPurchase.Name}** for `{itemToPurchase.Price}` MattBucks {MATTBUCKS_EMOJI}!\n" +
                                          $"Your new balance is `{GetUserBalance(Context.User.Id)}` MattBucks.";
            await RespondAsync(purchaseConfirmation, ephemeral: true);

            if (MATTHEWZRING_USER_ID != 0)
            {
                try
                {
                    var matthewUser = await Context.Client.GetUserAsync(MATTHEWZRING_USER_ID);
                    if (matthewUser != null)
                    {
                        await matthewUser.SendMessageAsync(
                            $"**New MattBucks Purchase!** {MATTBUCKS_EMOJI}\n" +
                            $"- **User:** {Context.User.Username}#{Context.User.DiscriminatorValue} ({Context.User.Id})\n" +
                            $"- **Item:** {itemToPurchase.Name} ({itemCode})\n" +
                            $"- **Price:** {itemToPurchase.Price} MattBucks\n" +
                            $"- **Guild:** {Context.Guild?.Name ?? "N/A"} ({Context.Guild?.Id ?? 0})\n" +
                            $"- **Timestamp:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC"
                        );
                    }
                    else
                    {
                        Console.WriteLine($"MattBucks: Could not find user with ID {MATTHEWZRING_USER_ID} to send purchase DM.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"MattBucks: Error sending purchase DM to {MATTHEWZRING_USER_ID}: {ex.Message}");
                }
            }
        }

        [SlashCommand("richest", "shows the richest users")]
        public async Task CheckRichestAsync()
        {
            List<KeyValuePair<ulong, int>> richestUsersList;
            lock (_userMattBucks)
            {
                richestUsersList = _userMattBucks
                                .Where(kvp => kvp.Value > 0)
                                .OrderByDescending(kvp => kvp.Value)
                                .Take(10)
                                .ToList();
            }


            if (!richestUsersList.Any())
            {
                await RespondAsync($"Nobody has any MattBucks. Possible Error or fresh start.", ephemeral: true); // Adjusted message
                return;
            }

            var eb = new EmbedBuilder()
                .WithTitle($"Top MattBucks {MATTBUCKS_EMOJI} Holders")
                .WithColor(Color.Gold);

            var sb = new StringBuilder();
            int rank = 1;
            foreach (var entry in richestUsersList)
            {
                // As per instruction, not adding extra caching here. Relies on Discord.Net's GetUserAsync.
                var user = await Context.Client.GetUserAsync(entry.Key);
                var userName = user?.Username ?? $"User ID: {entry.Key}";
                sb.AppendLine($"{rank}. {userName} - `{entry.Value}` {MATTBUCKS_EMOJI}");
                rank++;
            }
            eb.WithDescription(sb.ToString());
            await RespondAsync(embed: eb.Build(), ephemeral: true);
        }

        [DefaultMemberPermissions(GuildPermission.Administrator)]
        [SlashCommand("modify", "Modify a user's MattBucks balance.")]
        public async Task ModifyAsync(
            [Summary("user", "The user whose balance you want to modify.")] SocketGuildUser targetUser,
            [Summary("amount", "The amount of MattBucks to add. Use a negative number to subtract.")] int amount)
        {
            int oldBalance = GetUserBalance(targetUser.Id);
            ModifyUserBalance(targetUser.Id, amount); 
            int newBalance = GetUserBalance(targetUser.Id);

            string action = amount >= 0 ? "Added" : "Removed";
            await RespondAsync($"{action} `{Math.Abs(amount)}` MattBucks {MATTBUCKS_EMOJI} {(amount >= 0 ? "to" : "from")} {targetUser.Mention}.\n" +
                               $"Old balance: `{oldBalance}`, New balance: `{newBalance}`.",
                               allowedMentions: AllowedMentions.None,
                               ephemeral: false);
        }
    }
}
