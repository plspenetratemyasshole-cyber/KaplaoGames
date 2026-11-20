using System.Reflection;
using Mirror;
using UnityEngine;

namespace KaplaoGames.Server
{
    /// <summary>
    /// Re-sends spawn messages to one connection without SetClientNotReady,
    /// which would leave NetworkClient.ready=false and block all Commands.
    /// </summary>
    static class KaplaoNetworkObserverUtil
    {
        static readonly MethodInfo AddObserverMethod = typeof(NetworkIdentity).GetMethod(
            "AddObserver",
            BindingFlags.Instance | BindingFlags.NonPublic);

        static readonly MethodInfo RemoveObserverMethod = typeof(NetworkIdentity).GetMethod(
            "RemoveObserver",
            BindingFlags.Instance | BindingFlags.NonPublic);

        static readonly MethodInfo RemoveFromObservingMethod = typeof(NetworkConnectionToClient).GetMethod(
            "RemoveFromObserving",
            BindingFlags.Instance | BindingFlags.NonPublic);

        public static void ForceResyncIdentityForConnection(NetworkIdentity identity, NetworkConnectionToClient conn)
        {
            if (identity == null || conn == null || !conn.isReady)
                return;

            if (AddObserverMethod == null || RemoveObserverMethod == null || RemoveFromObservingMethod == null)
            {
                Debug.LogWarning("[KaplaoNetworkObserverUtil] Mirror observer APIs not found.");
                return;
            }

            if (identity.observers.ContainsKey(conn.connectionId))
            {
                RemoveFromObservingMethod.Invoke(conn, new object[] { identity, false });
                RemoveObserverMethod.Invoke(identity, new object[] { conn });
            }

            AddObserverMethod.Invoke(identity, new object[] { conn });
        }
    }
}
