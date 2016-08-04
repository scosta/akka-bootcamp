using System.IO;
using Akka.Actor;

namespace WinTail
{
    /// <summary>
    /// Actor that validates user input and signals result to others.
    /// </summary>
    public class FileValidatorActor : UntypedActor
    {
        private readonly IActorRef _consoleWriterActor;

        public FileValidatorActor(IActorRef consoleWriterActor)
        {
            _consoleWriterActor = consoleWriterActor;
        }

        protected override void OnReceive(object message)
        {
            var msg = message as string;
            if (string.IsNullOrEmpty(msg))
            {
                // Signal that the user needs to supply an input
                _consoleWriterActor.Tell(new Messages.NullInputError("Input was blank.  Please try again.\n"));

                // Tell sender to continue doing its thing
                Sender.Tell(new Messages.ContinueProcessing());
            }
            else
            {
                var valid = IsFileUri(msg);
                if (valid)
                {
                    // Signal successful input
                    _consoleWriterActor.Tell(new Messages.InputSuccess(
                        string.Format("Starting processing for {0}", msg)));

                    // Start coordinator
                    Context.ActorSelection("akka://MyActorSystem/user/tailCoordinatorActor").Tell(
                        new TailCoordinatorActor.StartTail(msg, _consoleWriterActor));
                }
                else
                {
                    // Signal that input was bad
                    _consoleWriterActor.Tell(new Messages.ValidationError(
                        string.Format("{0} is not an existing URI on disk.", msg)));

                    // Tell sender to continue doing its thing
                    Sender.Tell(new Messages.ContinueProcessing());
                }
            }
        }

        /// <summary>
        /// Checks if file exists at path provided by user.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private static bool IsFileUri(string path)
        {
            return File.Exists(path);
        }
    }
}
