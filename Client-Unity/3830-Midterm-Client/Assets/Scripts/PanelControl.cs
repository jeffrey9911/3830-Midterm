using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UIElements;

public class PanelControl : MonoBehaviour
{
    public GameObject _chatPanel;
    public TMP_Text _btnText;

    bool isPerfroming = false;
    bool isPanelOpen = true;

    float timer = 0;

    public void PanelCon()
    {
        isPanelOpen = !isPanelOpen;
        isPerfroming = true;
        timer = 0;

        _btnText.text = isPanelOpen ? "X" : "<";

    }

    private void Update()
    {
        if(isPerfroming)
        {
            Vector3 scale = _chatPanel.GetComponent<RectTransform>().localScale;
            if (timer >= 1) timer = 1;

            if(isPanelOpen)
            {
                _chatPanel.GetComponent<RectTransform>().localScale = new Vector3(Mathf.Lerp(scale.x, 1, timer), scale.y, scale.z);
            }
            else
            {
                _chatPanel.GetComponent<RectTransform>().localScale = new Vector3(Mathf.Lerp(scale.x, 0, timer), scale.y, scale.z);
            }



            if(timer >= 1)
            {
                isPerfroming = false;
                timer = 0;
            }
            timer += Time.deltaTime;
        }
    }
}
