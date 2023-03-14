using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class PanelControl : MonoBehaviour
{
    public GameObject _chatPanel;
    public TMP_Text _btnText;

    public void PanelCon()
    {
        _chatPanel.SetActive(!_chatPanel.activeSelf);
        _btnText.text = _chatPanel.activeSelf ? "X" : "<";
    }
}
