using UnityEngine;
using UnityEngine.UI;

namespace Global.UI
{
    [DisallowMultipleComponent]
    public class LoadingScreenAnimation : MonoBehaviour
    {
        [SerializeField] private float _loopTime;
        [SerializeField] private Sprite[] _sequence;
        [SerializeField] private Image _image;

        private float _time;

        public void UpdateAnimation(float delta)
        {
            if (_sequence.Length == 0)
                return;

            _time += delta;

            var index = Mathf.FloorToInt(_time / _loopTime * _sequence.Length) % _sequence.Length;
            _image.sprite = _sequence[index];
        }
    }
}