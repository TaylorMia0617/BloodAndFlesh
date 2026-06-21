namespace Unity.UOS.Insight
{
    public partial class InsightSDK
    {
        private static bool _isInRoom;

        // used by Sync Realtime/Relay SDK
        public static bool IsInRoom
        {
            get => _isInRoom;
            set
            {
                if (!_initialized)
                {
                    _operationCache.Enqueue(() => { IsInRoom = value; });
                    return;
                }

                if (!_enabled)
                    return;

                _isInRoom = value;
            }
        }
    }
}