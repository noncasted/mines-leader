using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Menu
{
    public interface IMenuPlayerChatView
    {
        void ShowMessage(string message);
    }
    
    [DisallowMultipleComponent]
    public class MenuPlayerChatView : MonoBehaviour, IMenuPlayerChatView
    {
        [SerializeField] private TMP_Text _text;
        [SerializeField] private float _time;

        private int _index;
        
        public void ShowMessage(string message)
        {
            _text.text = message;
            _index++;
            var indexSave = _index;

            _text.text = message;

            UniTask.Create(async () =>
            {
                await UniTask.Delay(TimeSpan.FromSeconds(_time));
                
                if (indexSave != _index)
                    return;
                
                _text.text = string.Empty;
            });
        }
    }
}