using System;

namespace Scripts.Services
{
    public interface ICommandSenderService : IDisposable
    {  
        int CommandsPerSecond { get; set; }
        void Update(float deltaTime);
    }
} 