using UnityEngine;
using System;

namespace Scripts.Services
{
    public interface IInputManagerService
    {
        event Action<Vector2> OnLeftStickValueChanged;
        event Action<Vector2> OnRightStickValueChanged;
        event Action<bool> OnSpeedUpPressedChanged;
        event Action<bool> OnSpeedDownPressedChanged;
        event Action<bool> OnOptionsPressedChanged;
        event Action<float> OnDpadValueChanged;

        void Update();
        void Reset();
    }
} 