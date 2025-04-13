//------------------------------------------------------------------------------
// <auto-generated>
//     This code was auto-generated by com.unity.inputsystem:InputActionCodeGenerator
//     version 1.11.2
//     from Assets/InputActions/KeyboardInput.inputactions
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

public partial class @KeyboardInput: IInputActionCollection2, IDisposable
{
    public InputActionAsset asset { get; }
    public @KeyboardInput()
    {
        asset = InputActionAsset.FromJson(@"{
    ""name"": ""KeyboardInput"",
    ""maps"": [
        {
            ""name"": ""Keyboard Control"",
            ""id"": ""47ab8a32-04ae-4f6a-82e0-0bff67ef05a3"",
            ""actions"": [
                {
                    ""name"": ""CameraLeft"",
                    ""type"": ""Button"",
                    ""id"": ""d64c5698-9d31-48db-b2cd-ca49b69dcef1"",
                    ""expectedControlType"": """",
                    ""processors"": """",
                    ""interactions"": ""Hold"",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""CameraRight"",
                    ""type"": ""Button"",
                    ""id"": ""151a9ec5-a8a8-49fa-a439-d1858b2eed1b"",
                    ""expectedControlType"": """",
                    ""processors"": """",
                    ""interactions"": ""Hold"",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""SpeedUp"",
                    ""type"": ""Button"",
                    ""id"": ""bf8a8aaa-d661-49ba-86d0-2a481e8cd278"",
                    ""expectedControlType"": """",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""SpeedDown"",
                    ""type"": ""Button"",
                    ""id"": ""f8cfbbca-fa76-4bd0-a104-1a54b8021253"",
                    ""expectedControlType"": """",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""Settings"",
                    ""type"": ""Button"",
                    ""id"": ""a016ae8f-2282-43b2-b206-a9f553374d32"",
                    ""expectedControlType"": """",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""LeftFrontKey"",
                    ""type"": ""Button"",
                    ""id"": ""47ec79ec-f059-448f-8fc1-6f635cc7cfed"",
                    ""expectedControlType"": """",
                    ""processors"": """",
                    ""interactions"": ""Press(behavior=1)"",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""LeftBackKey"",
                    ""type"": ""Button"",
                    ""id"": ""03b83956-e3ca-4277-90e4-9f771b942fbf"",
                    ""expectedControlType"": """",
                    ""processors"": """",
                    ""interactions"": ""Press(behavior=1)"",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""RightFrontKey"",
                    ""type"": ""Button"",
                    ""id"": ""44a4a83a-4fe7-4b1d-8f4f-843ff709e747"",
                    ""expectedControlType"": """",
                    ""processors"": """",
                    ""interactions"": ""Press(behavior=1)"",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""RightBackKey"",
                    ""type"": ""Button"",
                    ""id"": ""66518414-a144-453c-b9b2-de3fe9d5393d"",
                    ""expectedControlType"": """",
                    ""processors"": """",
                    ""interactions"": ""Press(behavior=1)"",
                    ""initialStateCheck"": false
                }
            ],
            ""bindings"": [
                {
                    ""name"": """",
                    ""id"": ""dc6a0112-f438-4647-980b-d3d89ed15a1d"",
                    ""path"": ""<Keyboard>/q"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""CameraLeft"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""493b1695-0ff0-4003-8d19-ce75f9f5da4e"",
                    ""path"": ""<Keyboard>/e"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""CameraRight"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""e75e19fc-34c6-42c8-93d9-16ccabf57c8a"",
                    ""path"": ""<Keyboard>/shift"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""SpeedUp"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""2656008f-e72d-4e7a-b6d7-367b9010931f"",
                    ""path"": ""<Keyboard>/ctrl"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""SpeedDown"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""4ff0eeb5-59c8-412d-af3e-029adf36338d"",
                    ""path"": ""<Keyboard>/escape"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Settings"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""59ec1b40-ce48-4423-b1f2-14b240498f6a"",
                    ""path"": ""<Keyboard>/w"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""LeftFrontKey"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""30029fff-8817-4ddd-a53c-338124419662"",
                    ""path"": ""<Keyboard>/s"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""LeftBackKey"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""41438ad9-219d-4724-bed2-bcc50eb4ef90"",
                    ""path"": ""<Keyboard>/downArrow"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""RightBackKey"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""a912a2ee-5830-4f0c-a559-5dab7fa3ed1f"",
                    ""path"": ""<Keyboard>/upArrow"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""RightFrontKey"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                }
            ]
        }
    ],
    ""controlSchemes"": []
}");
        // Keyboard Control
        m_KeyboardControl = asset.FindActionMap("Keyboard Control", throwIfNotFound: true);
        m_KeyboardControl_CameraLeft = m_KeyboardControl.FindAction("CameraLeft", throwIfNotFound: true);
        m_KeyboardControl_CameraRight = m_KeyboardControl.FindAction("CameraRight", throwIfNotFound: true);
        m_KeyboardControl_SpeedUp = m_KeyboardControl.FindAction("SpeedUp", throwIfNotFound: true);
        m_KeyboardControl_SpeedDown = m_KeyboardControl.FindAction("SpeedDown", throwIfNotFound: true);
        m_KeyboardControl_Settings = m_KeyboardControl.FindAction("Settings", throwIfNotFound: true);
        m_KeyboardControl_LeftFrontKey = m_KeyboardControl.FindAction("LeftFrontKey", throwIfNotFound: true);
        m_KeyboardControl_LeftBackKey = m_KeyboardControl.FindAction("LeftBackKey", throwIfNotFound: true);
        m_KeyboardControl_RightFrontKey = m_KeyboardControl.FindAction("RightFrontKey", throwIfNotFound: true);
        m_KeyboardControl_RightBackKey = m_KeyboardControl.FindAction("RightBackKey", throwIfNotFound: true);
    }

