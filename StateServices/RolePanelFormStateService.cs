using System.Collections.Concurrent;
using tsgsBot_C_.Models;

namespace tsgsBot_C_.StateServices
{
    public sealed class RolePanelFormStateService
    {
        private readonly ConcurrentDictionary<ulong, UserRolePanelFormState> _userStates = new();

        public UserRolePanelFormState GetOrCreate(ulong userId)
        {
            return _userStates.GetOrAdd(userId, _ => new UserRolePanelFormState());
        }

        public bool TryGet(ulong userId, out UserRolePanelFormState? state)
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
            foreach (KeyValuePair<ulong, UserRolePanelFormState> kvp in _userStates.ToArray())
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

    public class UserRolePanelFormState
    {
        public string Title { get; set; } = "Self-assign Roles";
        public string Description { get; set; } = "";
        public List<ulong> RoleIds { get; set; } = new();
        public List<ulong> SkippedRoleIds { get; set; } = new();
        public List<string> ButtonLabels { get; set; } = new();
        public string? ImageUrl { get; set; }
        public ulong? OriginalMessageId { get; set; }

        // used as a timestamp for cleanup (e.g. expire after 30 min)
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
