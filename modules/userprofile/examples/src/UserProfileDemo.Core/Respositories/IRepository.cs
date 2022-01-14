using System;
using Couchbase.Lite.Sync;

namespace UserProfileDemo.Core.Respositories
{
    public interface IRepository<T, K> : IDisposable
    {
        T Get(K id);
        bool Save(T obj);
        IObservable<(ReplicatorStatus status, int count)> SubscribeSyncStatus();
        void Sync();
        void GetStatus();
        void Stop();
    }
}
