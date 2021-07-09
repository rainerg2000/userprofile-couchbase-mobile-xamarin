using System;
using System.Threading;
using System.Threading.Tasks;
using System.Reactive.Subjects;
using System.Runtime.InteropServices.ComTypes;
using Couchbase.Lite;
using Couchbase.Lite.Sync;
using UserProfileDemo.Core.Respositories;
using UserProfileDemo.Core.Services;
using Xamarin.Forms;

namespace UserProfileDemo.Respositories
{
    public abstract class BaseRepository<T,K> : IRepository<T,K> where T : class
    {
        string DatabaseName { get; set; }
        ListenerToken DatabaseListenerToken { get; set; }

        protected virtual DatabaseConfiguration DatabaseConfig { get; set; }
        private IAuth AuthSvc;
        private Replicator _repl;
        private Replicator _replContinuous;
        private BehaviorSubject<ReplicatorStatus> _replStatus;
        private BehaviorSubject<ReplicatorStatus> _replStatusContinuous;
        private bool _busy;
        private bool _nextReplicationQueued;

        private SessionAuthenticator _syncSession;
        private SessionAuthenticator SyncSession
        {
            get
            {
                if (_syncSession == null)
                {
                    _syncSession = new SessionAuthenticator(AuthSvc
                        .GetSgSessionToken(new Uri("https://nsc-1-sync.perftest-homemonitoring.com/nro"))
                        .Result);
                }

                return _syncSession;
            }
        }

        private Replicator Repl
        {
            get
            {
                if (_repl == null)
                {
                    var replConfig = new ReplicatorConfiguration(Database,
                        new URLEndpoint(new Uri("wss://nsc-1-sync.perftest-homemonitoring.com/nro")));
                    replConfig.Authenticator = SyncSession;
                    replConfig.ReplicatorType = ReplicatorType.Pull;
                    _repl = new Replicator(replConfig);
                }

                _repl.AddChangeListener((sender, args) =>
                {
                    _replStatus.OnNext(args.Status);
                    if (args.Status.Activity == ReplicatorActivityLevel.Stopped || args.Status.Activity == ReplicatorActivityLevel.Offline)
                    {
                        _busy = false;
                        if (_nextReplicationQueued)
                        {
                            Sync();
                            _nextReplicationQueued = false;
                        }
                    }
                });
                return _repl;
            }
        }

        private Replicator ReplContinuous
        {
            get
            {
                if (_replContinuous == null)
                {
                    var replConfig = new ReplicatorConfiguration(Database,
                        new URLEndpoint(new Uri("wss://nsc-1-sync.perftest-homemonitoring.com/nro")));
                    replConfig.Authenticator = SyncSession;
                    replConfig.ReplicatorType = ReplicatorType.Pull;
                    replConfig.Continuous = true;
                    _replContinuous = new Replicator(replConfig);
                }

                _replContinuous.AddChangeListener((sender, args) => _replStatusContinuous.OnNext(args.Status));
                return _replContinuous;
            }
        }

        // tag::database[]
        Database _database;

        protected Database Database
        {
            get
            {
                if (_database == null)
                {
                    // tag::databaseCreate[]
                    _database = new Database(DatabaseName, DatabaseConfig);
                    // end::databaseCreate[]
                }

                return _database;
            }
            private set => _database = value;
        }
        // end::database[]

        protected BaseRepository(string databaseName)
        {
            if (string.IsNullOrEmpty(databaseName))
            {
                throw new Exception($"Repository Exception: Database name cannot be null or empty!");
            }

            DatabaseName = databaseName;

            // tag::registerForDatabaseChanges[]
            DatabaseListenerToken = Database.AddChangeListener(OnDatabaseChangeEvent);
            // end::registerForDatabaseChanges[]
            AuthSvc = DependencyService.Get<IAuth>();
            _replStatus = new BehaviorSubject<ReplicatorStatus>(new ReplicatorStatus());
            _replStatusContinuous = new BehaviorSubject<ReplicatorStatus>(new ReplicatorStatus());
        }

        // tag::addChangeListener[]
        void OnDatabaseChangeEvent(object sender, DatabaseChangedEventArgs e)
        {
            foreach (var documentId in e.DocumentIDs)
            {
                var document = Database?.GetDocument(documentId);

                string message = $"Document (id={documentId}) was ";

                if (document == null)
                {
                    message += "deleted";
                }
                else
                {
                    message += "added/updaAted";
                }

                Console.WriteLine(message);
            }
        }
        // end::addChangeListener[]

        // tag::databaseClose[]
        public void Dispose()
        {
            DatabaseConfig = null;

            Database.RemoveChangeListener(DatabaseListenerToken);
            Database.Close();
            Database = null;
        }
        // end::databaseClose[]

        public abstract T Get(K id);
        public abstract bool Save(T obj);
        public IObservable<ReplicatorStatus> SubscribeSyncStatus()
        {
            return _replStatus;
        }

        public IObservable<ReplicatorStatus> SubscribeSyncStatusContinuous()
        {
            return _replStatusContinuous;
        }

        public void Sync()
        {
            if (_busy)
            {
                if (_nextReplicationQueued)
                {
                    // nothing to do follow-on replication is already queued
                    return;
                }

                _nextReplicationQueued = true;
                return;
            }
            _busy = true;
            Task.Run(() => Repl.Start());
        }

        public void SyncContinuous(bool start)
        {
            if (start)
            {
                // running on other thread, to allow OAuth popup if user interaction is required
                Task.Run(() => ReplContinuous.Start());
            }
            else
            {
                ReplContinuous.Stop();
            }
        }
    }
}
