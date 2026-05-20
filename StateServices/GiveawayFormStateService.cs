using System.Collections.Concurrent;
using tsgsBot_C_.Models;

namespace tsgsBot_C_.StateServices
{
    public sealed class GiveawayFormStateService
    {
        private readonly ConcurrentDictionary<ulong, UserGiveawayFormState> _userStates = new();

        public UserGiveawayFormState GetOrCreate(ulong userId)
        {
            return _userStates.GetOrAdd(userId, _ => new UserGiveawayFormState());
        }

        public bool TryGet(ulong userId, out UserGiveawayFormState? state)
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
            foreach (KeyValuePair<ulong, UserGiveawayFormState> kvp in _userStates.ToArray())
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

    public class UserGiveawayFormState
    {
        public int DurationMinutes { get; set; }
        public GiveawayModalModel? ModalData { get; set; }
        public string? ImageUrl { get; set; }
        public ulong? OriginalMessageId { get; set; }
        public ulong? OriginalChannelId { get; set; }
        public int? GiveawayId { get; set; }
        public DateTime? EndTimeUtc { get; set; }

        public bool IsEditMode => OriginalMessageId.HasValue && GiveawayId.HasValue;

        // used as a timestamp for cleanup (e.g. expire after 30 min)
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}