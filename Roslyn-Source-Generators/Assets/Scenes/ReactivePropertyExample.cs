using System;
using UnityEngine;

namespace Scenes
{
    public partial class ReactivePropertyExample : MonoBehaviour
    {
        [ReactiveProperty] private float _reactiveField;
        private void Start()
        {
            ReactiveField = 10;
        }
    }
}
