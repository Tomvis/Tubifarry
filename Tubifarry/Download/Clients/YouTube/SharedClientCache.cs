using Tubifarry.Core.Records;

namespace Tubifarry.Download.Clients.YouTube
{
    /// <summary>
    /// Tomvis fork (MUSIC-210): one client per key, rebuilt only when the session (tokens, cookies) changes.
    /// Lidarr's container keeps every indexer instance it resolves, so per-instance YouTube clients piled up;
    /// each streaming client lazily builds a Player whose Jint engines hold YouTube's parsed player JS
    /// (~14 MB each). 51 of them took Lidarr from ~0.1 to ~0.8 GB of managed heap in 12 h.
    /// Generic so tests need no YouTubeMusicAPI types (the plugin bundles its own copy).
    /// </summary>
    public sealed class SharedClientCache<T> where T : class
    {
        private readonly Dictionary<string, (ClientSessionInfo Session, T Client)> _entries = new();
        private readonly object _lock = new();

        public int Count { get { lock (_lock) return _entries.Count; } }

        public T GetOrCreate(string key, ClientSessionInfo session, Func<ClientSessionInfo, T> factory)
        {
            lock (_lock)
            {
                if (_entries.TryGetValue(key, out (ClientSessionInfo Session, T Client) entry) && entry.Session.IsEquivalentTo(session))
                    return entry.Client;

                T client = factory(session);
                _entries[key] = (session, client);
                return client;
            }
        }
    }
}
