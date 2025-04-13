using UnityEngine;
using UnityEngine.InputSystem;
using System;

namespace Scripts.Services
{
    public class KeyboardInputManagerService : InputManagerService
    {
        private KeyboardInput.KeyboardControlActions _keyboardActions;

        public KeyboardInputManagerService()
        {
            var gameInput = new KeyboardInput();
            _keyboardActions = gameInput.KeyboardControl;
            gameInput.Enable();
        }

        public override void Update()
        {
            float leftValue = _keyboardActions.LeftFrontKey.IsPressed() ? 1f : _keyboardActions.LeftBackKey.IsPressed() ? -1f : 0f;
            UpdateLeftStickValue(new Vector2(0f, leftValue));          

            float rightValue = _keyboardActions.RightFrontKey.IsPressed() ? 1f : _keyboardActions.RightBackKey.IsPressed() ? -1f : 0f;
            UpdateRightStickValue(new Vector2(0f, rightValue));            

            UpdateSpeedUpPressed(_keyboardActions.SpeedUp.IsPressed());           

            UpdateSpeedDownPressed(_keyboardActions.SpeedDown.IsPressed());           

            UpdateOptionsPressed(_keyboardActions.Settings.IsPressed());          

            UpdateDpadValue(_keyboardActions.CameraLeft.IsPressed() ? -1f : _keyboardActions.CameraRight.IsPressed() ? 1f : 0f);
        }
    }
} 