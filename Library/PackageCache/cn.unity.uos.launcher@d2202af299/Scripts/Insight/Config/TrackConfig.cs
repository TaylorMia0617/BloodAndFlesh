using System.Collections.Generic;
using UnityEngine;

namespace Unity.UOS.Insight.Config
{
    public static class TrackConfig
    {
        private static Dictionary<string, bool> Config { get; set; } = new()
        {
            ["list_rooms"] = false,
            ["list_subscribed_channels"] = false,
            ["list_public_channels"] = false,
            ["get_player_teams"] = false,
            ["pick_teams"] = false,
            ["list_historical_teammates"] = false,
            ["list_persona_achievements"] = false,
            ["get_completed_statistics"] = false,
            ["get_completed_statistics_by_slug"] = false,
            ["get_persona_battlepass"] = false,
            ["get_product"] = false,
            ["list_products"] = false,
            ["search_persona_inventory"] = false,
            ["list_persona_inventory"] = false,
            ["get_current_guild"] = false,
            ["list_random_guilds"] = false,
            ["search_guilds"] = false,
            ["list_leaderboards"] = false,
            ["list_scores"] = false,
            ["get_score"] = false,
            ["list_member_bucket_scores"] = false,
            ["get_friends_leaderboard"] = false,
            ["get_member_history_score"] = false,
            ["get_entity_history_score"] = false,
            ["list_history_scores"] = false,
            ["search_persona_quests"] = false,
        };

        public static void LoadFromLocal()
        {
            var settings = Resources.Load<TrackSettings>(nameof(TrackSettings));
            if (!settings)
                return;
            Config = settings.ToConfig();
        }

        public static bool IsEnabled(string key)
        {
            return Config?.TryGetValue(key, out var value) is true && value;
        }
    }
}