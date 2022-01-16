using UnityEngine;

//-----------------------------------------------------------------------------
// name: CamRotator.cs
// desc: uses keyboard input to toggle camera rotation on/off
//-----------------------------------------------------------------------------

public class CamRotator : MonoBehaviour
{
    public float speed;
    private float toggle = 0;

    void Update()
    {
        // R = toggle rotate on/off
        if (Input.GetKeyUp(KeyCode.R))
        {
            if (toggle == 1)
            {
                // glide into new position
                iTween.MoveTo(gameObject, iTween.Hash("position",
                                                      new Vector3(0.62f,1.1f,6.49f),
                                                      "time",
                                                      1.5f,
                                                      "easetype",
                                                      iTween.EaseType.easeInOutSine));
                toggle = 0;
            }
            else
            {
                iTween.MoveTo(gameObject, iTween.Hash("position",
                                                      new Vector3(0.62f,-4f,6.49f),
                                                      "time",
                                                      1.5f,
                                                      "easetype",
                                                      iTween.EaseType.easeInOutSine));
                toggle = 1;
            }
        }

        transform.Rotate(0, toggle * speed * Time.deltaTime, 0);
    }
}