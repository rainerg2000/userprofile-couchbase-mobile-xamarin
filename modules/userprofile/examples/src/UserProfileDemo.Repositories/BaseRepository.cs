using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Reactive.Subjects;
using System.Reflection;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography.X509Certificates;
using Couchbase.Lite;
using Couchbase.Lite.Sync;
using UserProfileDemo.Core.Respositories;
using UserProfileDemo.Core.Services;
using Xamarin.Forms;

namespace UserProfileDemo.Respositories
{
    public abstract class BaseRepository<T,K> : IRepository<T,K> where T : class
    {
        //private const string SgHost = "sync-gateway.intg-3.msei.nro.biotronik.dev/neurodev";
        private const string SgHost = "nsc-1-sync.perftest-homemonitoring.com/nro";
        //private const string CertFileName = "UserProfileDemo.Repositories.intg_3_msei_nro_biotronik_dev.cer";
        private const string CertFileName = "UserProfileDemo.Repositories.perftest_homemonitoring_com.cer";

        string DatabaseName { get; set; }
        ListenerToken DatabaseListenerToken { get; set; }

        protected virtual DatabaseConfiguration DatabaseConfig { get; set; }
        private IAuth AuthSvc;
        private Replicator _repl;
        private BehaviorSubject<(ReplicatorStatus status, int count)> _replStatus;
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
                        .GetSgSessionToken(new Uri($"https://{SgHost}"))
                        .Result);
                }

                return _syncSession;
            }
        }

        public byte[] ReadCert(string fileName)
        {
            //Assembly assembly = Assembly.GetExecutingAssembly();
            var assembly = IntrospectionExtensions.GetTypeInfo(typeof(UserProfileRepository)).Assembly;
            string[] resources = assembly.GetManifestResourceNames();
            Stream f = assembly.GetManifestResourceStream(fileName);
            int size = (int)f.Length;
            byte[] data = new byte[size];
            var sizeRead = f.Read(data, 0, size);
            f.Close();
            return data;
        }



        private DateTime replStarted;

        private Replicator Repl
        {
            get
            {
                if (_repl == null)
                {
                    var replConfig = BuildReplConfig();

                    _repl = new Replicator(replConfig);

                    _repl.AddChangeListener((sender, args) =>
                    {

                        _replStatus.OnNext((args.Status, replCounter));
                        if (args.Status.Activity == ReplicatorActivityLevel.Stopped ||
                            args.Status.Activity == ReplicatorActivityLevel.Offline)
                        {
 
                            _busy = false;
                            if (_nextReplicationQueued)
                            {
                                Sync();
                                _nextReplicationQueued = false;
                            }
                        }

                    });
                }
                return _repl;
            }
        }

        private ReplicatorConfiguration BuildReplConfig()
        {
            byte[] rawData = ReadCert(CertFileName);
            var cert = new X509Certificate2(rawData);

            var replConfig = new ReplicatorConfiguration(Database,
                new URLEndpoint(new Uri($"wss://{SgHost}")));
            replConfig.Authenticator = SyncSession;
            replConfig.ReplicatorType = ReplicatorType.Pull;
            replConfig.Continuous = false;
            replConfig.PinnedServerCertificate = cert;
            return replConfig;
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
            _replStatus = new BehaviorSubject<(ReplicatorStatus, int)>((new ReplicatorStatus(), 0));
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
        public IObservable<(ReplicatorStatus status, int count)> SubscribeSyncStatus()
        {
            return _replStatus;
        }

        public void GetStatus()
        {
            var status = Repl.Status;
            _replStatus.OnNext((status, replCounter));
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
            Task.Run(() =>
            {
                replCounter++;
                Repl.Start();
            });

            DateTime utcNow = DateTime.UtcNow;
            var duration = utcNow - replStarted;
            if (duration > TimeSpan.FromMilliseconds(30000))
            {
                System.Diagnostics.Debug.WriteLine($"{utcNow} time since last replication much longer than expected {duration.TotalSeconds} seconds");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"{utcNow} time since last replication {duration.TotalSeconds} seconds");
            }
            replStarted = utcNow;
        }

        private int replCounter = 0;

    }
}
