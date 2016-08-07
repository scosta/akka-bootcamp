using System;
using System.Linq;
using Akka.Actor;
using Akka.Routing;

namespace GithubActors.Actors
{
    /// <summary>
    /// Top-level actor responsible for coordinating and launching repo-processing jobs
    /// </summary>
    public class GithubCommanderActor : ReceiveActor, IWithUnboundedStash
    {
        #region Message classes

        public class CanAcceptJob
        {
            public CanAcceptJob(RepoKey repo)
            {
                Repo = repo;
            }

            public RepoKey Repo { get; private set; }
        }

        public class AbleToAcceptJob
        {
            public AbleToAcceptJob(RepoKey repo)
            {
                Repo = repo;
            }

            public RepoKey Repo { get; private set; }
        }

        public class UnableToAcceptJob
        {
            public UnableToAcceptJob(RepoKey repo)
            {
                Repo = repo;
            }

            public RepoKey Repo { get; private set; }
        }

        #endregion

        private IActorRef _coordinator;
        private IActorRef _canAcceptJobSender;
        private int pendingJobReplies;
        private RepoKey _repoJob;

        public GithubCommanderActor()
        {
            Ready();
        }

        private void Ready()
        {
            Receive<CanAcceptJob>(job =>
            {
                _coordinator.Tell(job);
                _repoJob = job.Repo;
                BecomeAsking();
            });
        }

        private void BecomeAsking()
        {
            _canAcceptJobSender = Sender;
            pendingJobReplies = _coordinator.Ask<Routees>(new GetRoutees())
                .Result.Members.Count();
            Become(Asking);

            // Send ourselves a receive timeout message if no message within 3 seconds
            Context.SetReceiveTimeout(TimeSpan.FromSeconds(3));
        }

        private void Asking()
        {
            // Stash any subsequent requests
            Receive<CanAcceptJob>(job => Stash.Stash());

            Receive<ReceiveTimeout>(timeout =>
            {
                _canAcceptJobSender.Tell(new UnableToAcceptJob(_repoJob));
                BecomeReady();
            });

            Receive<UnableToAcceptJob>(job =>
            {
                pendingJobReplies--;
                if (pendingJobReplies == 0)
                {
                    _canAcceptJobSender.Tell(job);
                    BecomeReady();
                }
            });

            Receive<AbleToAcceptJob>(job =>
            {
                _canAcceptJobSender.Tell(job);

                // Start processing messages
                Sender.Tell(new GithubCoordinatorActor.BeginJob(job.Repo));

                // Launch the new window to view processing results
                Context.ActorSelection(ActorPaths.MainFormActor.Path).Tell(
                    new MainFormActor.LaunchRepoResultsWindow(job.Repo, Sender));

                BecomeReady();
            });
        }

        private void BecomeReady()
        {
            Become(Ready);
            Stash.UnstashAll();

            // Cancel ReceiveTimeout
            Context.SetReceiveTimeout(null);
        }

        protected override void PreStart()
        {
            // Create a broadcast router that will ask all of them if they are available to work
            _coordinator =
                Context.ActorOf(Props.Create(() => new GithubCoordinatorActor())
                    .WithRouter(FromConfig.Instance),
                    ActorPaths.GithubCoordinatorActor.Name);
            base.PreStart();
        }

        protected override void PreRestart(Exception reason, object message)
        {
            //kill off the old coordinator so we can recreate it from scratch
            _coordinator.Tell(PoisonPill.Instance);
            base.PreRestart(reason, message);
        }

        public IStash Stash { get; set; }
    }
}
