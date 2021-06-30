using System;
using Couchbase.Lite.Sync;


namespace UserProfileDemo.Core.Respositories
{
    public interface IRepository<T, K> : IDisposable
    {
        T Get(K id);
        bool Save(T obj);
        IObservable<ReplicatorStatus> SubscribeSyncStatus();
        IObservable<ReplicatorStatus> SubscribeSyncStatusContinuous();
        void Sync();
        void SyncContinuous(bool start);

    }
}
