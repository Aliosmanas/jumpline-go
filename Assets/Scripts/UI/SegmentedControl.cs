using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Jumpline.UI
{
    /// <summary>Tek seçimli buton grubu (grafik kalitesi, market sekmeleri).</summary>
    public class SegmentedControl : MonoBehaviour
    {
        [Serializable] public class IndexEvent : UnityEvent<int> { }

        [SerializeField] private Button[] segments;
        [SerializeField] private GameObject[] selectedVisuals;
        [SerializeField] private TMP_Text[] labels;
        [SerializeField] private Color selectedLabelColor = Color.white;
        [SerializeField] private Color normalLabelColor = Color.white;
        [SerializeField] private int selectedIndex;
        [SerializeField] private IndexEvent onChanged = new IndexEvent();

        public int SelectedIndex => selectedIndex;
        public IndexEvent OnChanged => onChanged;

        private void Awake()
        {
            for (int i = 0; i < segments.Length; i++)
            {
                int index = i;
                segments[i].onClick.AddListener(() => Select(index));
            }
            Apply();
        }

        public void Select(int index)
        {
            SetWithoutNotify(index);
            onChanged.Invoke(selectedIndex);
        }

        public void SetWithoutNotify(int index)
        {
            selectedIndex = Mathf.Clamp(index, 0, segments.Length - 1);
            Apply();
        }

        private void Apply()
        {
            for (int i = 0; i < segments.Length; i++)
            {
                bool on = i == selectedIndex;
                if (i < selectedVisuals.Length && selectedVisuals[i] != null) selectedVisuals[i].SetActive(on);
                if (i < labels.Length && labels[i] != null) labels[i].color = on ? selectedLabelColor : normalLabelColor;
            }
        }
    }
}
