

namespace Scripts.Services
{
    public interface IStatus
    {
        void UpdateServerStatus(bool isConnected);
        void UpdateRobotStatus(bool isConnected);
       
        void Error(string message);
        void Info(string message);  
    }
}
    