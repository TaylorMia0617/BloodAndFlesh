using System;
using System.Collections.Generic;

namespace Unity.UOS.Insight.Transport
{
    [Serializable]
    public class TrackSingle
    {
        public string user_id;

        public DateTime time;

        public string event_name;

        public Dictionary<string, object> properties;
    }
}