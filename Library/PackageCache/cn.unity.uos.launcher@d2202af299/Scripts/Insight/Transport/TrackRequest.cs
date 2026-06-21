using System;
using System.Collections.Generic;

namespace Unity.UOS.Insight.Transport
{
    [Serializable]
    public class TrackRequest
    {
        public List<TrackSingle> data;
    }
}