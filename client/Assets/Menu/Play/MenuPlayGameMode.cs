using System;
using Internal;
using Meta;
using Shared;
using TMPro;
using Tools;
using UnityEngine;
using UnityEngine.UI;

namespace Menu.Play
{
    [DisallowMultipleComponent]
    public class MenuPlayGameMode : MonoBehaviour
    {
        [SerializeField] private Image _plate;
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _description;
        [SerializeField] private Button _button;

        public GameMatchType Type { get; private set; }

        public void Setup(IGameModeDefinition definition)
        {
            Type = definition.Type;
            _name.text = definition.Name;
            _description.text = definition.Description;
            _icon.sprite = definition.Image;
            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            _plate.sprite = selected == true ? Sprites.MenuPlay.PlateSelected : Sprites.MenuPlay.PlateBase;
        }

        public void ListenClick(IReadOnlyLifetime lifetime, Action callback)
        {
            _button.ListenClick(lifetime, callback.Invoke);
        }
    }
}