using UnityEngine;
using System;

namespace Scripts.Services
{
    public abstract class InputManagerService : IInputManagerService
    {       
        public event Action<Vector2> OnLeftStickValueChanged;
        public event Action<Vector2> OnRightStickValueChanged;
        public event Action<bool> OnSpeedUpPressedChanged;
        public event Action<bool> OnSpeedDownPressedChanged;
        public event Action<bool> OnOptionsPressedChanged;
        public event Action<float> OnDpadValueChanged;
       
        public abstract void Update();

        private Vector2 _leftStickValue;
        private Vector2 _rightStickValue;
        private bool _isSpeedUpPressed;
        private bool _isSpeedDownPressed;
        private bool _isOptionsPressed;
        private float _dpadValue;

        private float _deadZone = 0.15f;

        public virtual void Reset()
        {
            _leftStickValue = Vector2.zero;
            _rightStickValue = Vector2.zero;
            _isSpeedUpPressed = false;
            _isSpeedDownPressed = false;
            _isOptionsPressed = false;
            _dpadValue = 0f;

            OnLeftStickValueChanged?.Invoke(_leftStickValue);
            OnRightStickValueChanged?.Invoke(_rightStickValue);
            OnSpeedUpPressedChanged?.Invoke(_isSpeedUpPressed);
            OnSpeedDownPressedChanged?.Invoke(_isSpeedDownPressed);
            OnOptionsPressedChanged?.Invoke(_isOptionsPressed);
            OnDpadValueChanged?.Invoke(_dpadValue);
        }

        protected void UpdateLeftStickValue(Vector2 newLeftStickValue)
        {           
            if (newLeftStickValue == _leftStickValue|| newLeftStickValue.magnitude < _deadZone)return;
            
            _leftStickValue = newLeftStickValue;
            OnLeftStickValueChanged?.Invoke(_leftStickValue);
                
        }

        protected void UpdateRightStickValue(Vector2 newRightStickValue)
        {           
            if (newRightStickValue == _rightStickValue|| newRightStickValue.magnitude < _deadZone)return;
            
            _rightStickValue = newRightStickValue;
            OnRightStickValueChanged?.Invoke(_rightStickValue);
        }

        protected void UpdateSpeedUpPressed(bool newSpeedUpPressed)
        {
            if (newSpeedUpPressed == _isSpeedUpPressed)return;
            
            _isSpeedUpPressed = newSpeedUpPressed;
            OnSpeedUpPressedChanged?.Invoke(_isSpeedUpPressed);
        }       

        protected void UpdateSpeedDownPressed(bool newSpeedDownPressed)
        {
            if (newSpeedDownPressed == _isSpeedDownPressed)return;
            
            _isSpeedDownPressed = newSpeedDownPressed;
            OnSpeedDownPressedChanged?.Invoke(_isSpeedDownPressed);
        }

        protected void UpdateOptionsPressed(bool newOptionsPressed)
        {
            if (newOptionsPressed == _isOptionsPressed)return;
            
            _isOptionsPressed = newOptionsPressed;
            OnOptionsPressedChanged?.Invoke(_isOptionsPressed);
        }

        protected void UpdateDpadValue(float newDpadValue)
        {
            if (newDpadValue == _dpadValue)return;
            
            _dpadValue = newDpadValue;
            OnDpadValueChanged?.Invoke(_dpadValue);
        }
        
    }
}
