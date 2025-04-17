using UnityEngine;
using System;

namespace Scripts.Services
{
    public interface IInputManagerService
    {
        event Action OnValueChanged;
        
        Vector2 LeftStickValue { get; }
        Vector2 RightStickValue { get; }
        bool SpeedUpPressed { get; }
        bool SpeedDownPressed { get; }
        bool OptionsPressed { get; }
        float DpadValue { get; }

        void Update();
        void Reset();
    }
} 