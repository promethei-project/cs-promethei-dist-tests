using Logging;

namespace PrometheiClient
{
    public interface IProcessControlFactory
    {
        IProcessControl CreateProcessControl(IPrometheiInstance instance);
    }

    public interface IProcessControl
    {
        void Stop(bool waitTillStopped);
        void Restart();
        IDownloadedLog DownloadLog(LogFile file);
        void DeleteDataDirFolder();
        bool HasCrashed();
    }

    public class DoNothingProcessControlFactory : IProcessControlFactory
    {
        public IProcessControl CreateProcessControl(IPrometheiInstance instance)
        {
            return new DoNothingProcessControl();
        }
    }

    public class DoNothingProcessControl : IProcessControl
    {
        public void DeleteDataDirFolder()
        {
        }

        public IDownloadedLog DownloadLog(LogFile file)
        {
            throw new NotImplementedException("Not supported by DoNothingProcessControl");
        }

        public bool HasCrashed()
        {
            return false;
        }

        public void Stop(bool waitTillStopped)
        {
        }

        public void Restart()
        {
        }
    }
}
