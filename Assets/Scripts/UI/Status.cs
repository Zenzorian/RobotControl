using UnityEngine;
using UnityEngine.UI;

namespace Scripts.UI
{
    public class Status : MonoBehaviour
    {
        [SerializeField] private Color _activeColor = Color.green;
        [SerializeField] private Color _inactiveColor = Color.red;

        [SerializeField] private Image _image;

        public void Initialize(bool isActive)
        {
            _image = GetComponent<Image>();
            _image.color = isActive ? _activeColor : _inactiveColor;
        }
    }
}
