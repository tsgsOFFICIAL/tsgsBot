using System.Collections.Concurrent;
using tsgsBot_C_.Models;

namespace tsgsBot_C_.StateServices
{
    public class SupportFormStateService
    {
        private readonly ConcurrentDictionary<ulong, UserSupportFormState> _userStates = new();
        /// <summary>
        /// Retrieves the support form state for the specified user, or creates a new state if none exists.
        /// </summary>
        /// <param name="userId">The unique identifier of the user whose support form state is to be retrieved or created.</param>
        /// <returns>The existing or newly created <see cref="UserSupportFormState"/> instance associated with the specified
        /// user.</returns>
        public UserSupportFormState GetOrCreate(ulong userId)
        {
            return _userStates.GetOrAdd(userId, _ => new UserSupportFormState());
        }
        /// <summary>
        /// Attempts to retrieve the support form state associated with the specified user identifier.
        /// </summary>
        /// <param name="userId">The unique identifier of the user whose support form state is to be retrieved.</param>
        /// <param name="state">When this method returns, contains the support form state for the specified user if found; otherwise, <see
        /// langword="null"/>.</param>
        /// <returns><see langword="true"/> if the support form state was found for the specified user; otherwise, <see
        /// langword="false"/>.</returns>
        public bool TryGet(ulong userId, out UserSupportFormState? state)
        {
            return _userStates.TryGetValue(userId, out state);
        }
        /// <summary>
        /// Removes all state information associated with the specified user.
        /// </summary>
        /// <param name="userId">The unique identifier of the user whose state should be cleared.</param>
        public void Clear(ulong userId)
        {
            _userStates.TryRemove(userId, out _);
        }
        /// <summary>
        /// Removes user support form states that were created before the specified time interval.
        /// </summary>
        /// <param name="olderThan">The time interval. User states created earlier than the current time minus this interval are removed.</param>
        /// <returns>The number of user support form states that were removed.</returns>
        public int Cleanup(TimeSpan olderThan)
        {
            DateTimeOffset cutoff = DateTimeOffset.UtcNow - olderThan;
            int removed = 0;

            // ToArray() avoids modification-during-enumeration issues
            foreach (KeyValuePair<ulong, UserSupportFormState> kvp in _userStates.ToArray())
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
}