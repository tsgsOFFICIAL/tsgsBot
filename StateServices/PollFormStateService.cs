using System.Collections.Concurrent;
using tsgsBot_C_.Models;

namespace tsgsBot_C_.StateServices
{
    public sealed class PollFormStateService
    {
        private readonly ConcurrentDictionary<ulong, UserPollFormState> _userStates = new();
        
        public UserPollFormState GetOrCreate(ulong userId)
        {
            return _userStates.GetOrAdd(userId, _ => new UserPollFormState());
        }

        public bool TryGet(ulong userId, out UserPollFormState? state)
        {
            return _userStates.TryGetValue(userId, out state);
        }

        public void Clear(ulong userId)
        {
            _userStates.TryRemove(userId, out _);
        }

        public int Cleanup(TimeSpan olderThan)
        {
            DateTimeOffset cutoff = DateTimeOffset.UtcNow - olderThan;
            int removed = 0;

            // ToArray() avoids modification-during-enumeration issues
            foreach (KeyValuePair<ulong, UserPollFormState> kvp in _userStates.ToArray())
            {
                if (kvp.Value.CreatedAt < cutoff)
                {
                    if (_userStates.TryRemove(kvp.Key, out _))
                        removed++;
                }
            }

            return removed;
        }
    }
    
    public class UserPollFormState
    {
        public int DurationMinutes { get; set; }
        public PollModalModel? ModalData { get; set; }

        // used as a timestamp for cleanup (e.g. expire after 30 min)
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}