    ~@KeyboardInput()
    {
        UnityEngine.Debug.Assert(!m_KeyboardControl.enabled, "This will cause a leak and performance issues, KeyboardInput.KeyboardControl.Disable() has not been called.");
    }

    public void Dispose()
    {
        UnityEngine.Object.Destroy(asset);
    }

    public InputBinding? bindingMask
    {
        get => asset.bindingMask;
        set => asset.bindingMask = value;
    }

    public ReadOnlyArray<InputDevice>? devices
    {
        get => asset.devices;
        set => asset.devices = value;
    }

    public ReadOnlyArray<InputControlScheme> controlSchemes => asset.controlSchemes;

    public bool Contains(InputAction action)
    {
        return asset.Contains(action);
    }

    public IEnumerator<InputAction> GetEnumerator()
    {
        return asset.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void Enable()
    {
        asset.Enable();
    }

    public void Disable()
    {
        asset.Disable();
    }

    public IEnumerable<InputBinding> bindings => asset.bindings;

    public InputAction FindAction(string actionNameOrId, bool throwIfNotFound = false)
    {
        return asset.FindAction(actionNameOrId, throwIfNotFound);
    }

    public int FindBinding(InputBinding bindingMask, out InputAction action)
    {
        return asset.FindBinding(bindingMask, out action);
    }

    // Keyboard Control
    private readonly InputActionMap m_KeyboardControl;
    private List<IKeyboardControlActions> m_KeyboardControlActionsCallbackInterfaces = new List<IKeyboardControlActions>();
    private readonly InputAction m_KeyboardControl_CameraLeft;
    private readonly InputAction m_KeyboardControl_CameraRight;
    private readonly InputAction m_KeyboardControl_SpeedUp;
    private readonly InputAction m_KeyboardControl_SpeedDown;
    private readonly InputAction m_KeyboardControl_Settings;
    private readonly InputAction m_KeyboardControl_LeftFrontKey;
    private readonly InputAction m_KeyboardControl_LeftBackKey;
    private readonly InputAction m_KeyboardControl_RightFrontKey;
    private readonly InputAction m_KeyboardControl_RightBackKey;
    public struct KeyboardControlActions
    {
        private @KeyboardInput m_Wrapper;
        public KeyboardControlActions(@KeyboardInput wrapper) { m_Wrapper = wrapper; }
        public InputAction @CameraLeft => m_Wrapper.m_KeyboardControl_CameraLeft;
        public InputAction @CameraRight => m_Wrapper.m_KeyboardControl_CameraRight;
        public InputAction @SpeedUp => m_Wrapper.m_KeyboardControl_SpeedUp;
        public InputAction @SpeedDown => m_Wrapper.m_KeyboardControl_SpeedDown;
        public InputAction @Settings => m_Wrapper.m_KeyboardControl_Settings;
        public InputAction @LeftFrontKey => m_Wrapper.m_KeyboardControl_LeftFrontKey;
        public InputAction @LeftBackKey => m_Wrapper.m_KeyboardControl_LeftBackKey;
        public InputAction @RightFrontKey => m_Wrapper.m_KeyboardControl_RightFrontKey;
        public InputAction @RightBackKey => m_Wrapper.m_KeyboardControl_RightBackKey;
        public InputActionMap Get() { return m_Wrapper.m_KeyboardControl; }
        public void Enable() { Get().Enable(); }
        public void Disable() { Get().Disable(); }
        public bool enabled => Get().enabled;
        public static implicit operator InputActionMap(KeyboardControlActions set) { return set.Get(); }
        public void AddCallbacks(IKeyboardControlActions instance)
        {
            if (instance == null || m_Wrapper.m_KeyboardControlActionsCallbackInterfaces.Contains(instance)) return;
            m_Wrapper.m_KeyboardControlActionsCallbackInterfaces.Add(instance);
            @CameraLeft.started += instance.OnCameraLeft;
            @CameraLeft.performed += instance.OnCameraLeft;
            @CameraLeft.canceled += instance.OnCameraLeft;
            @CameraRight.started += instance.OnCameraRight;
            @CameraRight.performed += instance.OnCameraRight;
            @CameraRight.canceled += instance.OnCameraRight;
            @SpeedUp.started += instance.OnSpeedUp;
            @SpeedUp.performed += instance.OnSpeedUp;
            @SpeedUp.canceled += instance.OnSpeedUp;
            @SpeedDown.started += instance.OnSpeedDown;
            @SpeedDown.performed += instance.OnSpeedDown;
            @SpeedDown.canceled += instance.OnSpeedDown;
            @Settings.started += instance.OnSettings;
            @Settings.performed += instance.OnSettings;
            @Settings.canceled += instance.OnSettings;
            @LeftFrontKey.started += instance.OnLeftFrontKey;
            @LeftFrontKey.performed += instance.OnLeftFrontKey;
            @LeftFrontKey.canceled += instance.OnLeftFrontKey;
            @LeftBackKey.started += instance.OnLeftBackKey;
            @LeftBackKey.performed += instance.OnLeftBackKey;
            @LeftBackKey.canceled += instance.OnLeftBackKey;
            @RightFrontKey.started += instance.OnRightFrontKey;
            @RightFrontKey.performed += instance.OnRightFrontKey;
            @RightFrontKey.canceled += instance.OnRightFrontKey;
            @RightBackKey.started += instance.OnRightBackKey;
            @RightBackKey.performed += instance.OnRightBackKey;
            @RightBackKey.canceled += instance.OnRightBackKey;
        }

        private void UnregisterCallbacks(IKeyboardControlActions instance)
        {
            @CameraLeft.started -= instance.OnCameraLeft;
            @CameraLeft.performed -= instance.OnCameraLeft;
            @CameraLeft.canceled -= instance.OnCameraLeft;
            @CameraRight.started -= instance.OnCameraRight;
            @CameraRight.performed -= instance.OnCameraRight;
            @CameraRight.canceled -= instance.OnCameraRight;
            @SpeedUp.started -= instance.OnSpeedUp;
            @SpeedUp.performed -= instance.OnSpeedUp;
            @SpeedUp.canceled -= instance.OnSpeedUp;
            @SpeedDown.started -= instance.OnSpeedDown;
            @SpeedDown.performed -= instance.OnSpeedDown;
            @SpeedDown.canceled -= instance.OnSpeedDown;
            @Settings.started -= instance.OnSettings;
            @Settings.performed -= instance.OnSettings;
            @Settings.canceled -= instance.OnSettings;
            @LeftFrontKey.started -= instance.OnLeftFrontKey;
            @LeftFrontKey.performed -= instance.OnLeftFrontKey;
            @LeftFrontKey.canceled -= instance.OnLeftFrontKey;
            @LeftBackKey.started -= instance.OnLeftBackKey;
            @LeftBackKey.performed -= instance.OnLeftBackKey;
            @LeftBackKey.canceled -= instance.OnLeftBackKey;
            @RightFrontKey.started -= instance.OnRightFrontKey;
            @RightFrontKey.performed -= instance.OnRightFrontKey;
            @RightFrontKey.canceled -= instance.OnRightFrontKey;
            @RightBackKey.started -= instance.OnRightBackKey;
            @RightBackKey.performed -= instance.OnRightBackKey;
            @RightBackKey.canceled -= instance.OnRightBackKey;
        }

        public void RemoveCallbacks(IKeyboardControlActions instance)
        {
            if (m_Wrapper.m_KeyboardControlActionsCallbackInterfaces.Remove(instance))
                UnregisterCallbacks(instance);
        }

        public void SetCallbacks(IKeyboardControlActions instance)
        {
            foreach (var item in m_Wrapper.m_KeyboardControlActionsCallbackInterfaces)
                UnregisterCallbacks(item);
            m_Wrapper.m_KeyboardControlActionsCallbackInterfaces.Clear();
            AddCallbacks(instance);
        }
    }
    public KeyboardControlActions @KeyboardControl => new KeyboardControlActions(this);
    public interface IKeyboardControlActions
    {
        void OnCameraLeft(InputAction.CallbackContext context);
        void OnCameraRight(InputAction.CallbackContext context);
        void OnSpeedUp(InputAction.CallbackContext context);
        void OnSpeedDown(InputAction.CallbackContext context);
        void OnSettings(InputAction.CallbackContext context);
        void OnLeftFrontKey(InputAction.CallbackContext context);
        void OnLeftBackKey(InputAction.CallbackContext context);
        void OnRightFrontKey(InputAction.CallbackContext context);
        void OnRightBackKey(InputAction.CallbackContext context);
    }
}
