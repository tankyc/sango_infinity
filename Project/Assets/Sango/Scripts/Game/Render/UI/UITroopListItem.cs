using UnityEngine;
using UnityEngine.UI;

public class UITroopListItem : MonoBehaviour 
{
    public Text name;
    public int index;
    public delegate void OnSelect(int idx);
    public delegate void OnShow(UITroopListItem item);
    public OnSelect onSelected;
    public OnShow onShow;

    void ScrollCellIndex(int idx) 
    {
        index = idx;
        onShow?.Invoke(this);
    }
    public void OnClick()
    {
        onSelected?.Invoke(index);
    }

}
