using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class DisplayedName : MonoBehaviour
{
    public Canvas _canvas;
    public TMP_Text _nameText;

    private void Update()
    {
        _canvas.transform.LookAt(GameObject.FindGameObjectWithTag("MainCamera").transform.position);
    }
}
