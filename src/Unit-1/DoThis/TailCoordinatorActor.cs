using System;
using Akka.Actor;

namespace WinTail
{
    public class TailCoordinatorActor : UntypedActor
    {
        #region Message Types
        /// <summary>
        /// Start tailing the file at a user-specified path
        /// </summary>
        public class StartTail
        {
            public string FilePath { get; private set; }
            public IActorRef ReporterActor { get; private set; }

            public StartTail(string filePath, IActorRef reporterActor)
            {
                FilePath = filePath;
                ReporterActor = reporterActor;
            }
        }

        public class StopTail
        {
            public string FilePath { get; private set; }

            public StopTail(string filePath)
            {
                FilePath = filePath;
            }
        }
        #endregion

        protected override void OnReceive(object message)
        {
            if (message is StartTail)
            {
                var msg = message as StartTail;
                Context.ActorOf(Props.Create(() => new TailActor(msg.ReporterActor, msg.FilePath)));
            }
        }

        protected override SupervisorStrategy SupervisorStrategy()
        {
            return new OneForOneStrategy(
                10, // max number of retries
                TimeSpan.FromSeconds(30), // within time range
                x => // local only decider
                { 
                    // Ignore arithmetic exceptions and keep going
                    if (x is ArithmeticException) return Directive.Resume;

                    // Error that we can't recover from, stop the failing actor
                    else if (x is NotSupportedException) return Directive.Stop;

                    // In all other cases just restart the failing actor
                    else return Directive.Restart;
                });
        }
    }
}
