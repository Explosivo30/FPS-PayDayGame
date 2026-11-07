using System.Collections.Generic;

namespace EZCollabTool
{
    public class EZCollabLockManager
    {
        readonly object syncRoot = new object();
        readonly Dictionary<string, string> locks = new Dictionary<string, string>();

        public bool TryAcquire(string guid, string peerId)
        {
            lock (syncRoot)
            {
                if (locks.TryGetValue(guid, out string current))
                    return current == peerId;

                locks[guid] = peerId;
                return true;
            }
        }

        public void Release(string guid, string peerId)
        {
            lock (syncRoot)
            {
                if (locks.TryGetValue(guid, out string current) && current == peerId)
                    locks.Remove(guid);
            }
        }

        public List<string> ReleaseAll(string peerId)
        {
            lock (syncRoot)
            {
                List<string> released = new List<string>();
                List<string> keys = new List<string>(locks.Keys);

                foreach (string key in keys)
                {
                    if (locks[key] != peerId) continue;

                    released.Add(key);
                    locks.Remove(key);
                }

                return released;
            }
        }

        public bool IsLockedBy(string guid, string peerId)
        {
            lock (syncRoot)
                return locks.TryGetValue(guid, out string current) && current == peerId;
        }

        public string GetOwner(string guid)
        {
            lock (syncRoot)
            {
                locks.TryGetValue(guid, out string owner);
                return owner;
            }
        }

        public Dictionary<string, string> GetAll()
        {
            lock (syncRoot)
                return new Dictionary<string, string>(locks);
        }
    }
}
