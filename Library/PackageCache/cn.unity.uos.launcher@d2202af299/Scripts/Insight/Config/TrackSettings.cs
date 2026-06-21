using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.UOS.Insight.Config
{
    public class TrackSettings : ScriptableObject
    {
        public Dictionary<string, bool> ToConfig()
        {
            return new Dictionary<string, bool>()
            {
                ["list_rooms"] = sync.room.viewRooms,
                ["list_subscribed_channels"] = push.message.viewSubscribed,
                ["list_public_channels"] = push.message.viewPublic,
                ["get_player_teams"] = push.team.viewCurrent,
                ["pick_teams"] = push.team.viewTeams,
                ["list_historical_teammates"] = push.team.viewTeammates,
                ["list_persona_achievements"] = passport.achievement.viewAchievements,
                ["get_completed_statistics"] = passport.achievement.viewCompleted,
                ["get_completed_statistics_by_slug"] = passport.achievement.viewCompleted,
                ["get_persona_battlepass"] = passport.battlepass.viewBattlepass,
                ["get_product"] = passport.economy.viewProductDetail,
                ["list_products"] = passport.economy.viewProducts,
                ["search_persona_inventory"] = passport.economy.viewInventories,
                ["list_persona_inventory"] = passport.economy.viewInventories,
                ["get_current_guild"] = passport.guild.viewCurrent,
                ["list_random_guilds"] = passport.guild.viewGuilds,
                ["search_guilds"] = passport.guild.viewGuilds,
                ["list_leaderboards"] = passport.leaderboard.viewLeaderboards,
                ["list_scores"] = passport.leaderboard.viewScore,
                ["get_score"] = passport.leaderboard.viewScore,
                ["list_member_bucket_scores"] = passport.leaderboard.viewScore,
                ["get_friends_leaderboard"] = passport.leaderboard.viewFriend,
                ["get_member_history_score"] = passport.leaderboard.viewHistory,
                ["get_entity_history_score"] = passport.leaderboard.viewHistory,
                ["list_history_scores"] = passport.leaderboard.viewHistory,
                ["search_persona_quests"] = passport.quest.viewQuests,
            };
        }

        [Serializable]
        public class Sync
        {
            [Serializable]
            public class Room
            {
                [Tooltip("查看房间列表")]
                public bool viewRooms;
            }

            public Room room;
        }

        public Sync sync;

        [Serializable]
        public class Push
        {
            [Serializable]
            public class Message
            {
                [Tooltip("查看已订阅的消息频道列表")]
                public bool viewSubscribed;

                [Tooltip("查看公共消息频道列表")]
                public bool viewPublic;
            }

            public Message message;

            [Serializable]
            public class Team
            {
                [Tooltip("查看当前队伍列表")]
                public bool viewCurrent;

                [Tooltip("随机获取队伍列表")]
                public bool viewTeams;

                [Tooltip("查看历史队友列表")]
                public bool viewTeammates;
            }

            public Team team;
        }

        public Push push;

        [Serializable]
        public class Passport
        {
            [Serializable]
            public class Achievement
            {
                [Tooltip("查看成就列表")]
                public bool viewAchievements;

                [Tooltip("查看成就完成人数信息")]
                public bool viewCompleted;
            }

            public Achievement achievement;

            [Serializable]
            public class Battlepass
            {
                [Tooltip("查看战令信息")]
                public bool viewBattlepass;
            }

            public Battlepass battlepass;

            [Serializable]
            public class Economy
            {
                [Tooltip("查看商品列表")]
                public bool viewProducts;

                [Tooltip("查看商品详情")]
                public bool viewProductDetail;

                [Tooltip("查看背包资源列表")]
                public bool viewInventories;
            }

            public Economy economy;

            [Serializable]
            public class Guild
            {
                [Tooltip("查看当前公会")]
                public bool viewCurrent;

                [Tooltip("查看公会列表")]
                public bool viewGuilds;
            }

            public Guild guild;

            [Serializable]
            public class Leaderboard
            {
                [Tooltip("查看排行榜列表")]
                public bool viewLeaderboards;

                [Tooltip("查看排行榜分数信息")]
                public bool viewScore;

                [Tooltip("查看好友分数信息")]
                public bool viewFriend;

                [Tooltip("查看排行榜的历史信息")]
                public bool viewHistory;
            }

            public Leaderboard leaderboard;

            [Serializable]
            public class Quest
            {
                [Tooltip("查看角色任务")]
                public bool viewQuests;
            }

            public Quest quest;
        }

        public Passport passport;
    }
}